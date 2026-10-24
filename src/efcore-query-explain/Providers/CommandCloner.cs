using System.Data.Common;

namespace EfCore.QueryExplain.Providers;

/// <summary>
/// Creates helper commands that run on the same connection and transaction as an intercepted command.
/// </summary>
internal static class CommandCloner
{
    /// <summary>
    /// Creates a command on the source connection with the source transaction, the given text and copies of the source parameters.
    /// </summary>
    /// <param name="source">The intercepted command to copy connection, transaction and parameters from. It is not modified.</param>
    /// <param name="newText">The SQL text for the new command.</param>
    /// <returns>A new command the caller must dispose.</returns>
    internal static DbCommand CloneWithText(DbCommand source, string newText)
    {
        var clone = source.Connection!.CreateCommand();
        clone.Transaction = source.Transaction;
        clone.CommandText = newText;

        foreach (DbParameter parameter in source.Parameters)
        {
            var copy = clone.CreateParameter();
            copy.ParameterName = parameter.ParameterName;
            copy.Value = parameter.Value;
            copy.DbType = parameter.DbType;
            copy.Direction = parameter.Direction;
            copy.Size = parameter.Size;
            clone.Parameters.Add(copy);
        }

        return clone;
    }
}
