namespace EfCore.QueryExplain.Tests;

public class UnusedIndexAnalyzerTests
{
    [Fact]
    public void Analyze_LowerOnColumn_ReturnsSingleWarningMentioningColumn()
    {
        // Arrange
        var analyzer = new UnusedIndexAnalyzer();
        var plan = CreatePlan(
            """SELECT * FROM "Customers" AS c WHERE LOWER(c."Email") = @p0""",
            SeqScan("Customers"));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.UnusedIndex, finding.Kind);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Contains("Email", finding.Message);
    }

    [Fact]
    public void Analyze_CastOnColumn_ReturnsSingleFinding()
    {
        // Arrange
        var analyzer = new UnusedIndexAnalyzer();
        var plan = CreatePlan(
            """SELECT * FROM "Customers" AS c WHERE CAST(c."CreatedAt" AS date) = @p0""",
            SeqScan("Customers"));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        Assert.Single(findings);
    }

    [Fact]
    public void Analyze_LeadingWildcardLike_ReturnsSingleFinding()
    {
        // Arrange
        var analyzer = new UnusedIndexAnalyzer();
        var plan = CreatePlan(
            """SELECT * FROM "Customers" AS c WHERE c."Email" LIKE '%' || @p0""",
            SeqScan("Customers"));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        Assert.Single(findings);
    }

    [Fact]
    public void Analyze_PlainEquality_ReturnsNothing()
    {
        // Arrange
        var analyzer = new UnusedIndexAnalyzer();
        var plan = CreatePlan(
            """SELECT * FROM "Customers" AS c WHERE c."Email" = @p0""",
            SeqScan("Customers"));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Analyze_LowerOnColumnWithIndexScan_ReturnsNothing()
    {
        // Arrange
        var analyzer = new UnusedIndexAnalyzer();
        var plan = CreatePlan(
            """SELECT * FROM "Customers" AS c WHERE LOWER(c."Email") = @p0""",
            Node("Index Scan", "Customers", "IX_Customers_Email"));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void Analyze_TwoDifferentWrappedColumns_ReturnsTwoFindings()
    {
        // Arrange
        var analyzer = new UnusedIndexAnalyzer();
        var plan = CreatePlan(
            """SELECT * FROM "Customers" AS c WHERE LOWER(c."Email") = @p0 AND CAST(c."CreatedAt" AS date) = @p1""",
            SeqScan("Customers"));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void Analyze_SameColumnWrappedTwice_ReturnsSingleFinding()
    {
        // Arrange
        var analyzer = new UnusedIndexAnalyzer();
        var plan = CreatePlan(
            """SELECT * FROM "Customers" AS c WHERE LOWER(c."Email") = @p0 OR UPPER(c."Email") = @p1""",
            SeqScan("Customers"));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions());

        // Assert
        Assert.Single(findings);
    }

    [Fact]
    public void Analyze_SuggestIndexesDisabled_ReturnsNothing()
    {
        // Arrange
        var analyzer = new UnusedIndexAnalyzer();
        var plan = CreatePlan(
            """SELECT * FROM "Customers" AS c WHERE LOWER(c."Email") = @p0""",
            SeqScan("Customers"));

        // Act
        var findings = analyzer.Analyze(plan, new QueryExplainOptions { SuggestIndexes = false });

        // Assert
        Assert.Empty(findings);
    }

    private static QueryPlan CreatePlan(string commandText, params PlanNode[] nodes) =>
        new(
            CommandText: commandText,
            Provider: "Microsoft.EntityFrameworkCore.Sqlite",
            RawPlan: string.Empty,
            Nodes: nodes,
            CapturedAt: DateTimeOffset.UtcNow);

    private static PlanNode SeqScan(string table) =>
        Node("Seq Scan", table, index: null);

    private static PlanNode Node(string operation, string table, string? index = null) =>
        new(operation, table, index, EstimatedRows: 5000, EstimatedCost: null, Children: []);
}
