namespace EfCore.QueryExplain;

/// <summary>
/// Default <see cref="IQueryAnalyzer"/> that reports full-table scans over large estimated row counts
/// as missing-index candidates.
/// </summary>
public sealed class SequentialScanAnalyzer : IQueryAnalyzer
{
    /// <inheritdoc />
    public IReadOnlyList<QueryFinding> Analyze(QueryPlan plan, QueryExplainOptions options)
    {
        if (!options.SuggestIndexes)
        {
            return [];
        }

        var findings = new List<QueryFinding>();

        foreach (var node in plan.Scans())
        {
            if (node.Table is null ||
                node.EstimatedRows is not { } rows ||
                rows < options.ScanRowThreshold)
            {
                continue;
            }

            var severity = rows >= 10 * options.ScanRowThreshold
                ? FindingSeverity.Error
                : FindingSeverity.Warning;

            var message = $"{node.Operation} on \"{node.Table}\" (est. {rows} rows)";

            var suggestedSql = IndexSuggestionBuilder.Build(plan.CommandText, node.Table, plan.Provider);

            findings.Add(new QueryFinding(
                FindingKind.MissingIndex,
                severity,
                message,
                plan.CommandText,
                suggestedSql));
        }

        return findings;
    }
}
