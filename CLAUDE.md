# EfCore.QueryExplain

EF Core `DbCommandInterceptor` for development: captures EXPLAIN plans, suggests missing indexes,
warns about N+1 query patterns. Target: net8.0, EF Core 8.

## Commands

```bash
dotnet build                          # zero warnings required (TreatWarningsAsErrors)
dotnet test                           # xunit, tests/efcore-query-explain.Tests
dotnet test --filter "FullyQualifiedName~NPlusOne"
dotnet format                         # apply .editorconfig
dotnet pack src/efcore-query-explain -c Release -o artifacts
```

## Layout

```
src/efcore-query-explain/        EfCore.QueryExplain (NuGet package)
  QueryExplainInterceptor.cs     entry point: DbCommandInterceptor, ReaderExecuting[Async]
  QueryExplainExtensions.cs      UseQueryExplain() on DbContextOptionsBuilder
  QueryExplainOptions.cs         thresholds, toggles
  QueryPlan.cs                   QueryPlan + PlanNode records (parsed EXPLAIN tree)
  IQueryPlanProvider.cs          per-provider EXPLAIN runner + parser
  IQueryAnalyzer.cs              IQueryAnalyzer, INPlusOneDetector, QueryFinding, enums
  Providers/                     (to create) Npgsql, SqlServer, Sqlite implementations
  Analyzers/                     (to create) MissingIndexAnalyzer, NPlusOneDetector
tests/efcore-query-explain.Tests/ xunit; Fixtures/ holds recorded EXPLAIN outputs
```

## Architecture

Flow per query: `ReaderExecuting` -> pick `IQueryPlanProvider` by `eventData.Context` provider name
-> `ExplainAsync(command)` on the same connection (must not consume the original command)
-> each `IQueryAnalyzer.Analyze(plan)` -> `INPlusOneDetector.Record(contextId, sql)`
-> findings logged via `ILogger` and raised on `FindingReported`.

Rules:
- Never execute the user's command ourselves; only EXPLAIN it. Return `result` unchanged.
- EXPLAIN must never break the app: swallow provider errors, log at Debug, respect `ExplainTimeout`.
- Skip non-SELECT commands and commands inside `EXPLAIN` itself (recursion guard).
- N+1 state is keyed by `DbContextId`; `Reset` on context dispose. Use `ConcurrentDictionary`.
- SQLite uses `EXPLAIN QUERY PLAN`; PostgreSQL `EXPLAIN (FORMAT JSON)`; SQL Server `SET SHOWPLAN_XML ON`.

## Conventions

- File-scoped namespaces, records for data, interfaces for extension points.
- Public API has XML docs (GenerateDocumentationFile = true, missing docs fail the build).
- Provider-specific code lives only in `Providers/`. Core has no provider package references
  beyond `Microsoft.EntityFrameworkCore.Relational`.
- Tests: Arrange/Act/Assert, one behavior per test, name `Method_Condition_Expected`.
- Conventional Commits. No Co-Authored-By trailers.
