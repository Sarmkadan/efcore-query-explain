namespace EfCore.QueryExplain.Tests;

public class PlanNodeExtensionsTests
{
    [Fact]
    public void Flatten_ThreeLevelTree_ReturnsFourNodesDepthFirst()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var operations = plan.Flatten().Select(node => node.Operation).ToList();

        // Assert
        Assert.Equal(new[] { "Aggregate", "Hash Join", "Seq Scan", "Index Scan" }, operations);
    }

    [Fact]
    public void Flatten_EmptyPlan_ReturnsEmpty()
    {
        // Arrange
        var plan = CreatePlan(Array.Empty<PlanNode>());

        // Act
        var nodes = plan.Flatten();

        // Assert
        Assert.Empty(nodes);
    }

    [Fact]
    public void Scans_ThreeLevelTree_ReturnsOnlySeqScan()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var scans = plan.Scans().ToList();

        // Assert
        var scan = Assert.Single(scans);
        Assert.Equal("Seq Scan", scan.Operation);
        Assert.Equal("Orders", scan.Table);
    }

    [Theory]
    [InlineData("Seq Scan")]
    [InlineData("Table Scan")]
    [InlineData("Clustered Index Scan")]
    [InlineData("SCAN")]
    public void IsScan_FullScanOperation_ReturnsTrue(string operation)
    {
        // Arrange
        var node = Node(operation, table: "Orders", index: null, rows: null);

        // Act
        var result = node.IsScan();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsScan_IndexScanUsingIndex_ReturnsFalse()
    {
        // Arrange
        var node = Node("Index Scan", table: "Customers", index: "PK_Customers", rows: 1);

        // Act
        var result = node.IsScan();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsScan_HashJoin_ReturnsFalse()
    {
        // Arrange
        var node = Node("Hash Join", table: null, index: null, rows: null);

        // Act
        var result = node.IsScan();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TotalEstimatedRows_ThreeLevelTree_SumsEstimatesTreatingNullAsZero()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var total = plan.TotalEstimatedRows();

        // Assert
        Assert.Equal(120001, total);
    }

    [Fact]
    public void ToTreeString_ThreeLevelTree_ReturnsFourLines()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var lines = plan.ToTreeString().Split('\n');

        // Assert
        Assert.Equal(4, lines.Length);
    }

    [Fact]
    public void ToTreeString_ThreeLevelTree_IndentsChildByTwoSpaces()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var lines = plan.ToTreeString().Split('\n');

        // Assert
        Assert.Equal("  Hash Join", lines[1]);
    }

    [Fact]
    public void ToTreeString_ThreeLevelTree_ContainsSeqScanOnOrders()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var text = plan.ToTreeString();

        // Assert
        Assert.Contains("Seq Scan on Orders", text);
    }

    [Fact]
    public void ToTreeString_ThreeLevelTree_ContainsIndexUsingPrimaryKey()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var text = plan.ToTreeString();

        // Assert
        Assert.Contains("using PK_Customers", text);
    }

    private static QueryPlan CreatePlan(IReadOnlyList<PlanNode>? nodes = null) =>
        new(
            CommandText: "SELECT 1",
            Provider: "Npgsql",
            RawPlan: string.Empty,
            Nodes: nodes ?? new[] { AggregateTree() },
            CapturedAt: DateTimeOffset.UnixEpoch);

    private static PlanNode AggregateTree() =>
        Node(
            "Aggregate",
            table: null,
            index: null,
            rows: null,
            children: new[]
            {
                Node(
                    "Hash Join",
                    table: null,
                    index: null,
                    rows: null,
                    children: new[]
                    {
                        Node("Seq Scan", table: "Orders", index: null, rows: 120000),
                        Node("Index Scan", table: "Customers", index: "PK_Customers", rows: 1),
                    }),
            });

    private static PlanNode Node(
        string operation,
        string? table,
        string? index,
        long? rows,
        IReadOnlyList<PlanNode>? children = null) =>
        new(operation, table, index, rows, EstimatedCost: null, Children: children ?? Array.Empty<PlanNode>());
}
