using Microsoft.EntityFrameworkCore;

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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
