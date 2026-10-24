using System.Data.Common;
using EfCore.QueryExplain.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EfCore.QueryExplain.Tests;

public class QueryExplainInterceptorTests
{
    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";

    [Fact]
    public void ToList_SimpleQuery_ReturnsAllRows()
    {
        // Arrange
        using var connection = OpenConnection();
        var interceptor = CreateInterceptor(_ => { }, new SqlitePlanProvider());
        using var ctx = CreateContext(connection, interceptor);
        Seed(ctx);

        // Act
        var customers = ctx.Customers.OrderBy(c => c.Id).ToList();

        // Assert
        Assert.Equal(3, customers.Count);
        Assert.Equal("Ada", customers[0].Name);
    }

    [Fact]
    public void ToList_SameShapeThreeTimes_RaisesOneNPlusOneFinding()
    {
        // Arrange
        using var connection = OpenConnection();
        var interceptor = CreateInterceptor(
            o => o.NPlusOneThreshold = 3,
            new SqlitePlanProvider());
        var findings = CaptureFindings(interceptor);
        using var ctx = CreateContext(connection, interceptor);
        Seed(ctx);

        // Act
        foreach (var customerId in new[] { 1, 2, 3 })
        {
            ctx.Orders.Where(o => o.CustomerId == customerId).ToList();
        }

        // Assert
        var nPlusOne = findings.Where(f => f.Kind == FindingKind.NPlusOne).ToList();
        Assert.Single(nPlusOne);
    }

    [Fact]
    public void ToList_EnabledFalse_RaisesNoFindings()
    {
        // Arrange
        using var connection = OpenConnection();
        var interceptor = CreateInterceptor(
            o =>
            {
                o.NPlusOneThreshold = 3;
                o.Enabled = false;
            },
            new SqlitePlanProvider());
        var findings = CaptureFindings(interceptor);
        using var ctx = CreateContext(connection, interceptor);
        Seed(ctx);

        // Act
        foreach (var customerId in new[] { 1, 2, 3 })
        {
            ctx.Orders.Where(o => o.CustomerId == customerId).ToList();
        }

        // Assert
        Assert.Empty(findings);
    }

    [Fact]
    public void ToList_PlanProviderThrows_ReturnsCorrectRowsWithoutFindings()
    {
        // Arrange
        using var connection = OpenConnection();
        var interceptor = CreateInterceptor(
            o => o.NPlusOneThreshold = 3,
            new ThrowingPlanProvider());
        var findings = CaptureFindings(interceptor);
        using var ctx = CreateContext(connection, interceptor);
        Seed(ctx);
        var customerId = 2;

        // Act
        var orders = ctx.Orders.Where(o => o.CustomerId == customerId).ToList();

        // Assert
        var order = Assert.Single(orders);
        Assert.Equal(2, order.Id);
        Assert.Empty(findings);
    }

    [Fact]
    public void ToList_ScanOverThreshold_RaisesMissingIndexFinding()
    {
        // Arrange
        using var connection = OpenConnection();
        var interceptor = CreateInterceptor(
            o => o.ScanRowThreshold = 0,
            new ScanPlanProvider(rows: 5000));
        var findings = CaptureFindings(interceptor);
        using var ctx = CreateContext(connection, interceptor);
        Seed(ctx);

        // Act
        ctx.Orders.Where(o => o.CustomerId == 1).ToList();

        // Assert
        var finding = Assert.Single(findings);
        Assert.Equal(FindingKind.MissingIndex, finding.Kind);
        Assert.Contains("\"Orders\"", finding.Message);
    }

    /// <summary>
    /// Builds the interceptor from the same <see cref="QueryExplainOptions"/> that <c>UseQueryExplain(configure)</c>
    /// would use, so the tests can subscribe to <see cref="QueryExplainInterceptor.FindingReported"/>.
    /// </summary>
    private static QueryExplainInterceptor CreateInterceptor(
        Action<QueryExplainOptions> configure,
        IQueryPlanProvider planProvider)
    {
        var options = new QueryExplainOptions();
        configure(options);

        return new QueryExplainInterceptor(
            options,
            [planProvider],
            [new SequentialScanAnalyzer()],
            new NPlusOneDetector(),
            NullLogger<QueryExplainInterceptor>.Instance);
    }

    private static List<QueryFinding> CaptureFindings(QueryExplainInterceptor interceptor)
    {
        var findings = new List<QueryFinding>();
        interceptor.FindingReported += (_, finding) => findings.Add(finding);
        return findings;
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    private static ShopContext CreateContext(SqliteConnection connection, QueryExplainInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder()
            .UseSqlite(connection)
            .UseQueryExplain(interceptor)
            .Options;

        var ctx = new ShopContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static void Seed(ShopContext ctx)
    {
        ctx.Customers.AddRange(
            new Customer { Id = 1, Name = "Ada" },
            new Customer { Id = 2, Name = "Grace" },
            new Customer { Id = 3, Name = "Linus" });
        ctx.Orders.AddRange(
            new Order { Id = 1, CustomerId = 1, Total = 10m },
            new Order { Id = 2, CustomerId = 2, Total = 20m },
            new Order { Id = 3, CustomerId = 3, Total = 30m });
        ctx.SaveChanges();
    }

    public sealed class ShopContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();

        public DbSet<Order> Orders => Set<Order>();
    }

    public sealed class Customer
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public sealed class Order
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }

        public decimal Total { get; set; }
    }

    /// <summary>
    /// Stands in for a real EXPLAIN runner and always fails, as a broken provider would.
    /// </summary>
    private sealed class ThrowingPlanProvider : IQueryPlanProvider
    {
        public string ProviderName => SqliteProviderName;

        public Task<QueryPlan?> ExplainAsync(DbCommand command, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("EXPLAIN is unavailable");
    }

    /// <summary>
    /// Returns a plan with a single full-table scan over <c>Orders</c> estimated at <paramref name="rows"/> rows.
    /// </summary>
    private sealed class ScanPlanProvider(long rows) : IQueryPlanProvider
    {
        public string ProviderName => SqliteProviderName;

        public Task<QueryPlan?> ExplainAsync(DbCommand command, CancellationToken cancellationToken = default)
        {
            var scan = new PlanNode(
                Operation: "SCAN",
                Table: "Orders",
                Index: null,
                EstimatedRows: rows,
                EstimatedCost: null,
                Children: []);

            var plan = new QueryPlan(
                command.CommandText,
                "Sqlite",
                $"SCAN Orders (est. {rows} rows)",
                [scan],
                DateTimeOffset.UtcNow);

            return Task.FromResult<QueryPlan?>(plan);
        }
    }
}
