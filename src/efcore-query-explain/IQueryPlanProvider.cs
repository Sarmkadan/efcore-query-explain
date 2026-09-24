using System.Data.Common;

namespace EfCore.QueryExplain;

/// <summary>
/// Runs a provider-specific EXPLAIN for a command and parses the result into a <see cref="QueryPlan"/>.
/// Implementations exist per database provider (PostgreSQL, SQL Server, SQLite).
/// </summary>
public interface IQueryPlanProvider
{
    /// <summary>
    /// Gets the EF Core provider invariant name this implementation handles
    /// (e.g. <c>Npgsql.EntityFrameworkCore.PostgreSQL</c>).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Executes EXPLAIN for <paramref name="command"/> on the same connection and returns the parsed plan.
    /// The original command must not be executed or mutated.
    /// </summary>
    /// <param name="command">The command about to be executed by EF Core.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The captured plan, or <c>null</c> if the command is not explainable (e.g. DDL).</returns>
    Task<QueryPlan?> ExplainAsync(DbCommand command, CancellationToken cancellationToken = default);
}
