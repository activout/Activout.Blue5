# AGENTS.md

Guidance for AI coding agents working in this repository.

## What this is

`Activout.Blue5` is an unofficial, read-only .NET 10 client for the Bluestone PIM Public API (PAPI).
It hides PAPI DTOs and pagination behind a small typed API: products, `AttributeKey<T>`-based
attribute access, and lazy cursor streaming.

## Layout

```
src/Activout.Blue5/                 core (namespace Activout.Blue5)
  Blue5Client.cs, ProductClient.cs  public API
  Attributes/                       namespace Activout.Blue5.Attributes: ProductAttributes, AttributeKey<T>, value models, AttributeConverter
  Internal/Papi.cs                  the only HttpClient code: headers, JSON, error → BluestoneException
  Internal/Dtos.cs, Mapping.cs      hand-written PAPI DTOs and DTO → public model mapping
src/Activout.Blue5.Resilience/      AddBlue5Resilience() on IHttpClientBuilder (Microsoft.Extensions.Http.Resilience)
src/Activout.Blue5.Cli/             `blue5` dotnet tool (System.CommandLine); Blue5Cli.cs has the command tree
tests/Activout.Blue5.Tests/         xUnit + RichardSzalay.MockHttp; one file per concern
  Support/Fake.cs                   JSON builders and a MockHttp-backed Blue5Client
  Fixtures/all-attribute-types.json every documented PAPI attribute type (synthetic data)
```

## Build / test / pack

```bash
dotnet build -c Release      # warnings are errors
dotnet test -c Release
dotnet pack -c Release -o artifacts
```

A `.env` file (git-ignored) may hold `BLUE5_BASE_URL`/`BLUE5_API_KEY`/`BLUE5_CONTEXT` for manual live
checks. Only make read-only calls, never print the key, and never commit real PAPI data. Fixtures
must be synthetic.

## Rules

- Namespace follows folder (e.g. `Attributes/` → `Activout.Blue5.Attributes`, `Internal/` → `Activout.Blue5.Internal`).

- Read-only. No write operations, no Management API.
- Extension members use C# 14 `extension(...)` blocks, not `this` parameters.
- `CancellationToken` is always the last parameter. Use async I/O (`WriteLineAsync`, not `WriteLine`) and pass the token.
- No `Async` suffix on methods. Public members need XML docs (the build fails otherwise).
- `HttpMessageHandler` is the test seam. No transport interfaces, no Activout.RestClient, no
  OpenAPI-generated code.
- DTOs stay `internal`. Public types are hand-designed models and never expose PAPI shapes or
  cursors.
- The core never retries. Resilience lives only in `Activout.Blue5.Resilience`.
- Missing attribute values → `AttributeKey.DefaultValue`. Present but unconvertible values →
  `AttributeConversionException`. Never swallow bad data.
- Product numbers are strings. Never parse them.
- When live PAPI behaviour differs from the docs, keep compatibility with the real API, add a
  focused test, and document it under "PAPI notes" in README.md.
- The design spec is kept locally (`Activout.Blue5-Code-Agent-Specification.md`, not committed).

## CI

`.github/workflows/ci.yml` runs restore, build, test and pack on every push and PR.
`.github/workflows/publish.yml` packs all packages and pushes them to NuGet.org on `v*` tags, using
the `NUGET_API_KEY` repository secret.
