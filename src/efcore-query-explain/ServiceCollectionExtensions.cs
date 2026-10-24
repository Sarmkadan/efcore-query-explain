using EfCore.QueryExplain.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EfCore.QueryExplain;

/// <summary>
/// Dependency injection registration for the query-explain interceptor.
/// </summary>
public static class QueryExplainServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="QueryExplainOptions"/>, the default plan providers and analyzers, and
    /// <see cref="QueryExplainInterceptor"/>. Options are validated when they are first resolved.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Optional callback to adjust <see cref="QueryExplainOptions"/>.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="OptionsValidationException">
    /// Thrown when <see cref="IOptions{TOptions}.Value"/> is read and the options are invalid.
    /// </exception>
    public static IServiceCollection AddQueryExplain(
        this IServiceCollection services,
        Action<QueryExplainOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services.AddQueryExplainCore();
    }

    /// <summary>
    /// Binds <see cref="QueryExplainOptions"/> from <paramref name="section"/> and registers the same services
    /// as <see cref="AddQueryExplain(IServiceCollection, Action{QueryExplainOptions}?)"/>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="section">The configuration section that holds the <see cref="QueryExplainOptions"/> values.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="OptionsValidationException">
    /// Thrown when <see cref="IOptions{TOptions}.Value"/> is read and the bound options are invalid.
    /// </exception>
    public static IServiceCollection AddQueryExplain(
        this IServiceCollection services,
        IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        services.Configure<QueryExplainOptions>(section);

        return services.AddQueryExplainCore();
    }

    /// <summary>
    /// Adds options validation and the plan providers, analyzers, N+1 detector and interceptor.
    /// Existing registrations are kept.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    private static IServiceCollection AddQueryExplainCore(this IServiceCollection services)
    {
        services
            .AddOptions<QueryExplainOptions>()
            .Validate(o => QueryExplainOptionsValidator.GetErrors(o).Count == 0, "Invalid QueryExplainOptions");

        services.TryAddEnumerable(new[]
        {
            ServiceDescriptor.Singleton<IQueryPlanProvider, PostgresPlanProvider>(),
            ServiceDescriptor.Singleton<IQueryPlanProvider, SqlServerPlanProvider>(),
            ServiceDescriptor.Singleton<IQueryPlanProvider, SqlitePlanProvider>(),
        });

        services.TryAddEnumerable(new[]
        {
            ServiceDescriptor.Singleton<IQueryAnalyzer, SequentialScanAnalyzer>(),
            ServiceDescriptor.Singleton<IQueryAnalyzer, UnusedIndexAnalyzer>(),
        });

        services.TryAddSingleton<INPlusOneDetector, NPlusOneDetector>();

        services.TryAddSingleton<QueryExplainInterceptor>(sp => new QueryExplainInterceptor(
            sp.GetRequiredService<IOptions<QueryExplainOptions>>().Value,
            sp.GetServices<IQueryPlanProvider>(),
            sp.GetServices<IQueryAnalyzer>(),
            sp.GetRequiredService<INPlusOneDetector>(),
            sp.GetRequiredService<ILogger<QueryExplainInterceptor>>()));

        return services;
    }
}
