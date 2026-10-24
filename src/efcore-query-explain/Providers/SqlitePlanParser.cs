using System.Globalization;
using System.Text.RegularExpressions;

namespace EfCore.QueryExplain.Providers;

/// <summary>
/// Parses the output of SQLite <c>EXPLAIN QUERY PLAN</c> into <see cref="PlanNode"/> trees.
/// </summary>
/// <remarks>
/// SQLite does not report row or cost estimates, so <see cref="PlanNode.EstimatedRows"/> and
/// <see cref="PlanNode.EstimatedCost"/> are always <c>null</c>.
/// </remarks>
public static class SqlitePlanParser
{
    private static readonly Regex LineRegex = new(
        @"^\s*(?<id>-?\d+)\s*\|\s*(?<parent>-?\d+)\s*\|(?<detail>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ScanRegex = new(
        @"^SCAN\s+(?:TABLE\s+)?(?<table>\S+)(?:\s+AS\s+\S+)?" +
        @"(?:\s+USING\s+(?:COVERING\s+)?INDEX\s+(?<index>[^\s(]+)|\s+USING\s+INTEGER\s+PRIMARY\s+KEY)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SearchRegex = new(
        @"^SEARCH\s+(?:TABLE\s+)?(?<table>\S+)(?:\s+AS\s+\S+)?" +
        @"(?:\s+USING\s+(?:AUTOMATIC\s+)?(?:COVERING\s+)?INDEX\s+(?<index>[^\s(]+)|\s+USING\s+INTEGER\s+PRIMARY\s+KEY)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Parses raw <c>EXPLAIN QUERY PLAN</c> output, one <c>id|parent|detail</c> row per line.
    /// </summary>
    /// <param name="rawPlan">The raw plan text. Blank or malformed lines are ignored.</param>
    /// <returns>The root nodes of the plan, or an empty list when no row could be parsed.</returns>
    public static IReadOnlyList<PlanNode> Parse(string rawPlan)
    {
        ArgumentNullException.ThrowIfNull(rawPlan);

        var rows = new List<(long Id, long Parent, string Detail)>();
        foreach (var line in rawPlan.Split('\n'))
        {
            var match = LineRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            rows.Add((
                long.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture),
                long.Parse(match.Groups["parent"].Value, CultureInfo.InvariantCulture),
                match.Groups["detail"].Value.Trim()));
        }

        return Parse(rows);
    }

    /// <summary>
    /// Builds plan trees from <c>EXPLAIN QUERY PLAN</c> rows.
    /// </summary>
    /// <param name="rows">Rows as <c>(Id, Parent, Detail)</c>. Rows with parent <c>0</c> (or an unknown parent) are roots.</param>
    /// <returns>The root nodes of the plan, ordered by id.</returns>
    public static IReadOnlyList<PlanNode> Parse(IReadOnlyList<(long Id, long Parent, string Detail)> rows)
    {
        var ids = new HashSet<long>(rows.Select(r => r.Id));
        var childrenByParent = rows
            .GroupBy(r => r.Parent)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Id).ToList());

        var roots = rows
            .Where(r => r.Parent == 0 || !ids.Contains(r.Parent))
            .OrderBy(r => r.Id);

        return roots.Select(r => MapNode(r, childrenByParent, [])).ToList();
    }

    private static PlanNode MapNode(
        (long Id, long Parent, string Detail) row,
        IReadOnlyDictionary<long, List<(long Id, long Parent, string Detail)>> childrenByParent,
        HashSet<long> visited)
    {
        // Guards against malformed input where a row is its own ancestor.
        visited.Add(row.Id);

        var children = new List<PlanNode>();
        if (childrenByParent.TryGetValue(row.Id, out var childRows))
        {
            foreach (var child in childRows.Where(c => !visited.Contains(c.Id)))
            {
                children.Add(MapNode(child, childrenByParent, visited));
            }
        }

        visited.Remove(row.Id);

        var (operation, table, index) = ParseDetail(row.Detail);
        return new PlanNode(
            Operation: operation,
            Table: table,
            Index: index,
            EstimatedRows: null,
            EstimatedCost: null,
            Children: children);
    }

    private static (string Operation, string? Table, string? Index) ParseDetail(string detail)
    {
        var scan = ScanRegex.Match(detail);
        if (scan.Success)
        {
            return ("SCAN", scan.Groups["table"].Value, IndexOf(scan, detail));
        }

        var search = SearchRegex.Match(detail);
        if (search.Success)
        {
            return ("SEARCH", search.Groups["table"].Value, IndexOf(search, detail));
        }

        // Non-table operations (e.g. "USE TEMP B-TREE FOR ORDER BY") keep the full detail text.
        return (detail, null, null);
    }

    private static string? IndexOf(Match match, string detail)
    {
        if (match.Groups["index"].Success)
        {
            return match.Groups["index"].Value;
        }

        return detail.Contains("USING INTEGER PRIMARY KEY", StringComparison.Ordinal) ? "PRIMARY KEY" : null;
    }
}
