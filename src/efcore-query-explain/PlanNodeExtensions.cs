using System.Globalization;
using System.Text;

namespace EfCore.QueryExplain;

/// <summary>
/// Helpers for traversing and rendering parsed <see cref="PlanNode"/> trees.
/// </summary>
public static class PlanNodeExtensions
{
    private static readonly HashSet<string> ScanOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "Seq Scan",
        "Table Scan",
        "Clustered Index Scan",
        "SCAN TABLE",
        "SCAN",
    };

    /// <summary>
    /// Yields each node depth-first, followed by its descendants.
    /// </summary>
    /// <param name="nodes">The root nodes to traverse.</param>
    /// <returns>Every node in the tree, parents before children.</returns>
    public static IEnumerable<PlanNode> Flatten(this IEnumerable<PlanNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in node.Children.Flatten())
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Yields every node of <paramref name="plan"/> depth-first.
    /// </summary>
    /// <param name="plan">The plan to traverse.</param>
    /// <returns>Every node in the plan, parents before children.</returns>
    public static IEnumerable<PlanNode> Flatten(this QueryPlan plan) => plan.Nodes.Flatten();

    /// <summary>
    /// Determines whether the node reads a relation by scanning rather than seeking.
    /// </summary>
    /// <param name="node">The node to test.</param>
    /// <returns>
    /// <c>true</c> when the operation is a known full-scan operation, or when it contains <c>Scan</c>
    /// and no index is used.
    /// </returns>
    public static bool IsScan(this PlanNode node) =>
        ScanOperations.Contains(node.Operation) ||
        (node.Index is null && node.Operation.Contains("Scan", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Yields every scan node of <paramref name="plan"/>.
    /// </summary>
    /// <param name="plan">The plan to search.</param>
    /// <returns>Every node for which <see cref="IsScan"/> is <c>true</c>.</returns>
    public static IEnumerable<PlanNode> Scans(this QueryPlan plan) => plan.Flatten().Where(node => node.IsScan());

    /// <summary>
    /// Sums the estimated row counts of every node in <paramref name="plan"/>.
    /// </summary>
    /// <param name="plan">The plan to sum.</param>
    /// <returns>The total estimated rows; nodes without an estimate count as zero.</returns>
    public static long TotalEstimatedRows(this QueryPlan plan) =>
        plan.Flatten().Sum(node => node.EstimatedRows ?? 0);

    /// <summary>
    /// Renders the plan as an indented, human-readable tree.
    /// </summary>
    /// <param name="plan">The plan to render.</param>
    /// <returns>
    /// One line per node, indented two spaces per level, for example
    /// <c>Nested Loop (cost=12.5)</c> followed by <c>  Seq Scan on Orders (rows=120000)</c>.
    /// </returns>
    public static string ToTreeString(this QueryPlan plan)
    {
        var builder = new StringBuilder();
        AppendTree(builder, plan.Nodes, depth: 0);
        return builder.ToString().TrimEnd('\n');
    }

    private static void AppendTree(StringBuilder builder, IEnumerable<PlanNode> nodes, int depth)
    {
        foreach (var node in nodes)
        {
            builder.Append(' ', depth * 2).Append(node.Operation);

            if (node.Table is not null)
            {
                builder.Append(" on ").Append(node.Table);
            }

            if (node.Index is not null)
            {
                builder.Append(" using ").Append(node.Index);
            }

            var details = new List<string>();

            if (node.EstimatedCost is { } cost)
            {
                details.Add($"cost={cost.ToString(CultureInfo.InvariantCulture)}");
            }

            if (node.EstimatedRows is { } rows)
            {
                details.Add($"rows={rows.ToString(CultureInfo.InvariantCulture)}");
            }

            if (details.Count > 0)
            {
                builder.Append(" (").Append(string.Join(", ", details)).Append(')');
            }

            builder.Append('\n');
            AppendTree(builder, node.Children, depth + 1);
        }
    }
}
