using System.Data;
using System.Data.Common;

namespace EfCore.QueryExplain.Providers;

/// <summary>
/// Captures PostgreSQL plans with <c>EXPLAIN (FORMAT JSON)</c> on the same connection as the intercepted command.
/// </summary>
/// <remarks>
/// The original command is never executed or modified; only a cloned <c>EXPLAIN</c> command is run.
/// </remarks>
public sealed class PostgresPlanProvider : IQueryPlanProvider
{
    /// <inheritdoc />
    public string ProviderName => "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <inheritdoc />
    public async Task<QueryPlan?> ExplainAsync(DbCommand command, CancellationToken cancellationToken = default)
    {
        var sql = command.CommandText.Trim();
        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !sql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var connection = command.Connection!;
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var explain = connection.CreateCommand();
            explain.Transaction = command.Transaction;
            explain.CommandText = "EXPLAIN (FORMAT JSON) " + command.CommandText;

            foreach (DbParameter source in command.Parameters)
            {
                var copy = explain.CreateParameter();
                copy.ParameterName = source.ParameterName;
                copy.Value = source.Value;
                copy.DbType = source.DbType;
                copy.Direction = source.Direction;
                copy.Size = source.Size;
                explain.Parameters.Add(copy);
            }

            var raw = (await explain.ExecuteScalarAsync(cancellationToken))?.ToString();
            if (raw is null)
            {
                return null;
            }

            var nodes = PostgresPlanParser.Parse(raw);

            return new QueryPlan(command.CommandText, "Npgsql", raw, nodes, DateTimeOffset.UtcNow);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
