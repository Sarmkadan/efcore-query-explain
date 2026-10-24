using System.Data;
using System.Data.Common;

namespace EfCore.QueryExplain.Providers;

/// <summary>
/// Captures SQLite plans with <c>EXPLAIN QUERY PLAN</c> on the same connection as the intercepted command.
/// </summary>
/// <remarks>
/// The original command is never executed or modified; only a cloned <c>EXPLAIN QUERY PLAN</c> command is run.
/// </remarks>
public sealed class SqlitePlanProvider : IQueryPlanProvider
{
    /// <inheritdoc />
    public string ProviderName => "Microsoft.EntityFrameworkCore.Sqlite";

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
            await using var explain = CommandCloner.CloneWithText(command, "EXPLAIN QUERY PLAN " + command.CommandText);
            await using var reader = await explain.ExecuteReaderAsync(cancellationToken);

            // Columns: id, parent, notused, detail. Rows are emitted as "id|parent|detail".
            var lines = new List<string>();
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add($"{reader.GetInt64(0)}|{reader.GetInt64(1)}|{reader.GetString(3)}");
            }

            if (lines.Count == 0)
            {
                return null;
            }

            var raw = string.Join('\n', lines);

            return new QueryPlan(command.CommandText, "Sqlite", raw, SqlitePlanParser.Parse(raw), DateTimeOffset.UtcNow);
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
