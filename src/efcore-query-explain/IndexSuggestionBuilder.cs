using System.Text.RegularExpressions;

namespace EfCore.QueryExplain;

/// <summary>
/// Builds a <c>CREATE INDEX</c> suggestion from the columns that appear in the WHERE clause of a SQL command.
/// </summary>
public static class IndexSuggestionBuilder
{
    // A bare, "double-quoted" or [bracketed] identifier.
    private const string Identifier = @"""[^""]+""|\[[^\]]+\]|\w+";

    private static readonly Regex WhereClause = new(
        @"\bWHERE\b(?<where>.*?)(?=\s(?:ORDER\s+BY|GROUP\s+BY|LIMIT|OFFSET)\b|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TableAlias = new(
        $@"\b(?:FROM|JOIN)\s+(?<table>{Identifier})\s+AS\s+(?<alias>{Identifier})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ColumnPredicate = new(
        $@"(?:(?<qualifier>{Identifier})\s*\.\s*)?(?<column>{Identifier})\s*(?:<>|!=|>=|<=|=|>|<|\bIN\b|\bLIKE\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Builds a <c>CREATE INDEX</c> statement over the WHERE-clause columns of <paramref name="table"/>,
    /// quoting identifiers with double quotes.
    /// </summary>
    /// <param name="commandText">The SQL text of the command.</param>
    /// <param name="table">The table to index.</param>
    /// <returns>The suggested statement, or <c>null</c> when no WHERE-clause columns belong to <paramref name="table"/>.</returns>
    public static string? Build(string commandText, string table) =>
        BuildCore(commandText, table, useBrackets: false);

    /// <summary>
    /// Builds a <c>CREATE INDEX</c> statement over the WHERE-clause columns of <paramref name="table"/>,
    /// quoting identifiers with <c>[brackets]</c> for SQL Server and double quotes otherwise.
    /// </summary>
    /// <param name="commandText">The SQL text of the command.</param>
    /// <param name="table">The table to index.</param>
    /// <param name="providerName">The EF Core provider name (e.g. <c>Microsoft.EntityFrameworkCore.SqlServer</c>).</param>
    /// <returns>The suggested statement, or <c>null</c> when no WHERE-clause columns belong to <paramref name="table"/>.</returns>
    public static string? Build(string commandText, string table, string providerName)
    {
        ArgumentNullException.ThrowIfNull(providerName);

        return BuildCore(
            commandText,
            table,
            useBrackets: providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase));
    }

    private static string? BuildCore(string commandText, string table, bool useBrackets)
    {
        ArgumentNullException.ThrowIfNull(commandText);
        ArgumentNullException.ThrowIfNull(table);

        var columns = FindWhereColumns(commandText, table);
        if (columns.Count == 0)
        {
            return null;
        }

        var (open, close) = useBrackets ? ("[", "]") : ("\"", "\"");
        var quotedColumns = string.Join(", ", columns.Select(c => $"{open}{c}{close}"));
        var indexName = $"IX_{table}_{string.Join("_", columns)}";

        return $"CREATE INDEX {indexName} ON {open}{table}{close} ({quotedColumns});";
    }

    private static List<string> FindWhereColumns(string commandText, string table)
    {
        var where = WhereClause.Match(commandText);
        if (!where.Success)
        {
            return [];
        }

        // Qualifiers may be the table name itself or any alias EF Core bound to it in FROM/JOIN.
        var qualifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { table };
        foreach (Match alias in TableAlias.Matches(commandText))
        {
            if (Unquote(alias.Groups["table"].Value).Equals(table, StringComparison.OrdinalIgnoreCase))
            {
                qualifiers.Add(Unquote(alias.Groups["alias"].Value));
            }
        }

        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match predicate in ColumnPredicate.Matches(where.Groups["where"].Value))
        {
            var qualifier = predicate.Groups["qualifier"];
            if (qualifier.Success && !qualifiers.Contains(Unquote(qualifier.Value)))
            {
                continue;
            }

            var column = Unquote(predicate.Groups["column"].Value);
            if (seen.Add(column))
            {
                columns.Add(column);
            }
        }

        return columns;
    }

    private static string Unquote(string identifier) => identifier.Trim('"', '[', ']');
}
