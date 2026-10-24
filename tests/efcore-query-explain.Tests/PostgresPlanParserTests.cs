using EfCore.QueryExplain.Providers;

namespace EfCore.QueryExplain.Tests;

public class PostgresPlanParserTests
{
    private const string NestedLoopPlan = """
        [
          {
            "Plan": {
              "Node Type": "Nested Loop",
              "Parallel Aware": false,
              "Join Type": "Inner",
              "Startup Cost": 0.29,
              "Total Cost": 123.45,
              "Plan Rows": 10,
              "Plan Width": 96,
              "Plans": [
                {
                  "Node Type": "Seq Scan",
                  "Parent Relationship": "Outer",
                  "Parallel Aware": false,
                  "Async Capable": false,
                  "Relation Name": "Orders",
                  "Schema": "public",
                  "Alias": "o",
                  "Startup Cost": 0.00,
                  "Total Cost": 2500.0,
                  "Plan Rows": 120000,
                  "Plan Width": 48
                },
                {
                  "Node Type": "Index Scan",
                  "Parent Relationship": "Inner",
                  "Parallel Aware": false,
                  "Async Capable": false,
                  "Scan Direction": "Forward",
                  "Index Name": "PK_Customers",
                  "Relation Name": "Customers",
                  "Schema": "public",
                  "Alias": "c",
                  "Startup Cost": 0.29,
                  "Total Cost": 8.31,
                  "Plan Rows": 1,
                  "Plan Width": 48,
                  "Index Cond": "(\"CustomerId\" = o.\"CustomerId\")"
                }
              ]
            }
          }
        ]
        """;

    private const string AggregatePlan = """
        [
          {
            "Plan": {
              "Node Type": "Aggregate",
              "Strategy": "Plain",
              "Partial Mode": "Simple",
              "Startup Cost": 1500.00,
              "Total Cost": 1520.00,
              "Plan Rows": 1,
              "Plan Width": 8,
              "Plans": [
                {
                  "Node Type": "Hash Join",
                  "Parent Relationship": "Outer",
                  "Join Type": "Inner",
                  "Startup Cost": 1000.00,
                  "Total Cost": 1400.00,
                  "Plan Rows": 5000,
                  "Plan Width": 0,
                  "Hash Cond": "(o.\"CustomerId\" = c.\"CustomerId\")",
                  "Plans": [
                    {
                      "Node Type": "Seq Scan",
                      "Parent Relationship": "Outer",
                      "Relation Name": "Orders",
                      "Schema": "public",
                      "Alias": "o",
                      "Startup Cost": 0.00,
                      "Total Cost": 900.00,
                      "Plan Rows": 120000,
                      "Plan Width": 4
                    },
                    {
                      "Node Type": "Hash",
                      "Parent Relationship": "Inner",
                      "Startup Cost": 25.00,
                      "Total Cost": 25.00,
                      "Plan Rows": 1000,
                      "Plan Width": 4,
                      "Plans": [
                        {
                          "Node Type": "Index Only Scan",
                          "Parent Relationship": "Outer",
                          "Index Name": "PK_Customers",
                          "Relation Name": "Customers",
                          "Schema": "public",
                          "Alias": "c",
                          "Startup Cost": 0.29,
                          "Total Cost": 25.00,
                          "Plan Rows": 1000,
                          "Plan Width": 4
                        }
                      ]
                    }
                  ]
                }
              ]
            }
          }
        ]
        """;

    private const string PlanWithoutPlanKey = """
        [
          {
            "Execution Time": 0.042,
            "Planning Time": 0.118
          }
        ]
        """;

    [Fact]
    public void Parse_ValidPlan_ReturnsSingleRootNode()
    {
        // Arrange
        var json = NestedLoopPlan;

        // Act
        var nodes = PostgresPlanParser.Parse(json);

        // Assert
        Assert.Single(nodes);
    }

    [Fact]
    public void Parse_ValidPlan_MapsRootNode()
    {
        // Arrange
        var json = NestedLoopPlan;

        // Act
        var root = PostgresPlanParser.Parse(json).Single();

        // Assert
        Assert.Equal("Nested Loop", root.Operation);
        Assert.Null(root.Table);
        Assert.Null(root.Index);
        Assert.Equal(10L, root.EstimatedRows);
        Assert.Equal(123.45, root.EstimatedCost);
    }

    [Fact]
    public void Parse_ValidPlan_RootHasTwoChildren()
    {
        // Arrange
        var json = NestedLoopPlan;

        // Act
        var root = PostgresPlanParser.Parse(json).Single();

        // Assert
        Assert.Equal(2, root.Children.Count);
    }

    [Fact]
    public void Parse_SeqScanChild_MapsOperationTableAndEstimates()
    {
        // Arrange
        var json = NestedLoopPlan;

        // Act
        var scan = PostgresPlanParser.Parse(json).Single().Children[0];

        // Assert
        Assert.Equal("Seq Scan", scan.Operation);
        Assert.Equal("Orders", scan.Table);
        Assert.Null(scan.Index);
        Assert.Equal(120000L, scan.EstimatedRows);
        Assert.Equal(2500.0, scan.EstimatedCost);
    }

    [Fact]
    public void Parse_IndexScanChild_MapsOperationTableIndexAndEstimates()
    {
        // Arrange
        var json = NestedLoopPlan;

        // Act
        var scan = PostgresPlanParser.Parse(json).Single().Children[1];

        // Assert
        Assert.Equal("Index Scan", scan.Operation);
        Assert.Equal("Customers", scan.Table);
        Assert.Equal("PK_Customers", scan.Index);
        Assert.Equal(1L, scan.EstimatedRows);
        Assert.Equal(8.31, scan.EstimatedCost);
    }

    [Fact]
    public void Parse_NodeWithoutRelationName_HasNullTable()
    {
        // Arrange
        var json = NestedLoopPlan;

        // Act
        var root = PostgresPlanParser.Parse(json).Single();

        // Assert
        Assert.Null(root.Table);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsEmptyList()
    {
        // Arrange
        var json = "[{ \"Plan\": { \"Node Type\": ";

        // Act
        var nodes = PostgresPlanParser.Parse(json);

        // Assert
        Assert.Empty(nodes);
    }

    [Fact]
    public void Parse_ArrayWithoutPlanKey_ReturnsEmptyList()
    {
        // Arrange
        var json = PlanWithoutPlanKey;

        // Act
        var nodes = PostgresPlanParser.Parse(json);

        // Assert
        Assert.Empty(nodes);
    }

    [Fact]
    public void Parse_ThreeLevelPlan_PreservesNesting()
    {
        // Arrange
        var json = AggregatePlan;

        // Act
        var root = PostgresPlanParser.Parse(json).Single();

        // Assert
        Assert.Equal("Aggregate", root.Operation);
        Assert.Single(root.Children);

        var hashJoin = root.Children[0];
        Assert.Equal("Hash Join", hashJoin.Operation);
        Assert.Equal(2, hashJoin.Children.Count);

        var ordersScan = hashJoin.Children[0];
        Assert.Equal("Seq Scan", ordersScan.Operation);
        Assert.Equal("Orders", ordersScan.Table);
        Assert.Empty(ordersScan.Children);

        var hash = hashJoin.Children[1];
        Assert.Equal("Hash", hash.Operation);
        Assert.Single(hash.Children);

        var customersScan = hash.Children[0];
        Assert.Equal("Index Only Scan", customersScan.Operation);
        Assert.Equal("Customers", customersScan.Table);
        Assert.Equal("PK_Customers", customersScan.Index);
    }
}
