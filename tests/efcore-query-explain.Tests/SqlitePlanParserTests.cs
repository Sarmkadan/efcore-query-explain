using EfCore.QueryExplain.Providers;

namespace EfCore.QueryExplain.Tests;

public class SqlitePlanParserTests
{
    [Fact]
    public void Parse_ScanWithoutIndex_ReturnsScanOnTable()
    {
        var rows = new[] { (3L, 0L, "SCAN Orders") };

        var nodes = SqlitePlanParser.Parse(rows);

        var node = Assert.Single(nodes);
        Assert.Equal("SCAN", node.Operation);
        Assert.Equal("Orders", node.Table);
        Assert.Null(node.Index);
    }

    [Fact]
    public void Parse_ScanWithTablePrefix_ReturnsScanOnTable()
    {
        var rows = new[] { (3L, 0L, "SCAN TABLE Orders") };

        var node = Assert.Single(SqlitePlanParser.Parse(rows));

        Assert.Equal("SCAN", node.Operation);
        Assert.Equal("Orders", node.Table);
        Assert.Null(node.Index);
    }

    [Fact]
    public void Parse_SearchUsingNamedIndex_ReturnsIndexName()
    {
        var rows = new[] { (3L, 0L, "SEARCH Orders USING INDEX IX_Orders_CustomerId (CustomerId=?)") };

        var node = Assert.Single(SqlitePlanParser.Parse(rows));

        Assert.Equal("SEARCH", node.Operation);
        Assert.Equal("Orders", node.Table);
        Assert.Equal("IX_Orders_CustomerId", node.Index);
    }

    [Fact]
    public void Parse_SearchUsingIntegerPrimaryKey_ReturnsPrimaryKeyIndex()
    {
        var rows = new[] { (3L, 0L, "SEARCH Customers USING INTEGER PRIMARY KEY (rowid=?)") };

        var node = Assert.Single(SqlitePlanParser.Parse(rows));

        Assert.Equal("SEARCH", node.Operation);
        Assert.Equal("Customers", node.Table);
        Assert.Equal("PRIMARY KEY", node.Index);
    }

    [Fact]
    public void Parse_ScanWithAlias_ReturnsTableWithoutAlias()
    {
        var rows = new[] { (3L, 0L, "SCAN Orders AS o") };

        var node = Assert.Single(SqlitePlanParser.Parse(rows));

        Assert.Equal("SCAN", node.Operation);
        Assert.Equal("Orders", node.Table);
        Assert.Null(node.Index);
    }

    [Fact]
    public void Parse_NonTableOperation_KeepsDetailAsOperation()
    {
        var rows = new[] { (3L, 0L, "USE TEMP B-TREE FOR ORDER BY") };

        var node = Assert.Single(SqlitePlanParser.Parse(rows));

        Assert.Equal("USE TEMP B-TREE FOR ORDER BY", node.Operation);
        Assert.Null(node.Table);
        Assert.Null(node.Index);
    }

    [Fact]
    public void Parse_RowsWithSharedParent_BuildsSingleRootWithChildren()
    {
        var rows = new[]
        {
            (3L, 0L, "SCAN Orders"),
            (5L, 3L, "SEARCH Customers USING INTEGER PRIMARY KEY (rowid=?)"),
            (8L, 3L, "SEARCH OrderLines USING INDEX IX_OrderLines_OrderId (OrderId=?)"),
        };

        var nodes = SqlitePlanParser.Parse(rows);

        var root = Assert.Single(nodes);
        Assert.Equal("Orders", root.Table);
        Assert.Equal(2, root.Children.Count);
        Assert.Equal("Customers", root.Children[0].Table);
        Assert.Equal("OrderLines", root.Children[1].Table);
        Assert.Equal("IX_OrderLines_OrderId", root.Children[1].Index);
    }

    [Fact]
    public void Parse_RowsWithSharedParent_OrdersChildrenById()
    {
        var rows = new[]
        {
            (8L, 3L, "SCAN OrderLines"),
            (3L, 0L, "SCAN Orders"),
            (5L, 3L, "SCAN Customers"),
        };

        var root = Assert.Single(SqlitePlanParser.Parse(rows));

        Assert.Equal(new[] { "Customers", "OrderLines" }, root.Children.Select(c => c.Table));
    }

    [Fact]
    public void Parse_RawText_ParsesSameAsRows()
    {
        const string raw = "3|0|SCAN Orders\n5|3|SEARCH Customers USING INTEGER PRIMARY KEY (rowid=?)\n8|3|SCAN OrderLines";

        var fromText = SqlitePlanParser.Parse(raw);
        var fromRows = SqlitePlanParser.Parse(new[]
        {
            (3L, 0L, "SCAN Orders"),
            (5L, 3L, "SEARCH Customers USING INTEGER PRIMARY KEY (rowid=?)"),
            (8L, 3L, "SCAN OrderLines"),
        });

        Assert.Equal(Flatten(fromRows), Flatten(fromText));
    }

    // PlanNode.Children is a List, so record equality would compare references; flatten to values instead.
    private static List<(int Depth, string Operation, string? Table, string? Index)> Flatten(
        IReadOnlyList<PlanNode> nodes, int depth = 0)
    {
        var result = new List<(int, string, string?, string?)>();
        foreach (var node in nodes)
        {
            result.Add((depth, node.Operation, node.Table, node.Index));
            result.AddRange(Flatten(node.Children, depth + 1));
        }

        return result;
    }

    [Fact]
    public void Parse_EmptyRawText_ReturnsEmptyList()
    {
        var nodes = SqlitePlanParser.Parse(string.Empty);

        Assert.Empty(nodes);
    }

    [Fact]
    public void Parse_EmptyRows_ReturnsEmptyList()
    {
        var nodes = SqlitePlanParser.Parse(Array.Empty<(long Id, long Parent, string Detail)>());

        Assert.Empty(nodes);
    }

    [Fact]
    public void Parse_Rows_LeavesEstimatesNull()
    {
        var rows = new[]
        {
            (3L, 0L, "SCAN Orders"),
            (5L, 3L, "SEARCH Customers USING INTEGER PRIMARY KEY (rowid=?)"),
        };

        var root = Assert.Single(SqlitePlanParser.Parse(rows));

        Assert.Null(root.EstimatedRows);
        Assert.Null(root.EstimatedCost);
        Assert.All(root.Children, child =>
        {
            Assert.Null(child.EstimatedRows);
            Assert.Null(child.EstimatedCost);
        });
    }
}
