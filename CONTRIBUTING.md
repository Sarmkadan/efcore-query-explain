# Contributing

Thanks for taking the time. Keep it small and focused.

## Workflow

1. Open an issue describing the bug or feature before large changes.
2. Fork, branch from `main` (`feat/...`, `fix/...`).
3. `dotnet build` must pass with zero warnings (warnings are errors).
4. `dotnet test` must pass. New behavior needs tests.
5. Open a PR. CI runs build + tests on every push.

## Code style

- `.editorconfig` is the source of truth; run `dotnet format` before committing.
- File-scoped namespaces, `var` where the type is obvious, braces always.
- Public API gets XML docs. Missing docs fail the build.
- No provider-specific code outside `Providers/`.

## Commit messages

Conventional Commits: `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`.

## Adding a database provider

Implement `IQueryPlanProvider`, register it in `UseQueryExplain`, add a parser test with a
recorded EXPLAIN output fixture under `tests/.../Fixtures/`.
