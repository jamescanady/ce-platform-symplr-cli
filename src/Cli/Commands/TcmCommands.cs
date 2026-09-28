using System.CommandLine;
using System.Net;
using SymplrCli.Auth;
using SymplrCli.Output;
using SymplrCli.Platform;

namespace SymplrCli.Commands;

public static class TcmCommands
{
    internal const string ServiceKey = "tcm";
    internal const string DefaultRoutePrefix = "ce-platform-tenant-configuration-service";

    public static Command Build(Option<SymplrEnvironment> envOption)
    {
        var tcm = new Command("tcm", "Tenant Configuration Management");

        var tenants = new Command("tenants", "Manage tenants");
        tenants.Subcommands.Add(BuildTenantsListCommand(envOption));
        tenants.Subcommands.Add(BuildTenantsGetCommand(envOption));
        tenants.Subcommands.Add(BuildTenantsSearchCommand(envOption));
        tenants.Subcommands.Add(BuildTenantsNamespacesCommand(envOption));

        var namespaces = new Command("namespaces", "Manage namespaces");
        namespaces.Subcommands.Add(BuildNamespacesListCommand(envOption));
        namespaces.Subcommands.Add(BuildNamespacesGetCommand(envOption));

        var products = new Command("products", "Manage products");
        products.Subcommands.Add(BuildProductsListCommand(envOption));
        products.Subcommands.Add(BuildProductsGetCommand(envOption));
        products.Subcommands.Add(BuildProductsSearchCommand(envOption));
        products.Subcommands.Add(BuildProductsTenantsCommand(envOption));
        products.Subcommands.Add(BuildProductsEnvironmentsCommand(envOption));

        var eventConsumers = new Command("event-consumers", "Manage event consumers");
        eventConsumers.Subcommands.Add(BuildEventConsumersListCommand(envOption));
        eventConsumers.Subcommands.Add(BuildEventConsumersGetCommand(envOption));
        eventConsumers.Subcommands.Add(BuildEventConsumersSyncCommand(envOption));
        eventConsumers.Subcommands.Add(BuildEventConsumersTestOAuthCommand(envOption));

        var eventTypes = new Command("event-types", "Manage event types");
        eventTypes.Subcommands.Add(BuildEventTypesListCommand(envOption));
        eventTypes.Subcommands.Add(BuildEventTypesGetCommand(envOption));

        var eventTypeConsumers = new Command("event-type-consumers", "Manage event type consumer mappings");
        eventTypeConsumers.Subcommands.Add(BuildEventTypeConsumersListCommand(envOption));
        eventTypeConsumers.Subcommands.Add(BuildEventTypeConsumersGetCommand(envOption));
        eventTypeConsumers.Subcommands.Add(BuildEventTypeConsumersByConsumerCommand(envOption));

        tcm.Subcommands.Add(tenants);
        tcm.Subcommands.Add(namespaces);
        tcm.Subcommands.Add(products);
        tcm.Subcommands.Add(eventConsumers);
        tcm.Subcommands.Add(eventTypes);
        tcm.Subcommands.Add(eventTypeConsumers);
        tcm.Subcommands.Add(VersionCommands.BuildSubCommand(envOption, ServiceKey, DefaultRoutePrefix));
        return tcm;
    }

    // ─── tenants ─────────────────────────────────────────────────────────────

