# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## symplr CLI (`src/Cli`)

A cross-platform .NET 10 CLI (Windows + Linux) for querying symplr Platform services, modelled after `gh` and `aws`. Single binary, no runtime dependency for end users.

### Build / run

```bash
# Dev loop
dotnet run --project src/Cli -- <command> [options]

# Build
dotnet build src/Cli

# Publish single-file executable
dotnet publish src/Cli -r win-x64   -c Release --self-contained -o dist/win
dotnet publish src/Cli -r linux-x64 -c Release --self-contained -o dist/linux
```

### Commands

```
symplr [--env|-e dev|qa|stable|staging|production]   # default: stable, or SYMPLR_ENVIRONMENT
  auth login [--token <bearer>]   # PKCE browser flow; --token skips it
  auth logout
  auth status

  config

  tcm tenants list [--with-products] [--output table|json]
  tcm tenants get <id> [--with-products] [--output table|json]
  tcm tenants search <query> [--output table|json]
  tcm tenants namespaces <id> [--output table|json]

  tcm namespaces list [--include-inactive] [--output table|json]
  tcm namespaces get <id> [--output table|json]

  tcm products list [--output table|json]
  tcm products get <id> [--output table|json]
  tcm products search <query> [--output table|json]
  tcm products tenants <id> [--filter <text>] [--namespace <name>] [--output table|json]
  tcm products environments <id> [--output table|json]

  tcm event-consumers list [--output table|json]
  tcm event-consumers get <id> [--output table|json]

  tcm event-types list [--output table|json]
  tcm event-types get <id> [--output table|json]

  tcm event-type-consumers list [--output table|json]
  tcm event-type-consumers get <id> [--output table|json]
  tcm event-type-consumers by-consumer <consumer-id> [--output table|json]
```

### Architecture

```
src/Cli/
  Program.cs                    root command + global --env option + SYMPLR_ENVIRONMENT wiring
  Commands/
    AuthCommands.cs             login / logout / status
    TcmCommands.cs              all tcm subcommand handlers
    ConfigCommands.cs           config command
  Auth/
    PkceFlowClient.cs           PKCE authorization-code flow (fixed port 14779)
    TokenStore.cs               per-env token storage in %APPDATA%\symplr\config.json
  Platform/
    EnvironmentConfig.cs        SymplrEnvironment enum → SSO + TCM base URLs
    TcmClient.cs                typed HttpClient for TCM REST API
  Output/
    Formatter.cs                PrintTable / PrintJson / Error helpers
```

**Auth flow:** PKCE + local callback on fixed port `14779`. `PkceFlowClient.LoginAsync` generates a PKCE verifier/challenge, starts `HttpListener` on `http://localhost:14779/`, opens the browser to PingFederate's authorization endpoint, waits for the redirect back with the auth code, then exchanges code + verifier for tokens at `{ssoHost}/as/token.oauth2`. The redirect URI `http://localhost:14779/callback` is registered exactly on the `ce-platform-symplr-cli` client — no wildcard. PingFederate brokers to Microsoft Entra as the upstream IdP; device flow is not viable for this reason. Tokens are stored per-environment in `config.json`.

**`--env` / `SYMPLR_ENVIRONMENT`:** Global `--env` option defaults to `stable`. If `SYMPLR_ENVIRONMENT` is set it overrides the default, but an explicit `--env` on the command line always wins. `SYMPLR_ENVIRONMENT` is case-insensitive.

**HttpClient base URL:** `TcmClient` sets `BaseAddress` to `env.TcmBaseUrl.TrimEnd('/') + "/"` and all relative paths omit the leading `/` (e.g. `v1/Tenant`). A leading `/` would replace the path segment rather than append to it.

**Error handling in TCM commands:** All tcm handlers go through `RunTcmAsync`, which checks token existence + expiry before calling the API and maps `HttpRequestException` status codes to friendly messages (401 → re-login, 403 → missing scope, other → status + message).

**404 handling:** `GetTenantAsync`, `GetNamespaceAsync`, `GetProductAsync`, `GetEventConsumerAsync`, `GetEventTypeAsync`, and `GetEventTypeConsumerAsync` return `null` on 404 rather than throwing.

**Environment → SSO mapping (non-obvious):**

| Env | SSO host |
|-----|----------|
| dev, qa | `https://dev-sso.symplr.com` |
| stable | `https://qa-sso.symplr.com` |
| staging | `https://stg-sso.symplr.com` |
| production | `https://sso.symplr.com` |

