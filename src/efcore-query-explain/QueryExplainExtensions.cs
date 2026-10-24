using EfCore.QueryExplain.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EfCore.QueryExplain;

/// <summary>
/// Registration helpers for <see cref="QueryExplainInterceptor"/>.
/// </summary>
public static class QueryExplainExtensions
{
    /// <summary>
    /// Adds the query-explain interceptor to <paramref name="builder"/> with default providers and analyzers.
    /// </summary>
    /// <param name="builder">The options builder being configured.</param>
    /// <param name="configure">Optional callback to adjust <see cref="QueryExplainOptions"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;(o => o
    ///     .UseNpgsql(connectionString)
    ///     .UseQueryExplain(x => x.NPlusOneThreshold = 3));
    /// </code>
    /// </example>
    public static DbContextOptionsBuilder UseQueryExplain(
        this DbContextOptionsBuilder builder,
        Action<QueryExplainOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new QueryExplainOptions();
        configure?.Invoke(options);

        var interceptor = new QueryExplainInterceptor(
            options,
            new IQueryPlanProvider[]
            {
                new PostgresPlanProvider(),
                new SqlServerPlanProvider(),
                new SqlitePlanProvider(),
            },
            new IQueryAnalyzer[]
            {
                new SequentialScanAnalyzer(),
                new UnusedIndexAnalyzer(),
            },
            new NPlusOneDetector(),
            CreateLogger(builder));

        return builder.AddInterceptors(interceptor);
    }

    /// <summary>
    /// Adds a prebuilt <see cref="QueryExplainInterceptor"/> to <paramref name="builder"/>.
    /// Use this overload when the interceptor is resolved from a dependency injection container.
    /// </summary>
    /// <param name="builder">The options builder being configured.</param>
    /// <param name="interceptor">The interceptor to register.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static DbContextOptionsBuilder UseQueryExplain(
        this DbContextOptionsBuilder builder,
        QueryExplainInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(interceptor);

        return builder.AddInterceptors(interceptor);
    }

    /// <summary>
    /// Generic overload of <see cref="UseQueryExplain(DbContextOptionsBuilder, Action{QueryExplainOptions}?)"/>.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="builder">The options builder being configured.</param>
    /// <param name="configure">Optional callback to adjust <see cref="QueryExplainOptions"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static DbContextOptionsBuilder<TContext> UseQueryExplain<TContext>(
        this DbContextOptionsBuilder<TContext> builder,
        Action<QueryExplainOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        return (DbContextOptionsBuilder<TContext>)UseQueryExplain((DbContextOptionsBuilder)builder, configure);
    }

    /// <summary>
    /// Resolves the application's <see cref="ILoggerFactory"/> when the context was registered through a service
    /// provider, falling back to a no-op factory otherwise.
    /// </summary>
    /// <param name="builder">The options builder being configured.</param>
    private static ILogger<QueryExplainInterceptor> CreateLogger(DbContextOptionsBuilder builder)
    {
        var loggerFactory = builder.Options.FindExtension<CoreOptionsExtension>()?
            .ApplicationServiceProvider?
            .GetService(typeof(ILoggerFactory)) as ILoggerFactory ?? NullLoggerFactory.Instance;

        return loggerFactory.CreateLogger<QueryExplainInterceptor>();
    }
}
