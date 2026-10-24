namespace EfCore.QueryExplain;

/// <summary>
/// Validates <see cref="QueryExplainOptions"/> before they are handed to <see cref="QueryExplainInterceptor"/>.
/// </summary>
internal static class QueryExplainOptionsValidation
{
    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> when a threshold or timeout holds a value the interceptor cannot use.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">A property is outside its allowed range.</exception>
    internal static void Validate(this QueryExplainOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.NPlusOneThreshold < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(QueryExplainOptions.NPlusOneThreshold),
                options.NPlusOneThreshold,
                $"{nameof(QueryExplainOptions.NPlusOneThreshold)} must be >= 2.");
        }

        if (options.ScanRowThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(QueryExplainOptions.ScanRowThreshold),
                options.ScanRowThreshold,
                $"{nameof(QueryExplainOptions.ScanRowThreshold)} must be >= 0.");
        }

        if (options.ExplainTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(QueryExplainOptions.ExplainTimeout),
                options.ExplainTimeout,
                $"{nameof(QueryExplainOptions.ExplainTimeout)} must be greater than zero.");
        }
    }
}
