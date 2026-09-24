namespace EfCore.QueryExplain;

/// <summary>
/// Configures how <see cref="QueryExplainInterceptor"/> captures plans and reports findings.
/// </summary>
public sealed class QueryExplainOptions
{
    /// <summary>
    /// Gets or sets whether EXPLAIN plans are captured for every executed reader command.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool CapturePlans { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the analyzer emits missing-index suggestions based on captured plans.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool SuggestIndexes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether N+1 query patterns are detected within a single <c>DbContext</c> scope.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool DetectNPlusOne { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of structurally identical queries (same SQL, different parameters)
    /// executed within one context scope after which an N+1 warning is raised.
    /// Defaults to <c>5</c>.
    /// </summary>
    public int NPlusOneThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the estimated row count above which a sequential/full scan is reported
    /// as a missing-index candidate. Defaults to <c>1000</c>.
    /// </summary>
    public long ScanRowThreshold { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum time spent running EXPLAIN for a single query.
    /// Defaults to 2 seconds.
    /// </summary>
    public TimeSpan ExplainTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets whether the interceptor is active. When <c>false</c>, all commands pass through untouched.
    /// Intended to be bound to the hosting environment (e.g. only enabled in Development).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
