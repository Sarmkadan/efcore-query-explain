namespace EfCore.QueryExplain.Tests;

public class AnalyzerGuardTests
{
    [Fact]
    public void Record_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var detector = new NPlusOneDetector();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(
            () => detector.Record(Guid.NewGuid(), "SELECT 1", null!));

        // Assert
        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public void Record_NullCommandText_ThrowsArgumentException()
    {
        // Arrange
        var detector = new NPlusOneDetector();

        // Act
        var ex = Assert.ThrowsAny<ArgumentException>(
            () => detector.Record(Guid.NewGuid(), null!, new QueryExplainOptions()));

        // Assert
        Assert.Equal("commandText", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Record_WhitespaceCommandText_ThrowsArgumentException(string commandText)
    {
        // Arrange
        var detector = new NPlusOneDetector();

        // Act
        var ex = Assert.Throws<ArgumentException>(
            () => detector.Record(Guid.NewGuid(), commandText, new QueryExplainOptions()));

        // Assert
        Assert.Equal("commandText", ex.ParamName);
    }

    [Fact]
    public void Record_EmptyContextId_ThrowsArgumentException()
    {
        // Arrange
        var detector = new NPlusOneDetector();

        // Act
        var ex = Assert.Throws<ArgumentException>(
            () => detector.Record(Guid.Empty, "SELECT 1", new QueryExplainOptions()));

        // Assert
        Assert.Equal("contextId", ex.ParamName);
        Assert.Contains("contextId must not be empty", ex.Message);
    }

    [Fact]
    public void SequentialScanAnalyze_NullPlan_ThrowsArgumentNullException()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(
            () => analyzer.Analyze(null!, new QueryExplainOptions()));

        // Assert
        Assert.Equal("plan", ex.ParamName);
    }

    [Fact]
    public void SequentialScanAnalyze_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var analyzer = new SequentialScanAnalyzer();
        var plan = CreatePlan("SELECT 1");

        // Act
        var ex = Assert.Throws<ArgumentNullException>(
            () => analyzer.Analyze(plan, null!));

        // Assert
        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public void UnusedIndexAnalyze_NullPlan_ThrowsArgumentNullException()
    {
        // Arrange
        var analyzer = new UnusedIndexAnalyzer();

        // Act
        var ex = Assert.Throws<ArgumentNullException>(
            () => analyzer.Analyze(null!, new QueryExplainOptions()));

        // Assert
        Assert.Equal("plan", ex.ParamName);
    }

    [Fact]
    public void UnusedIndexAnalyze_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var analyzer = new UnusedIndexAnalyzer();
        var plan = CreatePlan("SELECT 1");

        // Act
        var ex = Assert.Throws<ArgumentNullException>(
            () => analyzer.Analyze(plan, null!));

        // Assert
        Assert.Equal("options", ex.ParamName);
    }

    private static QueryPlan CreatePlan(string commandText) =>
        new(commandText, "Sqlite", string.Empty, [], DateTimeOffset.UtcNow);
}
