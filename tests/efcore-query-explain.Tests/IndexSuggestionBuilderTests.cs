namespace EfCore.QueryExplain.Tests;

public class IndexSuggestionBuilderTests
{
    private const string NpgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    [Theory]
    [InlineData(
        """"SELECT o."Id", o."Total" FROM "Orders" AS o WHERE o."CustomerId" = @__customerId_0 ORDER BY o."Id"""",
        """CREATE INDEX IX_Orders_CustomerId ON "Orders" ("CustomerId");""")]
    [InlineData(
        """SELECT o."Id", o."Total" FROM "Orders" AS o WHERE o."CustomerId" = @p0 AND o."Status" = @p1""",
        """CREATE INDEX IX_Orders_CustomerId_Status ON "Orders" ("CustomerId", "Status");""")]
    [InlineData(
        """SELECT o."Id" FROM "Orders" AS o WHERE o."CustomerId" = @p0 OR o."CustomerId" = @p1""",
        """CREATE INDEX IX_Orders_CustomerId ON "Orders" ("CustomerId");""")]
    public void Build_NpgsqlWhereColumns_ReturnsExpectedStatement(string commandText, string expected)
    {
        // Arrange
        const string table = "Orders";

        // Act
        var statement = IndexSuggestionBuilder.Build(commandText, table);

        // Assert
        Assert.Equal(expected, statement);
    }

    [Theory]
    [InlineData("""SELECT o."Id" FROM "Orders" AS o WHERE o."Status" = @p0""")]
    [InlineData("""SELECT o."Id" FROM "Orders" AS o WHERE o."Status" IN (@p0, @p1)""")]
    [InlineData("""SELECT o."Id" FROM "Orders" AS o WHERE o."Status" LIKE @p0""")]
    public void Build_PredicateOperator_IncludesColumn(string commandText)
    {
        // Arrange
        const string table = "Orders";

        // Act
        var statement = IndexSuggestionBuilder.Build(commandText, table);

        // Assert
        Assert.Equal("""CREATE INDEX IX_Orders_Status ON "Orders" ("Status");""", statement);
    }

    [Fact]
    public void Build_SqlServerProvider_UsesBracketQuoting()
    {
        // Arrange
        const string commandText = """SELECT [o].[Id] FROM [Orders] AS [o] WHERE [o].[CustomerId] = @p0""";

        // Act
        var statement = IndexSuggestionBuilder.Build(commandText, "Orders", SqlServerProvider);

        // Assert
        Assert.Equal("CREATE INDEX IX_Orders_CustomerId ON [Orders] ([CustomerId]);", statement);
    }

    [Fact]
    public void Build_NonSqlServerProvider_UsesDoubleQuoteQuoting()
    {
        // Arrange
        const string commandText = """SELECT o."Id" FROM "Orders" AS o WHERE o."CustomerId" = @p0""";

        // Act
        var statement = IndexSuggestionBuilder.Build(commandText, "Orders", NpgsqlProvider);

        // Assert
        Assert.Equal("""CREATE INDEX IX_Orders_CustomerId ON "Orders" ("CustomerId");""", statement);
    }

    [Fact]
    public void Build_ColumnReferencedTwice_IncludesColumnOnce()
    {
        // Arrange
        const string commandText = """SELECT o."Id" FROM "Orders" AS o WHERE o."CustomerId" > @p0 AND o."CustomerId" < @p1""";

        // Act
        var statement = IndexSuggestionBuilder.Build(commandText, "Orders");

        // Assert
        Assert.Equal("""CREATE INDEX IX_Orders_CustomerId ON "Orders" ("CustomerId");""", statement);
    }

    [Theory]
    [InlineData(""""SELECT o."Id" FROM "Orders" AS o ORDER BY o."CustomerId"""")]
    [InlineData("""SELECT o."Id" FROM "Orders" AS o""")]
    public void Build_NoWhereClause_ReturnsNull(string commandText)
    {
        // Arrange
        const string table = "Orders";

        // Act
        var statement = IndexSuggestionBuilder.Build(commandText, table);

        // Assert
        Assert.Null(statement);
    }

    [Fact]
    public void Build_ColumnOnlyInOrderBy_ExcludesColumn()
    {
        // Arrange
        const string commandText = """"SELECT o."Id" FROM "Orders" AS o WHERE o."Status" = @p0 ORDER BY o."CustomerId"""";

        // Act
        var statement = IndexSuggestionBuilder.Build(commandText, "Orders");

        // Assert
        Assert.Equal("""CREATE INDEX IX_Orders_Status ON "Orders" ("Status");""", statement);
    }
}
