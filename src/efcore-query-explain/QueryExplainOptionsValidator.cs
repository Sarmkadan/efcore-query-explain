namespace EfCore.QueryExplain;

/// <summary>
/// Checks <see cref="QueryExplainOptions"/> for values that cannot work with the interceptor
/// and reports them as human-readable messages.
/// </summary>
public static class QueryExplainOptionsValidator
{
    private static readonly TimeSpan MaxExplainTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Collects a message for every invalid value in <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The options to check.</param>
    /// <returns>The error messages in property order; empty when the options are valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> GetErrors(QueryExplainOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();

        if (options.NPlusOneThreshold < 2)
        {
            errors.Add("NPlusOneThreshold must be at least 2");
        }

        if (options.ScanRowThreshold < 0)
        {
            errors.Add("ScanRowThreshold must not be negative");
        }

        if (options.ExplainTimeout <= TimeSpan.Zero)
        {
            errors.Add("ExplainTimeout must be greater than zero");
        }
        else if (options.ExplainTimeout > MaxExplainTimeout)
        {
            errors.Add("ExplainTimeout above 60s is not sensible for development tooling");
        }

        return errors;
    }

    /// <summary>
    /// Throws when <paramref name="options"/> contains any invalid value.
    /// </summary>
    /// <param name="options">The options to check.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// One or more values are invalid. The message joins every error from <see cref="GetErrors"/> with "; ".
    /// </exception>
    public static void Validate(QueryExplainOptions options)
    {
        var errors = GetErrors(options);

        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join("; ", errors), nameof(options));
        }
    }
}
