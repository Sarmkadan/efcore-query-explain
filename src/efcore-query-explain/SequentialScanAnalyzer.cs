namespace EfCore.QueryExplain;

/// <summary>
/// Default <see cref="IQueryAnalyzer"/> that reports full-table scans over large estimated row counts
/// as missing-index candidates.
/// </summary>
public sealed class SequentialScanAnalyzer : IQueryAnalyzer
{
    private static readonly HashSet<string> ScanOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "Seq Scan",
        "Table Scan",
        "Clustered Index Scan",
        "SCAN TABLE",
        "SCAN",
    };

    /// <inheritdoc />
    public IReadOnlyList<QueryFinding> Analyze(QueryPlan plan, QueryExplainOptions options)
    {
        if (!options.SuggestIndexes)
        {
            return [];
        }

        var findings = new List<QueryFinding>();

        foreach (var node in Flatten(plan.Nodes))
        {
            if (!ScanOperations.Contains(node.Operation) ||
                node.Table is null ||
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

    private static IEnumerable<PlanNode> Flatten(IEnumerable<PlanNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}
