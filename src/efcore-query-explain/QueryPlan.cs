namespace EfCore.QueryExplain;

/// <summary>
/// A captured execution plan for a single SQL command.
/// </summary>
/// <param name="CommandText">The SQL text as sent by EF Core.</param>
/// <param name="Provider">The provider name (e.g. <c>Npgsql</c>, <c>SqlServer</c>, <c>Sqlite</c>).</param>
/// <param name="RawPlan">The provider-native plan output, verbatim.</param>
/// <param name="Nodes">The parsed plan tree, or empty when the provider plan could not be parsed.</param>
/// <param name="CapturedAt">When the plan was captured (UTC).</param>
public sealed record QueryPlan(
    string CommandText,
    string Provider,
    string RawPlan,
    IReadOnlyList<PlanNode> Nodes,
    DateTimeOffset CapturedAt);

/// <summary>
/// A single node in a parsed execution plan.
/// </summary>
/// <param name="Operation">The operation name (e.g. <c>Seq Scan</c>, <c>Index Scan</c>, <c>Nested Loop</c>).</param>
/// <param name="Table">The relation the node operates on, if any.</param>
/// <param name="Index">The index used by the node, if any.</param>
/// <param name="EstimatedRows">The planner's estimated row count, if reported.</param>
/// <param name="EstimatedCost">The planner's estimated cost, if reported.</param>
/// <param name="Children">Child nodes.</param>
public sealed record PlanNode(
    string Operation,
    string? Table,
    string? Index,
    long? EstimatedRows,
    double? EstimatedCost,
    IReadOnlyList<PlanNode> Children);
