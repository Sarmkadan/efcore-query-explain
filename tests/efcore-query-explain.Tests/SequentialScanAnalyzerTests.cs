namespace EfCore.QueryExplain.Tests;

public class SequentialScanAnalyzerTests
{
    private const string Sql = """SELECT * FROM "Orders" AS o WHERE o."CustomerId" = @p0""";

    [Fact]
    public void Analyze_RowsAboveThreshold_ReturnsSingleWarning()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var plan = CreatePlan(SeqScan("Orders", rows: 5000));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions { ScanRowThreshold = 1000 });

        // Assert
        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.MissingIndex, finding.Kind);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void Analyze_RowsAtTenTimesThreshold_ReturnsError()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var plan = CreatePlan(SeqScan("Orders", rows: 50000));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions { ScanRowThreshold = 1000 });

        // Assert
        var finding = Assert.Single(findings);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
    }

    [Fact]
    public void Analyze_RowsBelowThreshold_ReturnsNothing()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var plan = CreatePlan(SeqScan("Orders", rows: 500));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions { ScanRowThreshold = 1000 });

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Analyze_ScanTwoLevelsDeepInNestedLoop_ReturnsFinding()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var nested = Node(
            "Nested Loop",
            table: null,
            index: null,
            rows: null,
            children: [
                Node(
                    "Hash Join",
                    table: null,
                    index: null,
                    rows: null,
                    children: [SeqScan("Orders", rows: 5000)]),
            ]);
        var plan = CreatePlan(nested);

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        var finding = Assert.Single(findings);
        Assert.Contains("\"Orders\"", finding.Message);
    }

    [Fact]
    public void Analyze_IndexScan_ReturnsNothing()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var plan = CreatePlan(Node(
            "Index Scan",
            table: "Orders",
            index: "IX_Orders_CustomerId",
            rows: 5000,
            children: []));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Analyze_ScanWithoutTable_ReturnsNothing()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var plan = CreatePlan(Node(
            "Seq Scan",
            table: null,
            index: null,
            rows: 5000,
            children: []));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Analyze_SuggestIndexesDisabled_ReturnsNothing()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var plan = CreatePlan(SeqScan("Orders", rows: 50000));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions { SuggestIndexes = false });

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Analyze_SqlServerClusteredIndexScan_ReturnsFinding()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var plan = CreatePlan(Node(
            "Clustered Index Scan",
            table: "Orders",
            index: "PK_Orders",
            rows: 5000,
            children: []));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        Assert.Single(findings);
    }

    [Fact]
    public void Analyze_SqliteScanOperation_ReturnsFinding()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var plan = CreatePlan(Node(
            "SCAN",
            table: "Orders",
            index: null,
            rows: 5000,
            children: []));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        Assert.Single(findings);
    }

    [Fact]
    public void Analyze_Finding_CommandTextEqualsPlanCommandText()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var plan = CreatePlan(SeqScan("Orders", rows: 5000));

        // Act
        var finding = Assert.Single(analyzer.Analyze(plan, new QueryExplainOptions()));

        // Assert
        Assert.Equal(plan.CommandText, finding.CommandText);
    }

    [Fact]
    public void Analyze_WhereClauseColumn_SuggestedSqlContainsColumn()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var plan = CreatePlan(SeqScan("Orders", rows: 5000));

        // Act
        var finding = Assert.Single(analyzer.Analyze(plan, new QueryExplainOptions()));

        // Assert
        Assert.NotNull(finding.SuggestedSql);
        Assert.Contains("CustomerId", finding.SuggestedSql);
    }

    private static QueryPlan CreatePlan(params PlanNode[] nodes) =>
        new(
            CommandText: Sql,
            Provider: "Microsoft.EntityFrameworkCore.Sqlite",
            RawPlan: string.Empty,
            Nodes: nodes,
            CapturedAt: DateTimeOffset.UtcNow);

    private static PlanNode SeqScan(string table, long rows) =>
        Node("Seq Scan", table, index: null, rows, children: []);

    private static PlanNode Node(
        string operation,
        string? table,
        string? index,
        long? rows,
        IReadOnlyList<PlanNode> children) =>
        new(operation, table, index, rows, EstimatedCost: null, children);
}
