using EfCore.QueryExplain.Providers;
using Microsoft.Data.Sqlite;

namespace EfCore.QueryExplain.Tests;

public class ProviderGuardTests
{
    [Fact]
    public async Task SqlitePlanProvider_ExplainAsyncNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new SqlitePlanProvider();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => provider.ExplainAsync(null!));
    }

    [Fact]
    public async Task PostgresPlanProvider_ExplainAsyncNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new PostgresPlanProvider();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => provider.ExplainAsync(null!));
    }

    [Fact]
    public async Task SqlServerPlanProvider_ExplainAsyncNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new SqlServerPlanProvider();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => provider.ExplainAsync(null!));
    }

    [Fact]
    public async Task SqlitePlanProvider_ExplainAsyncCommandWithoutConnection_ThrowsInvalidOperationException()
    {
        // Arrange
        var provider = new SqlitePlanProvider();
        using var command = new SqliteCommand("SELECT 1");

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ExplainAsync(command));

        // Assert
        Assert.Equal("Command has no connection", ex.Message);
    }

    [Fact]
    public async Task PostgresPlanProvider_ExplainAsyncCommandWithoutConnection_ThrowsInvalidOperationException()
    {
        // Arrange
        var provider = new PostgresPlanProvider();
        using var command = new SqliteCommand("SELECT 1");

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ExplainAsync(command));

        // Assert
        Assert.Equal("Command has no connection", ex.Message);
    }

    [Fact]
    public async Task SqlServerPlanProvider_ExplainAsyncCommandWithoutConnection_ThrowsInvalidOperationException()
    {
        // Arrange
        var provider = new SqlServerPlanProvider();
        using var command = new SqliteCommand("SELECT 1");

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ExplainAsync(command));

        // Assert
        Assert.Equal("Command has no connection", ex.Message);
    }

    [Fact]
    public void SqlitePlanParser_NullRawPlan_ThrowsArgumentNullException()
    {
        // Arrange
        string? rawPlan = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SqlitePlanParser.Parse(rawPlan!));
    }

    [Fact]
    public void SqlitePlanParser_EmptyRawPlan_ReturnsEmptyList()
    {
        // Act
        var nodes = SqlitePlanParser.Parse(string.Empty);

        // Assert
        Assert.Empty(nodes);
    }

    [Fact]
    public void PostgresPlanParser_NullJson_ThrowsArgumentNullException()
    {
        // Arrange
        string? json = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => PostgresPlanParser.Parse(json!));
    }

    [Fact]
    public void SqlServerPlanParser_NullXml_ThrowsArgumentNullException()
    {
        // Arrange
        string? xml = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SqlServerPlanParser.Parse(xml!));
    }
}
