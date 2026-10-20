# EfCore.QueryExplain

[![CI](https://github.com/Sarmkadan/efcore-query-explain/actions/workflows/ci.yml/badge.svg)](https://github.com/Sarmkadan/efcore-query-explain/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/EfCore.QueryExplain.svg)](https://www.nuget.org/packages/EfCore.QueryExplain)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

EF Core interceptor that captures EXPLAIN plans, suggests missing indexes and warns about N+1 queries
while you develop. Zero code changes in your queries: register once, read the log.

## Features

- Captures the execution plan of every query via the provider's native EXPLAIN
- Flags sequential/full scans on large tables and proposes a `CREATE INDEX` statement
- Detects N+1 patterns (same query shape, different parameters) inside one `DbContext` scope
- Providers: PostgreSQL (Npgsql), SQL Server, SQLite; extensible through `IQueryPlanProvider`
- Findings surface through `ILogger` and a `FindingReported` event; wire them to tests, dashboards or CI
- Development-only by design: one toggle to disable in production

## Quick start

```csharp
using EfCore.QueryExplain;

builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseQueryExplain(x =>
    {
        x.Enabled = builder.Environment.IsDevelopment();
        x.NPlusOneThreshold = 3;
        x.ScanRowThreshold = 5_000;
    }));
```

Then run your app and watch the log:

```
warn: EfCore.QueryExplain[1001]
      MissingIndex: Seq Scan on "Orders" (est. 120000 rows) filtered by "CustomerId".
      Suggested: CREATE INDEX IX_Orders_CustomerId ON "Orders" ("CustomerId");
warn: EfCore.QueryExplain[1002]
      NPlusOne: query executed 17 times in one DbContext scope. Consider .Include() or a join.
      SELECT ... FROM "OrderLines" WHERE "OrderId" = @p0
```

## Installation

```bash
dotnet add package EfCore.QueryExplain
```

Requires .NET 8 and EF Core 8.

## API overview

| Type | Role |
| --- | --- |
| `UseQueryExplain(...)` | Extension on `DbContextOptionsBuilder`; registers the interceptor and defaults |
| `QueryExplainOptions` | Toggles and thresholds (`NPlusOneThreshold`, `ScanRowThreshold`, `ExplainTimeout`, `Enabled`) |
| `QueryExplainInterceptor` | The `DbCommandInterceptor`; exposes `FindingReported` |
| `IQueryPlanProvider` | Runs and parses EXPLAIN for one database provider |
| `IQueryAnalyzer` | Turns a `QueryPlan` into `QueryFinding`s (e.g. missing index) |
| `INPlusOneDetector` | Tracks repeated query shapes per `DbContextId` |
| `QueryPlan` / `PlanNode` | Provider-agnostic parsed plan tree |
| `QueryFinding` | Kind, severity, message, offending SQL, suggested SQL |

Hook findings programmatically:

```csharp
var interceptor = context.GetService<QueryExplainInterceptor>();
interceptor.FindingReported += (_, f) =>
{
    if (f.Kind == FindingKind.NPlusOne) Assert.Fail(f.Message);
};
```

## Findings

Every diagnostic produced by the interceptor is represented as a `QueryFinding` record:

```csharp
public sealed record QueryFinding(
    FindingKind Kind,
    FindingSeverity Severity,
    string Message,
    string CommandText,
    string? SuggestedSql = null);
```

| Property | Description |
| --- | --- |
| `Kind` | Category of the problem (`FindingKind` enum) |
| `Severity` | How serious it is (`FindingSeverity` enum) |
| `Message` | Human-readable explanation |
| `CommandText` | The offending SQL statement |
| `SuggestedSql` | A fix as SQL (e.g. `CREATE INDEX ...`), or `null` when not applicable |

### FindingKind

| Value | Meaning |
| --- | --- |
| `MissingIndex` | A scan that would likely benefit from an index |
| `NPlusOne` | The same query shape executed repeatedly in one context scope |
| `UnusedIndex` | An index exists but the planner chose not to use it (type mismatch, function on column, etc.) |
| `Other` | Any other plan-derived observation |

### FindingSeverity

| Value | Meaning |
| --- | --- |
| `Info` | Informational only |
| `Warning` | Likely performance problem worth investigating |
| `Error` | Almost certainly a performance problem (e.g. full scan on a large table, confirmed N+1) |

### Log EventIds

Findings are written to `ILogger` with these EventIds:

| EventId | Category | Description |
| --- | --- | --- |
| 1001 | MissingIndex | A sequential or full scan on a table with estimated rows above `ScanRowThreshold` |
| 1002 | NPlusOne | The same parameterized query shape executed more than `NPlusOneThreshold` times in one `DbContext` scope |
| 1003 | Other | Any other plan-level observation (unused index, etc.) |

## Subscribing to findings

`QueryExplainInterceptor` exposes a `FindingReported` event. You can use it in integration tests to fail on N+1 queries:

```csharp
using EfCore.QueryExplain;
using Xunit;

public class NoPlusOneTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly List<QueryFinding> _findings = new();

    public NoPlusOneTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnectionString)
            .UseQueryExplain(x =>
            {
                x.NPlusOneThreshold = 2;
                x.ScanRowThreshold = 500;
            })
            .Options;

        _db = new AppDbContext(options);

        var interceptor = _db.GetService<QueryExplainInterceptor>();
        interceptor.FindingReported += OnFinding;
    }

    private void OnFinding(object? sender, QueryFinding finding)
    {
        _findings.Add(finding);
    }

    [Fact]
    public async Task GetOrdersWithLines_NoNPlusOne()
    {
        // arrange - seed test data

        // act
        var orders = await _db.Orders
            .Include(o => o.Lines)
            .ToListAsync();

        // assert
        var nPlusOnes = _findings
            .Where(f => f.Kind == FindingKind.NPlusOne)
            .ToList();

        Assert.Empty(nPlusOnes);
    }

    public void Dispose() => _db.Dispose();
}
```

## Writing a custom analyzer

Implement `IQueryAnalyzer` to add your own plan inspections. For example, flagging `Nested Loop` nodes with high estimated rows:

```csharp
using EfCore.QueryExplain;

public class NestedLoopAnalyzer : IQueryAnalyzer
{
    private const long RowThreshold = 10_000;

    public IReadOnlyList<QueryFinding> Analyze(QueryPlan plan, QueryExplainOptions options)
    {
        var findings = new List<QueryFinding>();
        InspectNodes(plan.Nodes, plan.CommandText, findings);
        return findings;
    }

    private static void InspectNodes(
        IReadOnlyList<PlanNode> nodes,
        string commandText,
        List<QueryFinding> findings)
    {
        foreach (var node in nodes)
        {
            if (node.Operation == "Nested Loop"
                && node.EstimatedRows.HasValue
                && node.EstimatedRows.Value > RowThreshold)
            {
                findings.Add(new QueryFinding(
                    FindingKind.Other,
                    FindingSeverity.Warning,
                    $"Nested Loop with {node.EstimatedRows.Value} estimated rows "
                    + $"on \"{node.Table ?? "unknown"}\". Consider a hash or merge join.",
                    commandText));
            }

            InspectNodes(node.Children, commandText, findings);
        }
    }
}
```

Register it when building the interceptor:

```csharp
// Option 1: pass it to the QueryExplainInterceptor constructor
var interceptor = new QueryExplainInterceptor(
    options,
    planProviders,
    new IQueryAnalyzer[] { defaultAnalyzer, new NestedLoopAnalyzer() },
    nPlusOneDetector,
    logger);

// Option 2: via DbContextOptionsBuilder (UseQueryExplain wires defaults + your additions)
builder.Services.AddDbContext<AppDbContext>(o => o
    .UseNpgsql(connectionString)
    .UseQueryExplain());

// Then register your analyzer in DI so the interceptor picks it up:
builder.Services.AddSingleton<IQueryAnalyzer, NestedLoopAnalyzer>();
```

## Writing a custom plan provider

Implement `IQueryPlanProvider` to support a database not covered out of the box. Here is a sketch for MySQL using `EXPLAIN FORMAT=JSON`:

```csharp
using System.Data.Common;
using System.Text.Json;
using EfCore.QueryExplain;

public class MySqlPlanProvider : IQueryPlanProvider
{
    public string ProviderName => "Pomelo.EntityFrameworkCore.MySql";

    public async Task<QueryPlan?> ExplainAsync(
        DbCommand command,
        CancellationToken cancellationToken = default)
    {
        // Build an EXPLAIN command on the same connection
        await using var explainCmd = command.Connection!.CreateCommand();
        explainCmd.CommandText = $"EXPLAIN FORMAT=JSON {command.CommandText}";

        // Copy parameters
        foreach (DbParameter p in command.Parameters)
        {
            var clone = explainCmd.CreateParameter();
            clone.ParameterName = p.ParameterName;
            clone.Value = p.Value;
            explainCmd.Parameters.Add(clone);
        }

        await using var reader = await explainCmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var json = reader.GetString(0);
        var nodes = ParseMySqlPlan(json);

        return new QueryPlan(
            command.CommandText,
            ProviderName,
            json,
            nodes,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<PlanNode> ParseMySqlPlan(string json)
    {
        // Parse the JSON plan tree into PlanNode objects.
        // MySQL's EXPLAIN FORMAT=JSON nests under "query_block".
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.GetProperty("query_block");

        var operation = root.TryGetProperty("table", out var table)
            ? table.GetProperty("access_type").GetString() ?? "unknown"
            : "query_block";

        long? rows = root.TryGetProperty("table", out var t)
            && t.TryGetProperty("rows_examined_per_scan", out var r)
                ? r.GetInt64()
                : null;

        return new[]
        {
            new PlanNode(operation, null, null, rows, null, Array.Empty<PlanNode>())
        };
    }
}
```

Register it the same way as a custom analyzer:

```csharp
builder.Services.AddSingleton<IQueryPlanProvider, MySqlPlanProvider>();
```

## Limitations

- **One extra round-trip per query.** Every intercepted command runs an EXPLAIN before the actual execution. Use this in development only; set `Enabled = false` (or bind it to `IsDevelopment()`) in production.
- **EXPLAIN reflects planner estimates, not actual rows.** The row counts and costs in findings are the optimizer's predictions. Real execution may differ, especially after statistics drift.
- **SQLite gives no row estimates.** The SQLite EXPLAIN output does not include estimated row counts, so `ScanRowThreshold` has no effect for SQLite connections. N+1 detection still works because it counts repeated query shapes, not plan data.
- **DDL and non-query commands are skipped.** Only reader commands (`SELECT` / queries) go through the interceptor. Migrations, `INSERT`, `UPDATE` and `DELETE` are not explained.

## Status

Early scaffold. Public API shape is defined; provider and analyzer implementations are in progress.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) - Copyright 2026 Vladyslav Zaiets
