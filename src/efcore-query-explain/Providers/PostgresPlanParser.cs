using System.Text.Json;

namespace EfCore.QueryExplain.Providers;

/// <summary>
/// Parses the output of PostgreSQL <c>EXPLAIN (FORMAT JSON)</c> into <see cref="PlanNode"/> trees.
/// </summary>
public static class PostgresPlanParser
{
    /// <summary>
    /// Parses an <c>EXPLAIN (FORMAT JSON)</c> document.
    /// </summary>
    /// <param name="explainJson">The raw JSON array returned by PostgreSQL.</param>
    /// <returns>
    /// A list containing the single root node, or an empty list when the JSON is invalid
    /// or does not contain a <c>Plan</c> object.
    /// </returns>
    public static IReadOnlyList<PlanNode> Parse(string explainJson)
    {
        ArgumentNullException.ThrowIfNull(explainJson);

        try
        {
            using var document = JsonDocument.Parse(explainJson);

            if (document.RootElement.ValueKind != JsonValueKind.Array ||
                document.RootElement.GetArrayLength() == 0 ||
                document.RootElement[0].ValueKind != JsonValueKind.Object ||
                !document.RootElement[0].TryGetProperty("Plan", out var plan) ||
                plan.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return [MapNode(plan)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static PlanNode MapNode(JsonElement plan)
    {
        var children = new List<PlanNode>();
        if (plan.TryGetProperty("Plans", out var plans) && plans.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in plans.EnumerateArray())
            {
                children.Add(MapNode(child));
            }
        }

        return new PlanNode(
            Operation: GetString(plan, "Node Type") ?? string.Empty,
            Table: GetString(plan, "Relation Name"),
            Index: GetString(plan, "Index Name"),
            EstimatedRows: plan.TryGetProperty("Plan Rows", out var rows) && rows.TryGetInt64(out var r) ? r : null,
            EstimatedCost: plan.TryGetProperty("Total Cost", out var cost) && cost.TryGetDouble(out var c) ? c : null,
            Children: children);
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