    private static Command BuildTenantsListCommand(Option<SymplrEnvironment> envOption)
    {
        var withProducts = new Option<bool>("--with-products")
        {
            Description = "Include product relationships",
        };
        var includeDisabled = new Option<bool>("--include-disabled") { Description = "Include disabled records" };
        var output = OutputOption();
        var cmd = new Command("list", "List all tenants");
        cmd.Options.Add(withProducts);
        cmd.Options.Add(includeDisabled);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env         = parseResult.GetValue(envOption);
            var wp          = parseResult.GetValue(withProducts);
            var showDisabled = parseResult.GetValue(includeDisabled);
            var fmt         = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var tenants = await client.GetTenantsAsync(wp);
                if (tenants is null || tenants.Length == 0) { Console.WriteLine("No tenants found."); return; }
                if (!showDisabled) tenants = tenants.Where(t => !t.IsDisabled).ToArray();
                if (tenants.Length == 0) { Console.WriteLine("No active tenants found. Use --include-disabled to show all."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(tenants, SymplrJsonContext.Default.TenantResponseArray);
                    return;
                }

                Formatter.PrintTable(
                    ["ID", "NAME", "SHORT CODE", "GLOBAL CODE", "DISABLED"],
                    tenants.Select(t => new[]
                    {
                        t.Id?.ToString() ?? "",
                        t.Name,
                        t.TenantShortCode,
                        t.GlobalTenantCode,
                        t.IsDisabled ? "yes" : "no",
                    }));
                Formatter.PrintCount(tenants.Length);
            });
        });
        return cmd;
    }

    private static Command BuildTenantsGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<Guid>("id") { Description = "Tenant ID (UUID)" };
        var withProducts = new Option<bool>("--with-products")
        {
            Description = "Include product relationships",
        };
        var output = OutputOption();
        var cmd = new Command("get", "Get a tenant by ID");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(withProducts);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env = parseResult.GetValue(envOption);
            var id  = parseResult.GetValue(idArg);
            var wp  = parseResult.GetValue(withProducts);
            var fmt = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var tenant = await client.GetTenantAsync(id, wp);
                if (tenant is null) { Formatter.Error($"Tenant {id} not found."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(tenant, SymplrJsonContext.Default.TenantResponse);
                    return;
                }

                Formatter.PrintTable(
                    ["FIELD", "VALUE"],
                    TenantFields(tenant));
            });
        });
        return cmd;
    }

    private static Command BuildTenantsSearchCommand(Option<SymplrEnvironment> envOption)
    {
        var needleArg = new Argument<string>("query")
        {
            Description = "Search against name, description, shortCode, and globalTenantCode",
        };
        var includeDisabled = new Option<bool>("--include-disabled") { Description = "Include disabled records" };
        var output = OutputOption();
        var cmd = new Command("search", "Search tenants by name, short code, or global code");
        cmd.Arguments.Add(needleArg);
        cmd.Options.Add(includeDisabled);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env         = parseResult.GetValue(envOption);
            var needle      = parseResult.GetValue(needleArg)!;
            var showDisabled = parseResult.GetValue(includeDisabled);
            var fmt         = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var tenants = await client.FindTenantsAsync(needle);
                if (tenants is null || tenants.Length == 0) { Console.WriteLine("No matching tenants."); return; }
                if (!showDisabled) tenants = tenants.Where(t => !t.IsDisabled).ToArray();
                if (tenants.Length == 0) { Console.WriteLine("No active matching tenants. Use --include-disabled to show all."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(tenants, SymplrJsonContext.Default.TenantResponseArray);
                    return;
                }

                Formatter.PrintTable(
                    ["ID", "NAME", "SHORT CODE", "GLOBAL CODE", "DISABLED"],
                    tenants.Select(t => new[]
                    {
                        t.Id?.ToString() ?? "",
                        t.Name,
                        t.TenantShortCode,
                        t.GlobalTenantCode,
                        t.IsDisabled ? "yes" : "no",
                    }));
                Formatter.PrintCount(tenants.Length);
            });
        });
        return cmd;
    }

    private static Command BuildTenantsNamespacesCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg  = new Argument<Guid>("id") { Description = "Tenant ID (UUID)" };
        var output = OutputOption();
        var cmd    = new Command("namespaces", "List namespaces and product environments for a tenant");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env = parseResult.GetValue(envOption);
            var id  = parseResult.GetValue(idArg);
            var fmt = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var result = await client.GetTenantNamespacesAsync(id);
                if (result is null) { Console.WriteLine("No namespaces found for tenant."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(result, SymplrJsonContext.Default.TenantNamespaceResponse);
                    return;
                }

                Console.WriteLine($"Tenant: {result.TenantName} ({result.TenantId})");
                foreach (var ns in result.Namespaces ?? [])
                {
                    Console.WriteLine($"  Namespace: {ns.Namespace} ({ns.NamespaceId})");
                    Formatter.PrintTable(
                        ["  PRODUCT", "ENVIRONMENT", "PRODUCT ENV ID"],
                        (ns.Products ?? []).Select(p => new[]
                        {
                            $"  {p.ProductName}",
                            p.EnvironmentName ?? "",
                            p.ProductEnvironmentId.ToString(),
                        }));
                }
            });
        });
        return cmd;
    }

    // ─── namespaces ───────────────────────────────────────────────────────────

    private static Command BuildNamespacesListCommand(Option<SymplrEnvironment> envOption)
    {
        var includeDisabled = new Option<bool>("--include-disabled") { Description = "Include disabled namespaces" };
        var output = OutputOption();
        var cmd    = new Command("list", "List all namespaces");
        cmd.Options.Add(includeDisabled);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env         = parseResult.GetValue(envOption);
            var showDisabled = parseResult.GetValue(includeDisabled);
            var fmt         = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var namespaces = await client.GetNamespacesAsync(showDisabled);
                if (namespaces is null || namespaces.Length == 0) { Console.WriteLine("No namespaces found."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(namespaces, SymplrJsonContext.Default.NamespaceResponseArray);
                    return;
                }

                Formatter.PrintTable(
                    ["ID", "NAME", "SHORT CODE", "DESCRIPTION", "DEFAULT", "DISABLED"],
                    namespaces.Select(n => new[]
                    {
                        n.Id.ToString(),
                        n.Name ?? "",
                        n.ShortCode ?? "",
                        n.Description ?? "",
                        n.IsDefault ? "yes" : "no",
                        n.IsDisabled ? "yes" : "no",
                    }));
                Formatter.PrintCount(namespaces.Length);
            });
        });
        return cmd;
    }

    private static Command BuildNamespacesGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg  = new Argument<Guid>("id") { Description = "Namespace ID (UUID)" };
        var output = OutputOption();
        var cmd    = new Command("get", "Get a namespace by ID");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env = parseResult.GetValue(envOption);
            var id  = parseResult.GetValue(idArg);
            var fmt = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var ns = await client.GetNamespaceAsync(id);
                if (ns is null) { Formatter.Error($"Namespace {id} not found."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(ns, SymplrJsonContext.Default.NamespaceResponse);
                    return;
                }

                Formatter.PrintTable(
                    ["FIELD", "VALUE"],
                    [
                        ["Id",           ns.Id.ToString()],
                        ["Name",         ns.Name ?? ""],
                        ["Short Code",   ns.ShortCode ?? ""],
                        ["Description",  ns.Description ?? ""],
                        ["Default",      ns.IsDefault ? "yes" : "no"],
                        ["Disabled",     ns.IsDisabled ? "yes" : "no"],
                        ["Created",      ns.CreatedDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Created By",   ns.CreatedBy ?? ""],
                        ["Modified",     ns.LastModified.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Modified By",  ns.LastModifiedBy ?? ""],
                    ]);
            });
        });
        return cmd;
    }

    // ─── products ────────────────────────────────────────────────────────────

    private static Command BuildProductsListCommand(Option<SymplrEnvironment> envOption)
    {
        var includeDisabled = new Option<bool>("--include-disabled") { Description = "Include disabled records" };
        var output = OutputOption();
        var cmd    = new Command("list", "List all products");
        cmd.Options.Add(includeDisabled);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env         = parseResult.GetValue(envOption);
            var showDisabled = parseResult.GetValue(includeDisabled);
            var fmt         = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var products = await client.GetProductsAsync();
                if (products is null || products.Length == 0) { Console.WriteLine("No products found."); return; }
                if (!showDisabled) products = products.Where(p => !p.IsDisabled).ToArray();
                if (products.Length == 0) { Console.WriteLine("No active products found. Use --include-disabled to show all."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(products, SymplrJsonContext.Default.ProductResponseArray);
                    return;
                }

                Formatter.PrintTable(
                    ["ID", "NAME", "DESCRIPTION", "DISABLED"],
                    products.Select(p => new[]
                    {
                        p.Id?.ToString() ?? "",
                        p.Name,
                        p.Description,
                        p.IsDisabled ? "yes" : "no",
                    }));
                Formatter.PrintCount(products.Length);
            });
        });
        return cmd;
    }

    private static Command BuildProductsGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg  = new Argument<Guid>("id") { Description = "Product ID (UUID)" };
        var output = OutputOption();
        var cmd    = new Command("get", "Get a product by ID");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env = parseResult.GetValue(envOption);
            var id  = parseResult.GetValue(idArg);
            var fmt = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var product = await client.GetProductAsync(id);
                if (product is null) { Formatter.Error($"Product {id} not found."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(product, SymplrJsonContext.Default.ProductResponse);
                    return;
                }

                Formatter.PrintTable(
                    ["FIELD", "VALUE"],
                    [
                        ["Id",          product.Id?.ToString() ?? ""],
                        ["Name",        product.Name],
                        ["Description", product.Description],
                        ["Disabled",    product.IsDisabled ? "yes" : "no"],
                    ]);
            });
        });
        return cmd;
    }

    private static Command BuildProductsSearchCommand(Option<SymplrEnvironment> envOption)
    {
        var needleArg       = new Argument<string>("query") { Description = "Search against product name" };
        var includeDisabled = new Option<bool>("--include-disabled") { Description = "Include disabled records" };
        var output          = OutputOption();
        var cmd             = new Command("search", "Search products by name");
        cmd.Arguments.Add(needleArg);
        cmd.Options.Add(includeDisabled);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env         = parseResult.GetValue(envOption);
            var needle      = parseResult.GetValue(needleArg)!;
            var showDisabled = parseResult.GetValue(includeDisabled);
            var fmt         = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var products = await client.FindProductsAsync(needle);
                if (products is null || products.Length == 0) { Console.WriteLine("No matching products."); return; }
                if (!showDisabled) products = products.Where(p => !p.IsDisabled).ToArray();
                if (products.Length == 0) { Console.WriteLine("No active matching products. Use --include-disabled to show all."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(products, SymplrJsonContext.Default.ProductResponseArray);
                    return;
                }

                Formatter.PrintTable(
                    ["ID", "NAME", "DESCRIPTION", "DISABLED"],
                    products.Select(p => new[]
                    {
                        p.Id?.ToString() ?? "",
                        p.Name,
                        p.Description,
                        p.IsDisabled ? "yes" : "no",
                    }));
                Formatter.PrintCount(products.Length);
            });
        });
        return cmd;
    }

    private static Command BuildProductsTenantsCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg           = new Argument<Guid>("id") { Description = "Product ID (UUID)" };
        var filterOption    = new Option<string?>("--filter") { Description = "Filter by tenant name, short code, or global code" };
        var namespaceOption = new Option<string?>("--namespace") { Description = "Filter by namespace name" };
        var output          = OutputOption();
        var cmd             = new Command("tenants", "List tenants using a product");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(filterOption);
        cmd.Options.Add(namespaceOption);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env = parseResult.GetValue(envOption);
            var id  = parseResult.GetValue(idArg);
            var filter = parseResult.GetValue(filterOption);
            var ns  = parseResult.GetValue(namespaceOption);
            var fmt = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var tenants = await client.GetTenantsByProductAsync(id, filter, ns);
                if (tenants is null || tenants.Length == 0) { Console.WriteLine("No tenants found for product."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(tenants, SymplrJsonContext.Default.TenantByProductResponseArray);
                    return;
                }

                Formatter.PrintTable(
                    ["TENANT ID", "NAME", "SHORT CODE", "NAMESPACE", "ENVIRONMENT"],
                    tenants.Select(t => new[]
                    {
                        t.Id.ToString(),
                        t.Name ?? "",
                        t.TenantShortCode ?? "",
                        t.NameSpace ?? "",
                        t.EnvironmentName ?? "",
                    }));
                Formatter.PrintCount(tenants.Length);
            });
        });
        return cmd;
    }

    private static Command BuildProductsEnvironmentsCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg           = new Argument<Guid>("id") { Description = "Product ID (UUID)" };
        var includeDisabled = new Option<bool>("--include-disabled") { Description = "Include disabled records" };
        var output          = OutputOption();
        var cmd             = new Command("environments", "List environments for a product");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(includeDisabled);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env         = parseResult.GetValue(envOption);
            var id          = parseResult.GetValue(idArg);
            var showDisabled = parseResult.GetValue(includeDisabled);
            var fmt         = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var envs = await client.GetProductEnvironmentsAsync(id);
                if (envs is null || envs.Length == 0) { Console.WriteLine("No environments found for product."); return; }
                if (!showDisabled) envs = envs.Where(e => !e.IsDisabled).ToArray();
                if (envs.Length == 0) { Console.WriteLine("No active environments found. Use --include-disabled to show all."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(envs, SymplrJsonContext.Default.ProductEnvironmentResponseArray);
                    return;
                }

                Formatter.PrintTable(
                    ["ID", "NAME", "DISABLED"],
                    envs.Select(e => new[]
                    {
                        e.Id?.ToString() ?? "",
                        e.Name ?? "",
                        e.IsDisabled ? "yes" : "no",
                    }));
                Formatter.PrintCount(envs.Length);
            });
        });
        return cmd;
    }

    // ─── event-consumers ────────────────────────────────────────────────────

    private static Command BuildEventConsumersListCommand(Option<SymplrEnvironment> envOption)
    {
        var includeDisabled = new Option<bool>("--include-disabled") { Description = "Include disabled records" };
        var output          = OutputOption();
        var cmd             = new Command("list", "List all event consumers");
        cmd.Options.Add(includeDisabled);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env          = parseResult.GetValue(envOption);
            var showDisabled = parseResult.GetValue(includeDisabled);
            var fmt          = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var consumers = await client.GetEventConsumersAsync();
                if (consumers is null || consumers.Length == 0) { Console.WriteLine("No event consumers found."); return; }
                if (!showDisabled) consumers = consumers.Where(c => !c.IsDisabled).ToArray();
                if (consumers.Length == 0) { Console.WriteLine("No active event consumers found. Use --include-disabled to show all."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(consumers, SymplrJsonContext.Default.EventConsumerResponseArray);
                    return;
                }

                Formatter.PrintTable(
                    ["ID", "NAME", "ENDPOINT", "AUTH TYPE", "DISABLED"],
                    consumers.Select(c => new[]
                    {
                        c.Id?.ToString() ?? "",
                        c.Name,
                        c.Endpoint,
                        c.AuthorizationType ?? "",
                        c.IsDisabled ? "yes" : "no",
                    }));
                Formatter.PrintCount(consumers.Length);
            });
        });
        return cmd;
    }

    private static Command BuildEventConsumersGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg  = new Argument<Guid>("id") { Description = "Event Consumer ID (UUID)" };
        var output = OutputOption();
        var cmd    = new Command("get", "Get an event consumer by ID");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env = parseResult.GetValue(envOption);
            var id  = parseResult.GetValue(idArg);
            var fmt = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var consumer = await client.GetEventConsumerAsync(id);
                if (consumer is null) { Formatter.Error($"Event consumer {id} not found."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(consumer, SymplrJsonContext.Default.EventConsumerResponse);
                    return;
                }

                Formatter.PrintTable(
                    ["FIELD", "VALUE"],
                    [
                        ["Id",           consumer.Id?.ToString() ?? ""],
                        ["Tenant Id",    consumer.TenantId.ToString()],
                        ["Name",         consumer.Name],
                        ["Description",  consumer.Description ?? ""],
                        ["Endpoint",        consumer.Endpoint],
                        ["Auth Type",       consumer.AuthorizationType ?? ""],
                        ["Disabled",        consumer.IsDisabled ? "yes" : "no"],
                        ["Last Sync",       consumer.LastSyncDate?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? ""],
                        ["Last Sync Msg",   consumer.LastSyncMessage ?? ""],
                        ["Version",         consumer.Version.ToString()],
                        ["Created",         consumer.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Created By",      consumer.CreatedBy ?? ""],
                        ["Modified",        consumer.LastModified.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Modified By",  consumer.LastModifiedBy ?? ""],
                    ]);
            });
        });
        return cmd;
    }

    private static Command BuildEventConsumersTestOAuthCommand(Option<SymplrEnvironment> envOption)
    {
        var consumerIdOption = new Option<Guid>("--consumer-id")
        {
            Description = "Event Consumer ID (UUID)",
            Required    = true,
        };
        var cmd = new Command("test-oauth", "Test OAuth credentials for an event consumer");
        cmd.Options.Add(consumerIdOption);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env        = parseResult.GetValue(envOption);
            var consumerId = parseResult.GetValue(consumerIdOption);
            await RunTcmAsync(env, async client =>
            {
                var response = await client.TestOAuthCredentialsAsync(consumerId);
                var body     = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"OAuth credentials test passed ({(int)response.StatusCode} {response.ReasonPhrase}).");
                    if (!string.IsNullOrWhiteSpace(body))
                        Console.WriteLine(body);
                }
                else
                {
                    Formatter.Error($"OAuth credentials test failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                    if (!string.IsNullOrWhiteSpace(body))
                        Console.Error.WriteLine(body);
                }
            });
        });
        return cmd;
    }

    private static Command BuildEventConsumersSyncCommand(Option<SymplrEnvironment> envOption)
    {
        var consumerIdOption = new Option<Guid>("--consumer-id")
        {
            Description = "Event Consumer ID (UUID)",
            Required    = true,
        };
        var cmd = new Command("sync", "Trigger an immediate sync for an event consumer");
        cmd.Options.Add(consumerIdOption);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env        = parseResult.GetValue(envOption);
            var consumerId = parseResult.GetValue(consumerIdOption);
            await RunTcmAsync(env, async client =>
            {
                await client.SyncEventConsumerAsync(consumerId);
                Console.WriteLine($"Sync triggered for consumer {consumerId}.");
            });
        });
        return cmd;
    }

    // ─── event-types ─────────────────────────────────────────────────────────

    private static Command BuildEventTypesListCommand(Option<SymplrEnvironment> envOption)
    {
        var includeDisabled = new Option<bool>("--include-disabled") { Description = "Include disabled records" };
        var output          = OutputOption();
        var cmd             = new Command("list", "List all event types");
        cmd.Options.Add(includeDisabled);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env          = parseResult.GetValue(envOption);
            var showDisabled = parseResult.GetValue(includeDisabled);
            var fmt          = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var types = await client.GetEventTypesAsync();
                if (types is null || types.Length == 0) { Console.WriteLine("No event types found."); return; }
                if (!showDisabled) types = types.Where(t => !t.IsDisabled).ToArray();
                if (types.Length == 0) { Console.WriteLine("No active event types found. Use --include-disabled to show all."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(types, SymplrJsonContext.Default.EventTypeResponseArray);
                    return;
                }

                Formatter.PrintTable(
                    ["ID", "NAME", "PRODUCT ID", "DISABLED"],
                    types.Select(t => new[]
                    {
                        t.Id.ToString(),
                        t.Name ?? "",
                        t.ProductId.ToString(),
                        t.IsDisabled ? "yes" : "no",
                    }));
                Formatter.PrintCount(types.Length);
            });
        });
        return cmd;
    }

    private static Command BuildEventTypesGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg  = new Argument<Guid>("id") { Description = "Event Type ID (UUID)" };
        var output = OutputOption();
        var cmd    = new Command("get", "Get an event type by ID");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env = parseResult.GetValue(envOption);
            var id  = parseResult.GetValue(idArg);
            var fmt = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var et = await client.GetEventTypeAsync(id);
                if (et is null) { Formatter.Error($"Event type {id} not found."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(et, SymplrJsonContext.Default.EventTypeResponse);
                    return;
                }

                Formatter.PrintTable(
                    ["FIELD", "VALUE"],
                    [
                        ["Id",          et.Id.ToString()],
                        ["Product Id",  et.ProductId.ToString()],
                        ["Name",        et.Name ?? ""],
                        ["Description", et.Description ?? ""],
                        ["Disabled",    et.IsDisabled ? "yes" : "no"],
                        ["Created",     et.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Created By",  et.CreatedBy ?? ""],
                        ["Modified",    et.LastModified.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Modified By", et.LastModifiedBy ?? ""],
                    ]);
            });
        });
        return cmd;
    }

    // ─── event-type-consumers ────────────────────────────────────────────────

    private static Command BuildEventTypeConsumersListCommand(Option<SymplrEnvironment> envOption)
    {
        var includeDisabled = new Option<bool>("--include-disabled") { Description = "Include disabled records" };
        var output          = OutputOption();
        var cmd             = new Command("list", "List all event type consumer mappings");
        cmd.Options.Add(includeDisabled);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env          = parseResult.GetValue(envOption);
            var showDisabled = parseResult.GetValue(includeDisabled);
            var fmt          = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var mappings = await client.GetEventTypeConsumersAsync();
                if (mappings is null || mappings.Length == 0) { Console.WriteLine("No event type consumer mappings found."); return; }
                if (!showDisabled) mappings = mappings.Where(m => !m.IsDisabled).ToArray();
                if (mappings.Length == 0) { Console.WriteLine("No active event type consumer mappings found. Use --include-disabled to show all."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(mappings, SymplrJsonContext.Default.EventTypeConsumerResponseArray);
                    return;
                }

                Formatter.PrintTable(
                    ["ID", "EVENT TYPE", "CONSUMER", "TENANT", "PRODUCT", "ENVIRONMENT", "DISABLED"],
                    mappings.Select(m => new[]
                    {
                        m.Id.ToString(),
                        m.EventTypeName ?? m.EventTypeId.ToString(),
                        m.ConsumerName ?? m.ConsumerId.ToString(),
                        m.TenantName ?? m.TenantId.ToString(),
                        m.ProductName ?? m.ProductId.ToString(),
                        m.EnvironmentName ?? "",
                        m.IsDisabled ? "yes" : "no",
                    }));
                Formatter.PrintCount(mappings.Length);
            });
        });
        return cmd;
    }

    private static Command BuildEventTypeConsumersGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg  = new Argument<Guid>("id") { Description = "Event Type Consumer ID (UUID)" };
        var output = OutputOption();
        var cmd    = new Command("get", "Get an event type consumer mapping by ID");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env = parseResult.GetValue(envOption);
            var id  = parseResult.GetValue(idArg);
            var fmt = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var m = await client.GetEventTypeConsumerAsync(id);
                if (m is null) { Formatter.Error($"Event type consumer {id} not found."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(m, SymplrJsonContext.Default.EventTypeConsumerResponse);
                    return;
                }

                Formatter.PrintTable(
                    ["FIELD", "VALUE"],
                    [
                        ["Id",                         m.Id.ToString()],
                        ["Consumer Id",                m.ConsumerId.ToString()],
                        ["Consumer Name",              m.ConsumerName ?? ""],
                        ["Consumer Endpoint",          m.ConsumerEndpoint ?? ""],
                        ["Consumer Auth Type",         m.ConsumerAuthorizationType ?? ""],
                        ["Event Type Id",              m.EventTypeId.ToString()],
                        ["Event Type Name",            m.EventTypeName ?? ""],
                        ["Tenant Id",                  m.TenantId.ToString()],
                        ["Tenant Name",                m.TenantName ?? ""],
                        ["Product Id",                 m.ProductId.ToString()],
                        ["Product Name",               m.ProductName ?? ""],
                        ["Product String",             m.ProductString ?? ""],
                        ["Environment",                m.EnvironmentName ?? ""],
                        ["Tenant Product Env Id",      m.TenantProductEnvironmentId.ToString()],
                        ["Disabled",                   m.IsDisabled ? "yes" : "no"],
                        ["Created",                    m.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Created By",                 m.CreatedBy ?? ""],
                        ["Modified",                   m.LastModified.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Modified By",                m.LastModifiedBy ?? ""],
                    ]);
            });
        });
        return cmd;
    }

    private static Command BuildEventTypeConsumersByConsumerCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg           = new Argument<Guid>("consumer-id") { Description = "Event Consumer ID (UUID)" };
        var includeDisabled = new Option<bool>("--include-disabled") { Description = "Include disabled records" };
        var output          = OutputOption();
        var cmd             = new Command("by-consumer", "List all event type mappings for a given event consumer");
        cmd.Arguments.Add(idArg);
        cmd.Options.Add(includeDisabled);
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env          = parseResult.GetValue(envOption);
            var id           = parseResult.GetValue(idArg);
            var showDisabled = parseResult.GetValue(includeDisabled);
            var fmt          = parseResult.GetValue(output);
            await RunTcmAsync(env, async client =>
            {
                var mappings = await client.GetEventTypeConsumersByConsumerAsync(id);
                if (mappings is null || mappings.Length == 0) { Console.WriteLine("No mappings found for event consumer."); return; }
                if (!showDisabled) mappings = mappings.Where(m => !m.IsDisabled).ToArray();
                if (mappings.Length == 0) { Console.WriteLine("No active mappings found. Use --include-disabled to show all."); return; }

                if (fmt == OutputFormat.Json)
                {
                    Formatter.PrintJson(mappings, SymplrJsonContext.Default.EventTypeConsumerResponseArray);
                    return;
                }

                Formatter.PrintTable(
                    ["ID", "EVENT TYPE", "TENANT", "PRODUCT", "ENVIRONMENT", "DISABLED"],
                    mappings.Select(m => new[]
                    {
                        m.Id.ToString(),
                        m.EventTypeName ?? m.EventTypeId.ToString(),
                        m.TenantName ?? m.TenantId.ToString(),
                        m.ProductName ?? m.ProductId.ToString(),
                        m.EnvironmentName ?? "",
                        m.IsDisabled ? "yes" : "no",
                    }));
                Formatter.PrintCount(mappings.Length);
            });
        });
        return cmd;
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static Option<OutputFormat> OutputOption() =>
        new("--output")
        {
            Description = "Output format: table or json",
            DefaultValueFactory = _ => OutputFormat.Table,
        };

    private static async Task RunTcmAsync(SymplrEnvironment env, Func<TcmClient, Task> action)
    {
        var store = new TokenStore();
        var token = store.Load(env);

        if (token is null)
        {
            Formatter.Error($"Not logged in to {env}. Run: symplr auth login --env {env.ToString().ToLowerInvariant()}");
            return;
        }

        if (token.ExpiresAt < DateTimeOffset.UtcNow)
        {
            Formatter.Error($"Token for {env} has expired. Run: symplr auth login --env {env.ToString().ToLowerInvariant()}");
            return;
        }

        var baseUrl = ServiceUrlResolver.Resolve(env, ServiceKey, DefaultRoutePrefix, store);
        var client  = TcmClient.Create(baseUrl, token.AccessToken);
        try
        {
            await action(client);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Formatter.Error($"Unauthorized (401). Your token may have expired. Run: symplr auth login --env {env.ToString().ToLowerInvariant()}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            Formatter.Error("Forbidden (403). Your token does not have the required scope.");
        }
        catch (HttpRequestException ex)
        {
            Formatter.Error($"Request failed: {(int?)ex.StatusCode} {ex.Message}");
        }
    }

    private static IEnumerable<string[]> TenantFields(TenantResponse t)
    {
        yield return ["Id",              t.Id?.ToString() ?? ""];
        yield return ["Name",            t.Name];
        yield return ["Description",     t.Description];
        yield return ["Global Code",     t.GlobalTenantCode];
        yield return ["Short Code",      t.TenantShortCode];
        yield return ["Disabled",        t.IsDisabled ? "yes" : "no"];

        if (t.Products is { Length: > 0 })
        {
            yield return ["", ""];
            yield return ["Products", ""];
            foreach (var p in t.Products)
                yield return [$"  {p.Name}", p.IsDisabled ? "disabled" : "enabled"];
        }
    }
}
