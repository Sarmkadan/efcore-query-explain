namespace EfCore.QueryExplain;

/// <summary>
/// Severity of a <see cref="QueryFinding"/>.
/// </summary>
public enum FindingSeverity
{
    /// <summary>Informational only.</summary>
    Info,

    /// <summary>Likely performance problem worth investigating.</summary>
    Warning,

    /// <summary>Almost certainly a performance problem (e.g. full scan on a large table, confirmed N+1).</summary>
    Error,
}

/// <summary>
/// Kind of problem detected by an analyzer.
/// </summary>
public enum FindingKind
{
    /// <summary>A scan that would likely benefit from an index.</summary>
    MissingIndex,

    /// <summary>The same query shape executed repeatedly in one context scope.</summary>
    NPlusOne,

    /// <summary>An index exists but the planner chose not to use it (type mismatch, function on column, etc.).</summary>
    UnusedIndex,

    /// <summary>Any other plan-derived observation.</summary>
    Other,
}

/// <summary>
/// A single diagnostic produced from a captured plan or a sequence of commands.
/// </summary>
/// <param name="Kind">The category of the finding.</param>
/// <param name="Severity">How serious the finding is.</param>
/// <param name="Message">Human-readable explanation.</param>
/// <param name="CommandText">The offending SQL.</param>
/// <param name="SuggestedSql">A suggested fix as SQL (e.g. a <c>CREATE INDEX</c> statement), if applicable.</param>
public sealed record QueryFinding(
    FindingKind Kind,
    FindingSeverity Severity,
    string Message,
    string CommandText,
    string? SuggestedSql = null);

/// <summary>
/// Inspects a captured <see cref="QueryPlan"/> and produces zero or more <see cref="QueryFinding"/>s.
/// Analyzers are stateless per plan; cross-query detection (N+1) is handled by <see cref="INPlusOneDetector"/>.
/// </summary>
public interface IQueryAnalyzer
{
    /// <summary>
    /// Analyzes <paramref name="plan"/> according to <paramref name="options"/>.
    /// </summary>
    /// <param name="plan">The plan to analyze.</param>
    /// <param name="options">The active options.</param>
    /// <returns>Findings for this plan, possibly empty.</returns>
    IReadOnlyList<QueryFinding> Analyze(QueryPlan plan, QueryExplainOptions options);
}

/// <summary>
/// Tracks command executions within a single <c>DbContext</c> lifetime and detects N+1 patterns.
/// </summary>
public interface INPlusOneDetector
{
    /// <summary>
    /// Records an execution of <paramref name="commandText"/> for the context identified by <paramref name="contextId"/>.
    /// </summary>
    /// <param name="contextId">The <c>DbContextId</c> of the executing context.</param>
    /// <param name="commandText">The parameterized SQL text.</param>
    /// <param name="options">The active options.</param>
    /// <returns>A finding if the threshold was just crossed for this query shape; otherwise <c>null</c>.</returns>
    QueryFinding? Record(Guid contextId, string commandText, QueryExplainOptions options);

    /// <summary>
    /// Clears all tracked state for <paramref name="contextId"/>. Called when the context is disposed.
    /// </summary>
    /// <param name="contextId">The <c>DbContextId</c> to forget.</param>
    void Reset(Guid contextId);
}
