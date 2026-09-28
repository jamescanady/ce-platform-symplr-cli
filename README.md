# symplr CLI

A cross-platform CLI for querying symplr Platform services.

## Creating a release

Releases are driven by a git tag. Pushing a tag triggers the release workflow, which builds binaries for all platforms, generates release notes from merged PRs, and publishes a GitHub release.

**Prerequisites:** the tag must follow [semver](https://semver.org/) with a `v` prefix — e.g. `v1.2.3`.

```bash
# Make sure main is up to date
git checkout main
git pull

# Tag the commit
git tag v1.2.3

# Push the tag — this is what triggers the release workflow
git push origin v1.2.3
```

The workflow (`release.yml`) then:
1. Extracts the version from the tag (`v1.2.3` → `1.2.3`)
2. Cross-compiles a self-contained binary for each platform in parallel
3. Packages each binary (`symplr-1.2.3-<rid>.zip` / `.tar.gz`)
4. Builds release notes from PRs merged since the previous tag
5. Creates a GitHub release with all four archives attached

**Platform artifacts produced:**

| File | Platform |
|------|----------|
| `symplr-<version>-win-x64.zip` | Windows x64 |
| `symplr-<version>-linux-x64.tar.gz` | Linux x64 |
| `symplr-<version>-osx-x64.tar.gz` | macOS Intel |
| `symplr-<version>-osx-arm64.tar.gz` | macOS Apple Silicon |

To delete a tag locally and remotely (e.g. to retag after a mistake):

```bash
git tag -d v1.2.3
git push origin :refs/tags/v1.2.3
```

---

## Installation

### Linux and macOS

The `install.sh` script detects your OS and architecture, downloads the correct binary from the latest (or a pinned) release, and installs it to `/usr/local/bin`. Run it directly or pipe it to `bash` — both forms are equivalent:

```bash
# Latest release — download and run
curl -fsSL https://raw.githubusercontent.com/jamescanady/ce-platform-symplr-cli/main/install.sh | bash

# Latest release — pipe directly to bash (no intermediate file)
curl -fsSL https://raw.githubusercontent.com/jamescanady/ce-platform-symplr-cli/main/install.sh | bash

# Specific version
curl -fsSL https://raw.githubusercontent.com/jamescanady/ce-platform-symplr-cli/main/install.sh | bash -s v1.2.3
```

Or download the script first if you want to inspect it before running:

```bash
curl -fsSLO https://raw.githubusercontent.com/jamescanady/ce-platform-symplr-cli/main/install.sh
chmod +x install.sh
./install.sh           # latest
./install.sh v1.2.3    # specific version
```

`sudo` is invoked automatically if the script is not already running as root.

**Supported platforms:** `linux-x64`, `osx-x64`, `osx-arm64`

---

### Windows

Run `install.ps1` from an elevated or standard PowerShell session. It downloads the latest release, extracts `symplr.exe` to `%LOCALAPPDATA%\Programs\symplr\`, and adds that directory to your user `PATH` automatically.

```powershell
# Latest release
irm https://raw.githubusercontent.com/jamescanady/ce-platform-symplr-cli/main/install.ps1 | iex

# Specific version
irm https://raw.githubusercontent.com/jamescanady/ce-platform-symplr-cli/main/install.ps1 | iex -Args v1.2.3
```

Or download the script first if you want to inspect it before running:

```powershell
Invoke-WebRequest https://raw.githubusercontent.com/jamescanady/ce-platform-symplr-cli/main/install.ps1 -OutFile install.ps1
.\install.ps1           # latest
.\install.ps1 v1.2.3    # specific version
```

Restart your terminal after installation for the `PATH` change to take effect.

**Supported platform:** `win-x64`

> If your execution policy blocks unsigned scripts, run `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` first.

---

### Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# Run without installing
dotnet run --project src/Cli -- <command> [options]

# Build a self-contained binary
dotnet publish src/Cli -r win-x64   -c Release --self-contained -o dist/win
dotnet publish src/Cli -r linux-x64 -c Release --self-contained -o dist/linux
dotnet publish src/Cli -r osx-arm64 -c Release --self-contained -o dist/osx-arm64
```

## Environments

All commands accept a global `--env` flag. The default environment is resolved in this priority order:

1. `--env` / `-e` on the command line
2. `SYMPLR_ENVIRONMENT` environment variable
3. Active environment stored by `symplr auth switch`
4. `stable`

```
--env, -e    dev | qa | stable | staging | production
```

```bash
# Persist a default across all terminal sessions
symplr auth switch production

# Override for the current shell session only
export SYMPLR_ENVIRONMENT=staging

# Override for a single command
symplr tcm tenants list --env dev
```

---

## Authentication

Tokens are stored per-environment in `%APPDATA%\symplr\config.json` (Windows) or `~/.config/symplr/config.json` (Linux).

### `symplr auth switch`

Sets the default environment, persisted to the config file. Takes effect for all subsequent commands that don't specify `--env` or `SYMPLR_ENVIRONMENT`.

```
symplr auth switch <environment>
```

| Argument | Description |
|----------|-------------|
| `environment` | Target environment: `dev`, `qa`, `stable`, `staging`, or `production` |

```bash
symplr auth switch production
symplr auth switch stable
```

Warns if no token is stored or the token has expired for the target environment, but the switch still takes effect.

---

### `symplr auth login`

Opens a browser window for SSO via PingFederate. Stores the resulting token for the target environment.

```
symplr auth login [--env <env>] [--token <bearer-token>]
```

| Option | Description |
|--------|-------------|
| `--token <value>` | Skip the browser flow and store this Bearer token directly (expires in 1 hour) |

```bash
symplr auth login
symplr auth login --env production
symplr auth login --token eyJhbGciOiJSUzI1NiJ9...
```

### `symplr auth logout`

Revokes the stored token for the target environment and removes it from the token store.

```
symplr auth logout [--env <env>]
```

```bash
symplr auth logout
symplr auth logout --env staging
```

### `symplr auth status`

Lists all stored sessions across all environments, with expiry times. An asterisk (`*`) marks the active environment.

```
symplr auth status
```

---

## Configuration

### `symplr config`

Displays the resolved configuration: config file path, `SYMPLR_ENVIRONMENT` value, default environment with source, any active URL overrides, and all stored sessions.

```
symplr config [--env <env>]
```

---

### Service URL overrides

Platform service URLs are built from two parts:

```
https://<platform-host>/<route-prefix>
```

| Part | Default | Example |
|------|---------|---------|
| Platform host | `{env}-platform.symplr.com` | `stable-platform.symplr.com` |
| Route prefix | Service-defined constant | `ce-platform-tenant-configuration-service` |

Production is the exception — its platform host is `platform.symplr.com` (no environment prefix).

Both parts can be overridden per-environment (host) or per-service (prefix) and are stored in `config.json`.

#### `symplr config set platform-host <host>`

Override the platform hostname for an environment. Affects all services in that environment.

```
symplr config set platform-host <host> [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `host` | Hostname to use (e.g. `my-stable.example.com`) |

```bash
symplr config set platform-host my-stable.example.com --env stable
symplr config set platform-host localhost:8080 --env dev
```

#### `symplr config set route-prefix <service> <prefix>`

Override the route prefix for a specific service. Applies across all environments.

```
symplr config set route-prefix <service> <prefix>
```

| Argument | Description |
|----------|-------------|
| `service` | Service key (e.g. `tcm`) |
| `prefix` | Route prefix to use (e.g. `my-tcm-route`) |

```bash
symplr config set route-prefix tcm my-tenant-config-service
```

#### `symplr config unset platform-host`

Remove the platform hostname override for an environment, restoring the default.

```
symplr config unset platform-host [--env <env>]
```

```bash
symplr config unset platform-host --env stable
```

#### `symplr config unset route-prefix <service>`

Remove the route prefix override for a service, restoring the default.

```
symplr config unset route-prefix <service>
```

```bash
symplr config unset route-prefix tcm
```

---

## Tenant Configuration Management (TCM)

All `tcm` subcommands require an active, non-expired token for the target environment. All support `--output table|json` (default: `table`).

### `symplr tcm version`

Show the deployed version of the TCM service. Does not require authentication.

```
symplr tcm version [--output table|json] [--env <env>]
```

```bash
symplr tcm version
symplr tcm version --env production
symplr tcm version --output json
```

---

### Tenants

#### `symplr tcm tenants list`

List all tenants.

```
symplr tcm tenants list [--with-products] [--output table|json] [--env <env>]
```

| Option | Description |
|--------|-------------|
| `--with-products` | Include product relationships in the response |

#### `symplr tcm tenants get <id>`

Get a single tenant by ID.

```
symplr tcm tenants get <id> [--with-products] [--output table|json] [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `id` | Tenant UUID |

#### `symplr tcm tenants search <query>`

Search tenants by name, description, short code, or global tenant code.

```
symplr tcm tenants search <query> [--output table|json] [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `query` | Search string matched against name, description, shortCode, and globalTenantCode |

#### `symplr tcm tenants namespaces <id>`

List namespaces and product environments for a specific tenant.

```
symplr tcm tenants namespaces <id> [--output table|json] [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `id` | Tenant UUID |

---

### Namespaces

#### `symplr tcm namespaces list`

List all namespaces.

```
symplr tcm namespaces list [--include-inactive] [--output table|json] [--env <env>]
```

| Option | Description |
|--------|-------------|
| `--include-inactive` | Include disabled and deleted namespaces |

#### `symplr tcm namespaces get <id>`

Get a single namespace by ID.

```
symplr tcm namespaces get <id> [--output table|json] [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `id` | Namespace UUID |

---

### Products

#### `symplr tcm products list`

List all products.

```
symplr tcm products list [--output table|json] [--env <env>]
```

#### `symplr tcm products get <id>`

Get a single product by ID.

```
symplr tcm products get <id> [--output table|json] [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `id` | Product UUID |

#### `symplr tcm products search <query>`

Search products by name.

```
symplr tcm products search <query> [--output table|json] [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `query` | Search string matched against product name |

#### `symplr tcm products tenants <id>`

List tenants using a specific product.

```
symplr tcm products tenants <id> [--filter <text>] [--namespace <name>] [--output table|json] [--env <env>]
```

| Argument / Option | Description |
|-------------------|-------------|
| `id` | Product UUID |
| `--filter <text>` | Filter results by tenant name, short code, or global code |
| `--namespace <name>` | Filter results by namespace name |

#### `symplr tcm products environments <id>`

List environments for a specific product.

```
symplr tcm products environments <id> [--output table|json] [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `id` | Product UUID |

---

### Event Consumers

#### `symplr tcm event-consumers list`

List all event consumers.

```
symplr tcm event-consumers list [--output table|json] [--env <env>]
```

#### `symplr tcm event-consumers get <id>`

Get a single event consumer by ID.

```
symplr tcm event-consumers get <id> [--output table|json] [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `id` | Event Consumer UUID |

---

### Event Types

#### `symplr tcm event-types list`

List all event types.

```
symplr tcm event-types list [--output table|json] [--env <env>]
```

#### `symplr tcm event-types get <id>`

Get a single event type by ID.

```
symplr tcm event-types get <id> [--output table|json] [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `id` | Event Type UUID |

---

### Event Type Consumers

Mappings between event types and the consumers that receive them.

#### `symplr tcm event-type-consumers list`

List all event type consumer mappings.

```
symplr tcm event-type-consumers list [--output table|json] [--env <env>]
```

#### `symplr tcm event-type-consumers get <id>`

Get a single event type consumer mapping by ID.

```
symplr tcm event-type-consumers get <id> [--output table|json] [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `id` | Event Type Consumer UUID |

#### `symplr tcm event-type-consumers by-consumer <consumer-id>`

List all event type mappings for a given event consumer.

```
symplr tcm event-type-consumers by-consumer <consumer-id> [--output table|json] [--env <env>]
```

| Argument | Description |
|----------|-------------|
| `consumer-id` | Event Consumer UUID |

---

## Event Engine (BEE)

All `bee` subcommands require an active, non-expired token for the target environment unless noted otherwise.

### `symplr bee version`

Show the deployed version of the Event Engine Validation service. Does not require authentication.

```
symplr bee version [--output table|json] [--env <env>]
```

```bash
symplr bee version
symplr bee version --env production
symplr bee version --output json
```

---

### `symplr bee event`

Submit a single event to the Event Engine. Any option not supplied on the command line will be prompted interactively.

```
symplr bee event [--tenant-id <uuid>] [--product-name <name>] [--event-name <name>] [--env <env>]
```

| Option | Description |
|--------|-------------|
| `--tenant-id <uuid>` | Tenant ID |
| `--product-name <name>` | Product name |
| `--event-name <name>` | Event name / type |
| `--payload <json>` | JSON payload as an object or array of objects (see below) |

The `environment` field sent in the request body is prompted with the current `--env` value as the default.

On success, prints the correlation ID returned by the server.

#### Payload

`--payload` accepts a JSON object or a JSON array of objects. A single object is automatically wrapped in an array. If omitted, a default sample payload is used:

```json
[{ "userName": "John Smith", "id": "<generated-uuid>" }]
```

```bash
# Fully interactive — prompts for all fields, uses default payload
symplr bee event

# Partially specified, default payload
symplr bee event --tenant-id 00000000-0000-0000-0000-000000000000 --product-name MyProduct

# Single object payload (wrapped in array automatically)
symplr bee event --event-name MyEvent --payload '{"userId":"abc","action":"login"}'

# Array of objects
symplr bee event --event-name MyEvent --payload '[{"userId":"abc"},{"userId":"def"}]'
```

---

### `symplr bee audits`

Query Event Engine audit records. Exactly one filter group must be provided.

```
symplr bee audits (--correlation-id <id> | --consumer-id <id> | --tenant-id <id> --product-name <name> [--environment <env>])
                  [--output table|json] [--watch] [--interval <seconds>] [--env <env>]
```

**Filter groups (mutually exclusive):**

| Option(s) | Description |
|-----------|-------------|
| `--correlation-id <id>` | Look up a specific event by its correlation ID |
| `--consumer-id <id>` | All audit records for a consumer |
| `--tenant-id <id> --product-name <name>` | All audit records for a tenant + product. `--environment` defaults to the current `--env` value |

**Other options:**

| Option | Description |
|--------|-------------|
| `--environment <env>` | Environment filter string (used with `--tenant-id`, defaults to current `--env`) |
| `--output table\|json` | Output format (default: `table`) |
| `--watch` | Continuously refresh results until Ctrl+C |
| `--interval <seconds>` | Refresh interval when using `--watch` (default: `5`) |

```bash
# Look up a specific event
symplr bee audits --correlation-id 00000000-0000-0000-0000-000000000000

# All records for a consumer
symplr bee audits --consumer-id 00000000-0000-0000-0000-000000000000

# All records for a tenant + product in the current environment
symplr bee audits --tenant-id 00000000-0000-0000-0000-000000000000 --product-name MyProduct

# Watch mode — refresh every 10 seconds
symplr bee audits --consumer-id 00000000-0000-0000-0000-000000000000 --watch --interval 10
```

---

## Output formats

All data commands support `--output table` (default) and `--output json`.

```bash
# Human-readable table
symplr tcm tenants list

# Machine-readable JSON (pipe to jq, etc.)
symplr tcm tenants list --output json | jq '.[] | .name'
```
