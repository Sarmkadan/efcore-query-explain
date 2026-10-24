using EfCore.QueryExplain.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EfCore.QueryExplain.Tests;

public class ServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(Action<QueryExplainOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQueryExplain(configure);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddQueryExplain_ResolveInterceptor_ReturnsInterceptor()
    {
        // Arrange
        using var provider = BuildProvider();

        // Act
        var interceptor = provider.GetRequiredService<QueryExplainInterceptor>();

        // Assert
        Assert.NotNull(interceptor);
    }

    [Fact]
    public void AddQueryExplain_ResolvePlanProviders_ReturnsThreeProviders()
    {
        // Arrange
        using var provider = BuildProvider();

        // Act
        var planProviders = provider.GetServices<IQueryPlanProvider>().ToList();

        // Assert
        Assert.Equal(3, planProviders.Count);
        Assert.Contains(planProviders, p => p is PostgresPlanProvider);
        Assert.Contains(planProviders, p => p is SqlServerPlanProvider);
        Assert.Contains(planProviders, p => p is SqlitePlanProvider);
    }

    [Fact]
    public void AddQueryExplain_ConfigureCallback_AppliesValuesToOptions()
    {
        // Arrange
        using var provider = BuildProvider(o => o.NPlusOneThreshold = 7);

        // Act
        var options = provider.GetRequiredService<IOptions<QueryExplainOptions>>().Value;

        // Assert
        Assert.Equal(7, options.NPlusOneThreshold);
    }

    [Fact]
    public void AddQueryExplain_InvalidOptions_ThrowsOptionsValidationExceptionOnAccess()
    {
        // Arrange
        using var provider = BuildProvider(o => o.NPlusOneThreshold = 1);

        // Act
        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<QueryExplainOptions>>().Value);

        // Assert
        Assert.Contains("Invalid QueryExplainOptions", exception.Failures);
    }
}
