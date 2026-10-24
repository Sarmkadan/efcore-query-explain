using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EfCore.QueryExplain;

/// <summary>
/// EF Core <see cref="DbCommandInterceptor"/> that captures EXPLAIN plans before reader commands execute,
/// runs the registered <see cref="IQueryAnalyzer"/>s, detects N+1 patterns and reports findings through
/// <see cref="ILogger"/> and <see cref="FindingReported"/>.
/// </summary>
/// <remarks>
/// Intended for development environments only: every intercepted query costs one extra EXPLAIN round-trip.
/// </remarks>
public sealed class QueryExplainInterceptor : DbCommandInterceptor
{
    private static readonly string[] NonAnalyzedPrefixes =
    {
        "INSERT", "UPDATE", "DELETE", "CREATE", "ALTER", "DROP",
    };

    private readonly QueryExplainOptions _options;
    private readonly IReadOnlyList<IQueryPlanProvider> _planProviders;
    private readonly IReadOnlyList<IQueryAnalyzer> _analyzers;
    private readonly INPlusOneDetector _nPlusOneDetector;
    private readonly ILogger<QueryExplainInterceptor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExplainInterceptor"/> class.
    /// </summary>
    /// <param name="options">Behavior configuration.</param>
    /// <param name="planProviders">Provider-specific EXPLAIN implementations; the one matching the active provider is used.</param>
    /// <param name="analyzers">Plan analyzers to run on each captured plan.</param>
    /// <param name="nPlusOneDetector">Cross-query N+1 detector.</param>
    /// <param name="logger">Logger used to surface findings.</param>
    public QueryExplainInterceptor(
        QueryExplainOptions options,
        IEnumerable<IQueryPlanProvider> planProviders,
        IEnumerable<IQueryAnalyzer> analyzers,
        INPlusOneDetector nPlusOneDetector,
        ILogger<QueryExplainInterceptor> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _planProviders = planProviders?.ToList() ?? throw new ArgumentNullException(nameof(planProviders));
        _analyzers = analyzers?.ToList() ?? throw new ArgumentNullException(nameof(analyzers));
        _nPlusOneDetector = nPlusOneDetector ?? throw new ArgumentNullException(nameof(nPlusOneDetector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Raised for every finding produced by the analyzers or the N+1 detector.
    /// </summary>
    public event EventHandler<QueryFinding>? FindingReported;

    /// <summary>
    /// Synchronous entry point for reader commands. Blocks the calling thread until analysis completes.
    /// </summary>
    /// <remarks>
    /// The sync path blocks on <see cref="AnalyzeCommandAsync"/> via <c>GetAwaiter().GetResult()</c>.
    /// Prefer the async execution path in applications that use async EF Core APIs, as it does not block.
    /// Failures are logged and never propagated to the application.
    /// </remarks>
    /// <inheritdoc />
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        if (!_options.Enabled || result.HasResult || !ShouldAnalyze(command))
        {
            return result;
        }

        try
        {
            AnalyzeCommandAsync(command, eventData).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Query analysis failed");
        }

        return result;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || result.HasResult || !ShouldAnalyze(command))
        {
            return result;
        }

        try
        {
            await AnalyzeCommandAsync(command, eventData, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Query analysis failed");
        }

        return result;
    }

    /// <summary>
    /// Captures the plan for <paramref name="command"/>, runs analyzers and N+1 detection, and reports findings.
    /// </summary>
    /// <param name="command">The command about to execute.</param>
    /// <param name="eventData">EF Core event data, used to resolve the provider and context id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The findings for this command, possibly empty.</returns>
    public async Task<IReadOnlyList<QueryFinding>> AnalyzeCommandAsync(
        DbCommand command,
        CommandEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Array.Empty<QueryFinding>();
        }

        var findings = new List<QueryFinding>();
        var providerName = eventData.Context?.Database.ProviderName;

        if (_options.DetectNPlusOne && eventData.Context != null)
        {
            var nPlusOne = _nPlusOneDetector.Record(eventData.Context.ContextId.InstanceId, command.CommandText, _options);
            if (nPlusOne != null)
            {
                findings.Add(nPlusOne);
            }
        }

        if (_options.CapturePlans && providerName != null && IsExplainable(command.CommandText))
        {
            var planProvider = _planProviders.FirstOrDefault(p =>
                string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

            if (planProvider != null)
            {
                using var explainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                explainCts.CancelAfter(_options.ExplainTimeout);

                QueryPlan? plan = null;
                try
                {
                    plan = await planProvider.ExplainAsync(command, explainCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("EXPLAIN timed out after {Timeout}", _options.ExplainTimeout);
                }
                catch (DbException ex)
                {
                    _logger.LogDebug(ex, "EXPLAIN failed for provider {Provider}", providerName);
                }

                if (plan != null)
                {
                    foreach (var analyzer in _analyzers)
                    {
                        findings.AddRange(analyzer.Analyze(plan, _options));
                    }
                }
            }
        }

        foreach (var finding in findings)
        {
            Report(finding);
        }

        return findings;
    }

    /// <summary>
    /// Returns <c>true</c> for SELECT statements; EXPLAIN itself and other commands are not captured,
    /// which also keeps the interceptor from recursing into its own EXPLAIN commands.
    /// </summary>
    /// <param name="commandText">The SQL text of the command.</param>
    private static bool IsExplainable(string commandText) =>
        commandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <c>false</c> for commands without SQL text and for data-modifying or DDL statements,
    /// which are never analyzed.
    /// </summary>
    /// <param name="command">The command about to execute.</param>
    private static bool ShouldAnalyze(DbCommand command)
    {
        var commandText = command.CommandText;
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return false;
        }

        var trimmed = commandText.TrimStart();
        return !NonAnalyzedPrefixes.Any(prefix => trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Logs <paramref name="finding"/> and raises <see cref="FindingReported"/>.
    /// </summary>
    /// <param name="finding">The finding to publish.</param>
    private void Report(QueryFinding finding)
    {
        var level = finding.Severity switch
        {
            FindingSeverity.Error => LogLevel.Error,
            FindingSeverity.Warning => LogLevel.Warning,
            _ => LogLevel.Information,
        };

        if (_logger.IsEnabled(level))
        {
            var eventId = finding.Kind switch
            {
                FindingKind.MissingIndex => LogEvents.MissingIndex,
                FindingKind.NPlusOne => LogEvents.NPlusOne,
                FindingKind.UnusedIndex => LogEvents.UnusedIndex,
                _ => LogEvents.Other,
            };

            if (finding.SuggestedSql is null)
            {
                _logger.Log(
                    level,
                    eventId,
                    "{Kind}: {Message}{NewLine}{CommandText}",
                    finding.Kind,
                    finding.Message,
                    Environment.NewLine,
                    finding.CommandText);
            }
            else
            {
                _logger.Log(
                    level,
                    eventId,
                    "{Kind}: {Message}{NewLine}Suggested: {SuggestedSql}{NewLine}{CommandText}",
                    finding.Kind,
                    finding.Message,
                    Environment.NewLine,
                    finding.SuggestedSql,
                    Environment.NewLine,
                    finding.CommandText);
            }
        }

        try
        {
            FindingReported?.Invoke(this, finding);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FindingReported subscriber threw for {Kind}", finding.Kind);
        }
    }
}
