using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace EfCore.QueryExplain;

/// <summary>
/// Default <see cref="INPlusOneDetector"/>. Counts executions per query shape within each <c>DbContext</c> scope
/// and reports once when a shape reaches <see cref="QueryExplainOptions.NPlusOneThreshold"/>.
/// </summary>
public sealed class NPlusOneDetector : INPlusOneDetector
{
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, int>> _countsByContext = new();

    /// <inheritdoc />
    public QueryFinding? Record(Guid contextId, string commandText, QueryExplainOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        if (contextId == Guid.Empty)
        {
            throw new ArgumentException("contextId must not be empty", nameof(contextId));
        }

        if (!options.DetectNPlusOne)
        {
            return null;
        }

        var shape = Normalize(commandText);
        var countsByShape = _countsByContext.GetOrAdd(contextId, _ => new ConcurrentDictionary<string, int>());
        var count = countsByShape.AddOrUpdate(shape, 1, (_, current) => current + 1);

        if (count != options.NPlusOneThreshold)
        {
            return null;
        }

        return new QueryFinding(
            FindingKind.NPlusOne,
            FindingSeverity.Warning,
            $"Query executed {count} times in one DbContext scope. Consider .Include() or a join.",
            commandText);
    }

    /// <inheritdoc />
    public void Reset(Guid contextId)
    {
        _countsByContext.TryRemove(contextId, out _);
    }

    private static string Normalize(string commandText) =>
        WhitespaceRun.Replace(commandText.Trim(), " ");
}
