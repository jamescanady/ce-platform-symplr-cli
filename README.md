# symplr CLI

A cross-platform CLI for querying symplr Platform services.

## Installation

Build a self-contained single-file executable:

```bash
# Windows
dotnet publish src/Cli -r win-x64 -c Release --self-contained -o dist/win

# Linux
dotnet publish src/Cli -r linux-x64 -c Release --self-contained -o dist/linux
```

Add the output directory to your `PATH`, then run `symplr`.

During development:

```bash
dotnet run --project src/Cli -- <command> [options]
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

Displays the resolved configuration: config file path, `SYMPLR_ENVIRONMENT` value, default environment with source, and all stored sessions.

```
symplr config [--env <env>]
```

---

## Tenant Configuration Management (TCM)

All `tcm` subcommands require an active, non-expired token for the target environment. All support `--output table|json` (default: `table`).

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

## Output formats

All data commands support `--output table` (default) and `--output json`.

```bash
# Human-readable table
symplr tcm tenants list

# Machine-readable JSON (pipe to jq, etc.)
symplr tcm tenants list --output json | jq '.[] | .name'
```
