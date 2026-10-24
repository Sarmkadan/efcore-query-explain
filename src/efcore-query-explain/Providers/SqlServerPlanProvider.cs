using System.Data;
using System.Data.Common;

namespace EfCore.QueryExplain.Providers;

/// <summary>
/// Captures SQL Server plans with <c>SET SHOWPLAN_XML ON</c> on the same connection as the intercepted command.
/// </summary>
/// <remarks>
/// With SHOWPLAN_XML enabled the server returns the estimated plan instead of executing the statement,
/// so the original command is only sent in that mode and is never executed for real.
/// </remarks>
public sealed class SqlServerPlanProvider : IQueryPlanProvider
{
    /// <inheritdoc />
    public string ProviderName => "Microsoft.EntityFrameworkCore.SqlServer";

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
            await SetShowPlanXmlAsync(command, enabled: true, cancellationToken);

            try
            {
                await using var explain = CommandCloner.CloneWithText(command, command.CommandText);
                var raw = (await explain.ExecuteScalarAsync(cancellationToken))?.ToString();
                if (raw is null)
                {
                    return null;
                }

                var nodes = SqlServerPlanParser.Parse(raw);

                return new QueryPlan(command.CommandText, "SqlServer", raw, nodes, DateTimeOffset.UtcNow);
            }
            finally
            {
                await SetShowPlanXmlAsync(command, enabled: false, cancellationToken);
            }
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task SetShowPlanXmlAsync(DbCommand command, bool enabled, CancellationToken cancellationToken)
    {
        await using var toggle = command.Connection!.CreateCommand();
        toggle.Transaction = command.Transaction;
        toggle.CommandText = enabled ? "SET SHOWPLAN_XML ON" : "SET SHOWPLAN_XML OFF";
        await toggle.ExecuteNonQueryAsync(cancellationToken);
    }
}
