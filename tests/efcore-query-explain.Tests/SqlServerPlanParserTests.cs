using EfCore.QueryExplain.Providers;

namespace EfCore.QueryExplain.Tests;

public class SqlServerPlanParserTests
{
    private const string NestedLoopsPlan = """
        <?xml version="1.0" encoding="utf-8"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan" Version="1.5" Build="16.0.1000.6">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementText="SELECT ..." StatementType="SELECT">
                  <QueryPlan>
                    <RelOp PhysicalOp="Nested Loops" LogicalOp="Inner Join" EstimateRows="10" EstimatedTotalSubtreeCost="0.5">
                      <NestedLoops Optimized="false">
                        <RelOp PhysicalOp="Clustered Index Scan" LogicalOp="Clustered Index Scan" EstimateRows="120000">
                          <IndexScan Ordered="true" ScanDirection="FORWARD">
                            <Object Table="[Orders]" Index="[PK_Orders]" />
                          </IndexScan>
                        </RelOp>
                        <RelOp PhysicalOp="Index Seek" LogicalOp="Index Seek" EstimateRows="1">
                          <IndexScan Ordered="true" ScanDirection="FORWARD">
                            <Object Table="[Customers]" Index="[IX_Customers_Email]" />
                          </IndexScan>
                        </RelOp>
                      </NestedLoops>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void Parse_ValidShowplan_ReturnsSingleRootNode()
    {
        // Act
        var nodes = SqlServerPlanParser.Parse(NestedLoopsPlan);

        // Assert
        Assert.Single(nodes);
    }

    [Fact]
    public void Parse_ValidShowplan_RootOperationIsNestedLoops()
    {
        // Act
        var root = SqlServerPlanParser.Parse(NestedLoopsPlan).Single();

        // Assert
        Assert.Equal("Nested Loops", root.Operation);
    }

    [Fact]
    public void Parse_ValidShowplan_RootEstimatesAreParsed()
    {
        // Act
        var root = SqlServerPlanParser.Parse(NestedLoopsPlan).Single();

        // Assert
        Assert.Equal(10L, root.EstimatedRows);
        Assert.Equal(0.5, root.EstimatedCost);
    }

    [Fact]
    public void Parse_ValidShowplan_RootHasNoTableOrIndex()
    {
        // Act
        var root = SqlServerPlanParser.Parse(NestedLoopsPlan).Single();

        // Assert
        Assert.Null(root.Table);
        Assert.Null(root.Index);
    }

    [Fact]
    public void Parse_ValidShowplan_RootHasTwoChildren()
    {
        // Act
        var root = SqlServerPlanParser.Parse(NestedLoopsPlan).Single();

        // Assert
        Assert.Equal(2, root.Children.Count);
    }

    [Fact]
    public void Parse_ValidShowplan_FirstChildIsClusteredIndexScan()
    {
        // Act
        var child = SqlServerPlanParser.Parse(NestedLoopsPlan).Single().Children[0];

        // Assert
        Assert.Equal("Clustered Index Scan", child.Operation);
    }

    [Fact]
    public void Parse_ValidShowplan_FirstChildStripsBracketsFromTableAndIndex()
    {
        // Act
        var child = SqlServerPlanParser.Parse(NestedLoopsPlan).Single().Children[0];

        // Assert
        Assert.Equal("Orders", child.Table);
        Assert.Equal("PK_Orders", child.Index);
    }

    [Fact]
    public void Parse_ValidShowplan_FirstChildEstimatedRowsAreLong()
    {
        // Act
        var child = SqlServerPlanParser.Parse(NestedLoopsPlan).Single().Children[0];

        // Assert
        Assert.Equal(120000L, child.EstimatedRows);
    }

    [Fact]
    public void Parse_ValidShowplan_SecondChildIsIndexSeek()
    {
        // Act
        var child = SqlServerPlanParser.Parse(NestedLoopsPlan).Single().Children[1];

        // Assert
        Assert.Equal("Index Seek", child.Operation);
    }

    [Fact]
    public void Parse_ValidShowplan_SecondChildStripsBracketsFromTableAndIndex()
    {
        // Act
        var child = SqlServerPlanParser.Parse(NestedLoopsPlan).Single().Children[1];

        // Assert
        Assert.Equal("Customers", child.Table);
        Assert.Equal("IX_Customers_Email", child.Index);
    }

    [Fact]
    public void Parse_ValidShowplan_SecondChildEstimatedRowsAreLong()
    {
        // Act
        var child = SqlServerPlanParser.Parse(NestedLoopsPlan).Single().Children[1];

        // Assert
        Assert.Equal(1L, child.EstimatedRows);
    }

    [Fact]
    public void Parse_ValidShowplan_LeafNodesHaveNoChildren()
    {
        // Act
        var children = SqlServerPlanParser.Parse(NestedLoopsPlan).Single().Children;

        // Assert
        Assert.All(children, child => Assert.Empty(child.Children));
    }

    [Fact]
    public void Parse_MalformedXml_ReturnsEmptyList()
    {
        // Arrange
        const string malformed = "<ShowPlanXML xmlns=\"http://schemas.microsoft.com/sqlserver/2004/07/showplan\"><QueryPlan>";

        // Act
        var nodes = SqlServerPlanParser.Parse(malformed);

        // Assert
        Assert.Empty(nodes);
    }

    [Fact]
    public void Parse_DifferentNamespace_ReturnsEmptyList()
    {
        // Arrange
        const string otherNamespace = """
            <ShowPlanXML xmlns="http://example.com/other">
              <QueryPlan>
                <RelOp PhysicalOp="Nested Loops" EstimateRows="10" EstimatedTotalSubtreeCost="0.5" />
              </QueryPlan>
            </ShowPlanXML>
            """;

        // Act
        var nodes = SqlServerPlanParser.Parse(otherNamespace);

        // Assert
        Assert.Empty(nodes);
    }
}
