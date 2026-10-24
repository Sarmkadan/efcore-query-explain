using Microsoft.EntityFrameworkCore;

namespace EfCore.QueryExplain.Tests;

public class QueryExplainExtensionsGuardTests
{
    [Fact]
    public void UseQueryExplain_NullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        DbContextOptionsBuilder? builder = null;

        // Act
        var ex = Record.Exception(() => builder!.UseQueryExplain());

        // Assert
        Assert.IsType<ArgumentNullException>(ex);
    }

    [Fact]
    public void UseQueryExplain_NPlusOneThresholdBelowTwo_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new DbContextOptionsBuilder();

        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => builder.UseQueryExplain(o => o.NPlusOneThreshold = 1));

        // Assert
        Assert.Equal("NPlusOneThreshold", ex.ParamName);
        Assert.Contains("NPlusOneThreshold", ex.Message);
    }

    [Fact]
    public void UseQueryExplain_ExplainTimeoutZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new DbContextOptionsBuilder();

        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => builder.UseQueryExplain(o => o.ExplainTimeout = TimeSpan.Zero));

        // Assert
        Assert.Equal("ExplainTimeout", ex.ParamName);
        Assert.Contains("ExplainTimeout", ex.Message);
    }

    [Fact]
    public void UseQueryExplain_ScanRowThresholdNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new DbContextOptionsBuilder();

        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => builder.UseQueryExplain(o => o.ScanRowThreshold = -1));

        // Assert
        Assert.Equal("ScanRowThreshold", ex.ParamName);
        Assert.Contains("ScanRowThreshold", ex.Message);
    }

    [Fact]
    public void UseQueryExplain_DefaultOptions_DoesNotThrow()
    {
        // Arrange
        var builder = new DbContextOptionsBuilder();

        // Act
        var ex = Record.Exception(() => builder.UseQueryExplain());

        // Assert
        Assert.Null(ex);
    }
}
