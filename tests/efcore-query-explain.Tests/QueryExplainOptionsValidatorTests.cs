namespace EfCore.QueryExplain.Tests;

public class QueryExplainOptionsValidatorTests
{
    [Fact]
    public void GetErrors_DefaultOptions_ReturnsNoErrors()
    {
        // Arrange
        var options = new QueryExplainOptions();

        // Act
        var errors = QueryExplainOptionsValidator.GetErrors(options);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void GetErrors_NPlusOneThresholdOne_ReturnsExactlyOneError()
    {
        // Arrange
        var options = new QueryExplainOptions { NPlusOneThreshold = 1 };

        // Act
        var errors = QueryExplainOptionsValidator.GetErrors(options);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal("NPlusOneThreshold must be at least 2", error);
    }

    [Fact]
    public void GetErrors_AllValuesInvalid_ReturnsOneErrorPerProperty()
    {
        // Arrange
        var options = new QueryExplainOptions
        {
            NPlusOneThreshold = 0,
            ScanRowThreshold = -1,
            ExplainTimeout = TimeSpan.Zero,
        };

        // Act
        var errors = QueryExplainOptionsValidator.GetErrors(options);

        // Assert
        Assert.Equal(
            new[]
            {
                "NPlusOneThreshold must be at least 2",
                "ScanRowThreshold must not be negative",
                "ExplainTimeout must be greater than zero",
            },
            errors);
    }

    [Fact]
    public void GetErrors_ExplainTimeoutAboveSixtySeconds_ReturnsTimeoutError()
    {
        // Arrange
        var options = new QueryExplainOptions { ExplainTimeout = TimeSpan.FromSeconds(61) };

        // Act
        var errors = QueryExplainOptionsValidator.GetErrors(options);

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal("ExplainTimeout above 60s is not sensible for development tooling", error);
    }

    [Fact]
    public void GetErrors_ExplainTimeoutExactlySixtySeconds_ReturnsNoErrors()
    {
        // Arrange
        var options = new QueryExplainOptions { ExplainTimeout = TimeSpan.FromSeconds(60) };

        // Act
        var errors = QueryExplainOptionsValidator.GetErrors(options);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void GetErrors_NullOptions_ThrowsArgumentNullException()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(
            () => QueryExplainOptionsValidator.GetErrors(null!));

        // Assert
        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public void Validate_MultipleInvalidValues_ThrowsArgumentExceptionWithAllMessages()
    {
        // Arrange
        var options = new QueryExplainOptions
        {
            NPlusOneThreshold = 1,
            ScanRowThreshold = -5,
            ExplainTimeout = TimeSpan.FromMinutes(5),
        };

        // Act
        var ex = Assert.Throws<ArgumentException>(
            () => QueryExplainOptionsValidator.Validate(options));

        // Assert
        Assert.Contains("NPlusOneThreshold must be at least 2", ex.Message);
        Assert.Contains("ScanRowThreshold must not be negative", ex.Message);
        Assert.Contains("ExplainTimeout above 60s is not sensible for development tooling", ex.Message);
        Assert.Contains("; ", ex.Message);
    }

    [Fact]
    public void Validate_SingleInvalidValue_MessageHasNoSeparator()
    {
        // Arrange
        var options = new QueryExplainOptions { ScanRowThreshold = -1 };

        // Act
        var ex = Assert.Throws<ArgumentException>(
            () => QueryExplainOptionsValidator.Validate(options));

        // Assert
        Assert.StartsWith("ScanRowThreshold must not be negative", ex.Message);
        Assert.DoesNotContain("; ", ex.Message);
    }

    [Fact]
    public void Validate_ValidOptions_DoesNotThrow()
    {
        // Arrange
        var options = new QueryExplainOptions();

        // Act
        var ex = Record.Exception(() => QueryExplainOptionsValidator.Validate(options));

        // Assert
        Assert.Null(ex);
    }

    [Fact]
    public void Clone_DefaultOptions_ReturnsEqualButDistinctInstance()
    {
        // Arrange
        var options = new QueryExplainOptions
        {
            CapturePlans = false,
            SuggestIndexes = false,
            DetectNPlusOne = false,
            NPlusOneThreshold = 3,
            ScanRowThreshold = 250,
            ExplainTimeout = TimeSpan.FromSeconds(10),
            Enabled = false,
        };

        // Act
        var clone = options.Clone();

        // Assert
        Assert.NotSame(options, clone);
        Assert.False(clone.CapturePlans);
        Assert.False(clone.SuggestIndexes);
        Assert.False(clone.DetectNPlusOne);
        Assert.Equal(3, clone.NPlusOneThreshold);
        Assert.Equal(250, clone.ScanRowThreshold);
        Assert.Equal(TimeSpan.FromSeconds(10), clone.ExplainTimeout);
        Assert.False(clone.Enabled);
    }

    [Fact]
    public void Clone_ChangedCopy_LeavesOriginalUnchanged()
    {
        // Arrange
        var options = new QueryExplainOptions();
        var clone = options.Clone();

        // Act
        clone.NPlusOneThreshold = 9;

        // Assert
        Assert.Equal(5, options.NPlusOneThreshold);
    }

    [Fact]
    public void SectionName_Constant_IsQueryExplain()
    {
        // Act & Assert
        Assert.Equal("QueryExplain", QueryExplainOptions.SectionName);
    }
}
