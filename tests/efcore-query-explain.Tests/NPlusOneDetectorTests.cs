namespace EfCore.QueryExplain.Tests;

public class NPlusOneDetectorTests
{
    [Fact]
    public void Record_BelowThreshold_ReturnsNull()
    {
        // Arrange
        var detector = new NPlusOneDetector();
        var options = new QueryExplainOptions { NPlusOneThreshold = 3 };
        var contextId = Guid.NewGuid();

        // Act
        var first = detector.Record(contextId, "SELECT 1", options);
        var second = detector.Record(contextId, "SELECT 1", options);

        // Assert
        Assert.Null(first);
        Assert.Null(second);
    }

    [Fact]
    public void Record_OnThresholdCall_ReturnsNPlusOneWarning()
    {
        // Arrange
        var detector = new NPlusOneDetector();
        var options = new QueryExplainOptions { NPlusOneThreshold = 3 };
        var contextId = Guid.NewGuid();
        detector.Record(contextId, "SELECT 1", options);
        detector.Record(contextId, "SELECT 1", options);

        // Act
        var finding = detector.Record(contextId, "SELECT 1", options);

        // Assert
        Assert.NotNull(finding);
        Assert.Equal(FindingKind.NPlusOne, finding.Kind);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal("SELECT 1", finding.CommandText);
    }

    [Fact]
    public void Record_AfterThreshold_ReturnsNullOnFourthAndFifthCall()
    {
        // Arrange
        var detector = new NPlusOneDetector();
        var options = new QueryExplainOptions { NPlusOneThreshold = 3 };
        var contextId = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            detector.Record(contextId, "SELECT 1", options);
        }

        // Act
        var fourth = detector.Record(contextId, "SELECT 1", options);
        var fifth = detector.Record(contextId, "SELECT 1", options);

        // Assert
        Assert.Null(fourth);
        Assert.Null(fifth);
    }

    [Fact]
    public void Record_DifferentContextIds_TrackCountsIndependently()
    {
        // Arrange
        var detector = new NPlusOneDetector();
        var options = new QueryExplainOptions { NPlusOneThreshold = 3 };
        var contextA = Guid.NewGuid();
        var contextB = Guid.NewGuid();
        detector.Record(contextA, "SELECT 1", options);
        detector.Record(contextA, "SELECT 1", options);
        detector.Record(contextB, "SELECT 1", options);
        detector.Record(contextB, "SELECT 1", options);

        // Act
        var contextAThird = detector.Record(contextA, "SELECT 1", options);
        var contextBThird = detector.Record(contextB, "SELECT 1", options);

        // Assert
        Assert.NotNull(contextAThird);
        Assert.NotNull(contextBThird);
    }

    [Fact]
    public void Record_WhitespaceDifferences_TreatedAsSameShape()
    {
        // Arrange
        var detector = new NPlusOneDetector();
        var options = new QueryExplainOptions { NPlusOneThreshold = 2 };
        var contextId = Guid.NewGuid();
        detector.Record(contextId, "SELECT  1", options);

        // Act
        var finding = detector.Record(contextId, "SELECT 1\n", options);

        // Assert
        Assert.NotNull(finding);
    }

    [Fact]
    public void Reset_AfterThresholdReached_ThresholdFiresAgain()
    {
        // Arrange
        var detector = new NPlusOneDetector();
        var options = new QueryExplainOptions { NPlusOneThreshold = 3 };
        var contextId = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            detector.Record(contextId, "SELECT 1", options);
        }

        // Act
        detector.Reset(contextId);
        var beforeThreshold = new[]
        {
            detector.Record(contextId, "SELECT 1", options),
            detector.Record(contextId, "SELECT 1", options),
        };
        var finding = detector.Record(contextId, "SELECT 1", options);

        // Assert
        Assert.All(beforeThreshold, Assert.Null);
        Assert.NotNull(finding);
    }

    [Fact]
    public void Record_DetectNPlusOneDisabled_ReturnsNullAlways()
    {
        // Arrange
        var detector = new NPlusOneDetector();
        var options = new QueryExplainOptions { NPlusOneThreshold = 3, DetectNPlusOne = false };
        var contextId = Guid.NewGuid();

        // Act
        var findings = Enumerable.Range(0, 10)
            .Select(_ => detector.Record(contextId, "SELECT 1", options))
            .ToList();

        // Assert
        Assert.All(findings, Assert.Null);
    }

    [Fact]
    public void Record_ParallelCallsSameShape_ProducesExactlyOneFinding()
    {
        // Arrange
        var detector = new NPlusOneDetector();
        var options = new QueryExplainOptions { NPlusOneThreshold = 5 };
        var contextId = Guid.NewGuid();
        var findingCount = 0;

        // Act
        Parallel.For(
            0,
            20,
            new ParallelOptions { MaxDegreeOfParallelism = 20 },
            _ =>
            {
                if (detector.Record(contextId, "SELECT 1", options) is not null)
                {
                    Interlocked.Increment(ref findingCount);
                }
            });

        // Assert
        Assert.Equal(1, findingCount);
    }
}