**TCM base URL pattern:** `https://{env}-platform.symplr.com/ce-platform-tenant-configuration-service` (production uses `https://platform.symplr.com/...`).

---

## DocGen tool

The XML doc-comment generator lives at `c:/Users/jcanady/Projects/tools/Docgen` (outside this repo intentionally — CI ignores it). It scans a C# directory for externally visible members that lack `///` comments and inserts generated ones, resolving CS1591.

### Commands

```bash
# Preview (default — reads nothing, writes nothing)
dotnet run --project c:/Users/jcanady/Projects/tools/Docgen -- <directory>

# Preview with per-member detail
dotnet run --project c:/Users/jcanady/Projects/tools/Docgen -- <directory> --verbose

# Apply edits in place
dotnet run --project c:/Users/jcanady/Projects/tools/Docgen -- <directory> --apply

# Build
dotnet build c:/Users/jcanady/Projects/tools/Docgen
```

### Post-apply verification

Always run against a clean working tree:

```bash
git status                                          # confirm clean before running
dotnet run --project c:/Users/jcanady/Projects/tools/Docgen -- ./src/<project> --apply
dotnet build -c Debug --no-incremental ./<solution> # must be 0 warnings, 0 errors
git diff --numstat src/ | awk '{a+=$1; d+=$2} END {print "added:", a, "deleted:", d}'
```

The `deleted` count **must be 0**. DocGen only inserts lines. Any deletion means something went wrong (usually encoding).

### Architecture

Two source files:
- `Program.cs` — argument parsing, calls `DocGenerator.Run()`
- `DocGenerator.cs` — all logic (~960 lines), single `internal static class`

#### Execution pipeline (`Run`)

1. **Parse** — `Directory.EnumerateFiles` for `*.cs`, skipping `bin/`, `obj/`, `*.g.cs`, `*.Designer.cs`. Each file is read via `SourceText.From(stream)` to detect encoding (preserves UTF-8 BOM).
2. **Collect interface members** — first pass builds `Dictionary<interfaceName, HashSet<memberKey>>` across all trees, used to emit `<inheritdoc/>` for implementers and overrides.
3. **Build edits** — second pass walks `DescendantNodes().OfType<MemberDeclarationSyntax>()`, filters by `IsExternallyVisible`, `HasDocComment`, and `IsExplicitInterfaceImplementation`, then calls `BuildDoc` to generate comment lines. Edits are `TextChange` (plain text insertions at `line.Start` offset 0).
4. **Write** — only if `--apply`; writes back with `text.Encoding` so BOM survives.

#### Key design constraints — preserve these

**Complete `<param>`/`<typeparam>` sets are mandatory.** The target project has `TreatWarningsAsErrors` with only CS1591 excused. An incomplete param set turns CS1573/CS1712 into a build *error*. Every generated comment carries a full set.

**`<see cref="..."/>` is used only for a constructor's own type.** An unresolvable cref raises CS1574 as a build error. Generic crefs use the doc-comment form `Foo{T}`, not `Foo<T>`.

**Edits are text insertions, never rewrites.** Roslyn is used only to parse. Inserting at `line.Start` places the comment above any attributes automatically and leaves all other formatting byte-for-byte identical. This is what makes the diff purely additive.

**Encoding and line endings are round-tripped.** `SourceText.From(stream)` detects encoding; `DetectLineEnding` matches the file's existing CRLF or LF. No mixing is introduced.

**`IsExternallyVisible` mirrors CS1591 exactly.** The member *and every containing type* must be `public` or `protected`. `internal`, `private protected`, explicit interface implementations, and already-documented members are all skipped.

**Members sharing a line are skipped, not guessed at.** Inserting at `line.Start` would attach the comment to the wrong declaration. These are reported as `skipped:` for manual handling.

#### Extension points

To improve generated text, extend these tables in `DocGenerator.cs`:

| Table | Purpose |
|---|---|
| `WellKnownProperties` | Named properties with hand-crafted summaries (`Id`, `CreatedBy`, `RowVersion`, …) |
| `WellKnownParameters` | Named parameters with hand-crafted text (`cancellationToken`, `logger`, …) |
| `VerbPhrases` | Verb → conjugated phrase mapping for method summaries (`get` → `Gets`, `upsert` → `Creates or updates`) |
| `TypeSuffixRules` | Name-suffix → summary template pairs (`Service`, `Repository`, `Controller`, …) |
| `Acronyms` | Words to preserve in upper-case during humanization (`API`, `ERP`, `JSON`, …) |
| `BooleanPrefixes` | Leading verb → "whether …" clause template for bool members (`is`, `include`, `should`, …) |
