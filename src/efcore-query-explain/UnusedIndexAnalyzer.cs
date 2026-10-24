using System.Text.RegularExpressions;

namespace EfCore.QueryExplain;

/// <summary>
/// <see cref="IQueryAnalyzer"/> that reports filters which wrap a column in a function, cast or leading-wildcard
/// <c>LIKE</c>, so an existing index on that column cannot be used, and only when the plan still contains an
/// unindexed scan.
/// </summary>
public sealed class UnusedIndexAnalyzer : IQueryAnalyzer
{
    // A quoted, [bracketed] or bare identifier, with an optional table/alias qualifier.
    private const string Qualifier = @"(?:(?:""[^""]+""|\[[^\]]+\]|\w+)\s*\.\s*)?";
    private const string Column = @"""(?<col>[^""]+)""|\[(?<col>[^\]]+)\]|(?<col>\w+)";

    private static readonly Regex WhereClause = new(
        @"\bWHERE\b(?<where>.*?)(?=\s(?:ORDER\s+BY|GROUP\s+BY|LIMIT|OFFSET)\b|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex[] WrappedColumnPatterns =
    [
        // LOWER("col"), UPPER("col"), DATE("col")
        new($@"\b(?:LOWER|UPPER|DATE)\s*\(\s*{Qualifier}(?:{Column})\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // CAST("col" AS ...)
        new($@"\bCAST\s*\(\s*{Qualifier}(?:{Column})\s+AS\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // CONVERT(type, "col")
        new($@"\bCONVERT\s*\(\s*[^,)]+,\s*{Qualifier}(?:{Column})\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // strftime(fmt, "col")
        new($@"\bstrftime\s*\(\s*[^,)]+,\s*{Qualifier}(?:{Column})\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // "col"::text
        new($@"{Qualifier}(?:{Column})\s*::\s*\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // "col" LIKE '%...  (leading wildcard defeats a B-tree index)
        new($@"{Qualifier}(?:{Column})\s+(?:NOT\s+)?LIKE\s+'%", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    /// <inheritdoc />
    public IReadOnlyList<QueryFinding> Analyze(QueryPlan plan, QueryExplainOptions options)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.SuggestIndexes)
        {
            return [];
        }

        var where = WhereClause.Match(plan.CommandText);
        if (!where.Success || !plan.Scans().Any(node => node.Index is null))
        {
            return [];
        }

        var findings = new List<QueryFinding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in WrappedColumnPatterns)
        {
            foreach (Match match in pattern.Matches(where.Groups["where"].Value))
            {
                var column = match.Groups["col"].Value;

                if (!seen.Add(column))
                {
                    continue;
                }

                findings.Add(new QueryFinding(
                    FindingKind.UnusedIndex,
                    FindingSeverity.Warning,
                    $"Filter uses a function/cast on column {column}; existing indexes cannot be used. Consider a computed column or expression index.",
                    plan.CommandText));
            }
        }

        return findings;
    }
}
