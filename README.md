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

## Status

Early scaffold. Public API shape is defined; provider and analyzer implementations are in progress.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) - Copyright 2026 Vladyslav Zaiets
