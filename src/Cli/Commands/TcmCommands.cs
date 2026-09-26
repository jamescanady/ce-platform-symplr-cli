using System.CommandLine;
using System.Net;
using SymplrCli.Auth;
using SymplrCli.Output;
using SymplrCli.Platform;

namespace SymplrCli.Commands;

public static class TcmCommands
{
    public static Command Build(Option<SymplrEnvironment> envOption)
    {
        var tcm = new Command("tcm", "Tenant Configuration Management");

        var tenants = new Command("tenants", "Manage tenants");
        tenants.AddCommand(BuildTenantsListCommand(envOption));
        tenants.AddCommand(BuildTenantsGetCommand(envOption));
        tenants.AddCommand(BuildTenantsSearchCommand(envOption));
        tenants.AddCommand(BuildTenantsNamespacesCommand(envOption));

        var namespaces = new Command("namespaces", "Manage namespaces");
        namespaces.AddCommand(BuildNamespacesListCommand(envOption));
        namespaces.AddCommand(BuildNamespacesGetCommand(envOption));

        var products = new Command("products", "Manage products");
        products.AddCommand(BuildProductsListCommand(envOption));
        products.AddCommand(BuildProductsGetCommand(envOption));
        products.AddCommand(BuildProductsSearchCommand(envOption));
        products.AddCommand(BuildProductsTenantsCommand(envOption));
        products.AddCommand(BuildProductsEnvironmentsCommand(envOption));

        var eventConsumers = new Command("event-consumers", "Manage event consumers");
        eventConsumers.AddCommand(BuildEventConsumersListCommand(envOption));
        eventConsumers.AddCommand(BuildEventConsumersGetCommand(envOption));

        var eventTypes = new Command("event-types", "Manage event types");
        eventTypes.AddCommand(BuildEventTypesListCommand(envOption));
        eventTypes.AddCommand(BuildEventTypesGetCommand(envOption));

        var eventTypeConsumers = new Command("event-type-consumers", "Manage event type consumer mappings");
        eventTypeConsumers.AddCommand(BuildEventTypeConsumersListCommand(envOption));
        eventTypeConsumers.AddCommand(BuildEventTypeConsumersGetCommand(envOption));
        eventTypeConsumers.AddCommand(BuildEventTypeConsumersByConsumerCommand(envOption));

        tcm.AddCommand(tenants);
        tcm.AddCommand(namespaces);
        tcm.AddCommand(products);
        tcm.AddCommand(eventConsumers);
        tcm.AddCommand(eventTypes);
        tcm.AddCommand(eventTypeConsumers);
        return tcm;
    }

    // ─── tenants ─────────────────────────────────────────────────────────────

    private static Command BuildTenantsListCommand(Option<SymplrEnvironment> envOption)
    {
        var withProducts = new Option<bool>("--with-products", "Include product relationships");
        var output = OutputOption();
        var cmd = new Command("list", "List all tenants");
        cmd.AddOption(withProducts);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, wp, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var tenants = await client.GetTenantsAsync(wp);
                if (tenants is null || tenants.Length == 0) { Console.WriteLine("No tenants found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(tenants); return; }

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
            });
        }, envOption, withProducts, output);
        return cmd;
    }

    private static Command BuildTenantsGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<Guid>("id", "Tenant ID (UUID)");
        var withProducts = new Option<bool>("--with-products", "Include product relationships");
        var output = OutputOption();
        var cmd = new Command("get", "Get a tenant by ID");
        cmd.AddArgument(idArg);
        cmd.AddOption(withProducts);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, id, wp, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var tenant = await client.GetTenantAsync(id, wp);
                if (tenant is null) { Formatter.Error($"Tenant {id} not found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(tenant); return; }

                Formatter.PrintTable(
                    ["FIELD", "VALUE"],
                    TenantFields(tenant));
            });
        }, envOption, idArg, withProducts, output);
        return cmd;
    }

    private static Command BuildTenantsSearchCommand(Option<SymplrEnvironment> envOption)
    {
        var needleArg = new Argument<string>("query", "Search against name, description, shortCode, and globalTenantCode");
        var output = OutputOption();
        var cmd = new Command("search", "Search tenants by name, short code, or global code");
        cmd.AddArgument(needleArg);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, needle, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var tenants = await client.FindTenantsAsync(needle);
                if (tenants is null || tenants.Length == 0) { Console.WriteLine("No matching tenants."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(tenants); return; }

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
            });
        }, envOption, needleArg, output);
        return cmd;
    }

    private static Command BuildTenantsNamespacesCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<Guid>("id", "Tenant ID (UUID)");
        var output = OutputOption();
        var cmd = new Command("namespaces", "List namespaces and product environments for a tenant");
        cmd.AddArgument(idArg);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, id, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var results = await client.GetTenantNamespacesAsync(id);
                if (results is null || results.Length == 0) { Console.WriteLine("No namespaces found for tenant."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(results); return; }

                foreach (var tenant in results)
                {
                    Console.WriteLine($"Tenant: {tenant.TenantName} ({tenant.TenantId})");
                    foreach (var ns in tenant.Namespaces ?? [])
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
                }
            });
        }, envOption, idArg, output);
        return cmd;
    }

    // ─── namespaces ───────────────────────────────────────────────────────────

    private static Command BuildNamespacesListCommand(Option<SymplrEnvironment> envOption)
    {
        var includeInactive = new Option<bool>("--include-inactive", "Include disabled/deleted namespaces");
        var output = OutputOption();
        var cmd = new Command("list", "List all namespaces");
        cmd.AddOption(includeInactive);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, inactive, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var namespaces = await client.GetNamespacesAsync(inactive);
                if (namespaces is null || namespaces.Length == 0) { Console.WriteLine("No namespaces found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(namespaces); return; }

                Formatter.PrintTable(
                    ["ID", "NAME", "DESCRIPTION", "DEFAULT", "DISABLED"],
                    namespaces.Select(n => new[]
                    {
                        n.Id.ToString(),
                        n.Name ?? "",
                        n.Description ?? "",
                        n.IsDefault ? "yes" : "no",
                        n.IsDisabled ? "yes" : "no",
                    }));
            });
        }, envOption, includeInactive, output);
        return cmd;
    }

    private static Command BuildNamespacesGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<Guid>("id", "Namespace ID (UUID)");
        var output = OutputOption();
        var cmd = new Command("get", "Get a namespace by ID");
        cmd.AddArgument(idArg);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, id, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var ns = await client.GetNamespaceAsync(id);
                if (ns is null) { Formatter.Error($"Namespace {id} not found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(ns); return; }

                Formatter.PrintTable(
                    ["FIELD", "VALUE"],
                    [
                        ["Id",           ns.Id.ToString()],
                        ["Name",         ns.Name ?? ""],
                        ["Description",  ns.Description ?? ""],
                        ["Default",      ns.IsDefault ? "yes" : "no"],
                        ["Disabled",     ns.IsDisabled ? "yes" : "no"],
                        ["Created",      ns.CreatedDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Created By",   ns.CreatedBy ?? ""],
                        ["Modified",     ns.LastModified.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Modified By",  ns.LastModifiedBy ?? ""],
                    ]);
            });
        }, envOption, idArg, output);
        return cmd;
    }

    // ─── products ────────────────────────────────────────────────────────────

    private static Command BuildProductsListCommand(Option<SymplrEnvironment> envOption)
    {
        var output = OutputOption();
        var cmd = new Command("list", "List all products");
        cmd.AddOption(output);
        cmd.SetHandler(async (env, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var products = await client.GetProductsAsync();
                if (products is null || products.Length == 0) { Console.WriteLine("No products found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(products); return; }

                Formatter.PrintTable(
                    ["ID", "NAME", "DESCRIPTION", "DISABLED"],
                    products.Select(p => new[]
                    {
                        p.Id?.ToString() ?? "",
                        p.Name,
                        p.Description,
                        p.IsDisabled ? "yes" : "no",
                    }));
            });
        }, envOption, output);
        return cmd;
    }

    private static Command BuildProductsGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<Guid>("id", "Product ID (UUID)");
        var output = OutputOption();
        var cmd = new Command("get", "Get a product by ID");
        cmd.AddArgument(idArg);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, id, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var product = await client.GetProductAsync(id);
                if (product is null) { Formatter.Error($"Product {id} not found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(product); return; }

                Formatter.PrintTable(
                    ["FIELD", "VALUE"],
                    [
                        ["Id",          product.Id?.ToString() ?? ""],
                        ["Name",        product.Name],
                        ["Description", product.Description],
                        ["Disabled",    product.IsDisabled ? "yes" : "no"],
                    ]);
            });
        }, envOption, idArg, output);
        return cmd;
    }

    private static Command BuildProductsSearchCommand(Option<SymplrEnvironment> envOption)
    {
        var needleArg = new Argument<string>("query", "Search against product name");
        var output = OutputOption();
        var cmd = new Command("search", "Search products by name");
        cmd.AddArgument(needleArg);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, needle, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var products = await client.FindProductsAsync(needle);
                if (products is null || products.Length == 0) { Console.WriteLine("No matching products."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(products); return; }

                Formatter.PrintTable(
                    ["ID", "NAME", "DESCRIPTION", "DISABLED"],
                    products.Select(p => new[]
                    {
                        p.Id?.ToString() ?? "",
                        p.Name,
                        p.Description,
                        p.IsDisabled ? "yes" : "no",
                    }));
            });
        }, envOption, needleArg, output);
        return cmd;
    }

    private static Command BuildProductsTenantsCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<Guid>("id", "Product ID (UUID)");
        var filterOption = new Option<string?>("--filter", "Filter by tenant name, short code, or global code");
        var namespaceOption = new Option<string?>("--namespace", "Filter by namespace name");
        var output = OutputOption();
        var cmd = new Command("tenants", "List tenants using a product");
        cmd.AddArgument(idArg);
        cmd.AddOption(filterOption);
        cmd.AddOption(namespaceOption);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, id, filter, ns, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var tenants = await client.GetTenantsByProductAsync(id, filter, ns);
                if (tenants is null || tenants.Length == 0) { Console.WriteLine("No tenants found for product."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(tenants); return; }

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
            });
        }, envOption, idArg, filterOption, namespaceOption, output);
        return cmd;
    }

    private static Command BuildProductsEnvironmentsCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<Guid>("id", "Product ID (UUID)");
        var output = OutputOption();
        var cmd = new Command("environments", "List environments for a product");
        cmd.AddArgument(idArg);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, id, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var envs = await client.GetProductEnvironmentsAsync(id);
                if (envs is null || envs.Length == 0) { Console.WriteLine("No environments found for product."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(envs); return; }

                Formatter.PrintTable(
                    ["ID", "NAME", "DISABLED"],
                    envs.Select(e => new[]
                    {
                        e.Id?.ToString() ?? "",
                        e.Name ?? "",
                        e.IsDisabled ? "yes" : "no",
                    }));
            });
        }, envOption, idArg, output);
        return cmd;
    }

    // ─── event-consumers ────────────────────────────────────────────────────

    private static Command BuildEventConsumersListCommand(Option<SymplrEnvironment> envOption)
    {
        var output = OutputOption();
        var cmd = new Command("list", "List all event consumers");
        cmd.AddOption(output);
        cmd.SetHandler(async (env, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var consumers = await client.GetEventConsumersAsync();
                if (consumers is null || consumers.Length == 0) { Console.WriteLine("No event consumers found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(consumers); return; }

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
            });
        }, envOption, output);
        return cmd;
    }

    private static Command BuildEventConsumersGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<Guid>("id", "Event Consumer ID (UUID)");
        var output = OutputOption();
        var cmd = new Command("get", "Get an event consumer by ID");
        cmd.AddArgument(idArg);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, id, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var consumer = await client.GetEventConsumerAsync(id);
                if (consumer is null) { Formatter.Error($"Event consumer {id} not found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(consumer); return; }

                Formatter.PrintTable(
                    ["FIELD", "VALUE"],
                    [
                        ["Id",           consumer.Id?.ToString() ?? ""],
                        ["Tenant Id",    consumer.TenantId.ToString()],
                        ["Name",         consumer.Name],
                        ["Description",  consumer.Description ?? ""],
                        ["Endpoint",     consumer.Endpoint],
                        ["Auth Type",    consumer.AuthorizationType ?? ""],
                        ["Disabled",     consumer.IsDisabled ? "yes" : "no"],
                        ["Version",      consumer.Version.ToString()],
                        ["Created",      consumer.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Created By",   consumer.CreatedBy ?? ""],
                        ["Modified",     consumer.LastModified.ToLocalTime().ToString("yyyy-MM-dd HH:mm")],
                        ["Modified By",  consumer.LastModifiedBy ?? ""],
                    ]);
            });
        }, envOption, idArg, output);
        return cmd;
    }

    // ─── event-types ─────────────────────────────────────────────────────────

    private static Command BuildEventTypesListCommand(Option<SymplrEnvironment> envOption)
    {
        var output = OutputOption();
        var cmd = new Command("list", "List all event types");
        cmd.AddOption(output);
        cmd.SetHandler(async (env, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var types = await client.GetEventTypesAsync();
                if (types is null || types.Length == 0) { Console.WriteLine("No event types found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(types); return; }

                Formatter.PrintTable(
                    ["ID", "NAME", "PRODUCT ID", "DISABLED"],
                    types.Select(t => new[]
                    {
                        t.Id.ToString(),
                        t.Name ?? "",
                        t.ProductId.ToString(),
                        t.IsDisabled ? "yes" : "no",
                    }));
            });
        }, envOption, output);
        return cmd;
    }

    private static Command BuildEventTypesGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<Guid>("id", "Event Type ID (UUID)");
        var output = OutputOption();
        var cmd = new Command("get", "Get an event type by ID");
        cmd.AddArgument(idArg);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, id, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var et = await client.GetEventTypeAsync(id);
                if (et is null) { Formatter.Error($"Event type {id} not found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(et); return; }

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
        }, envOption, idArg, output);
        return cmd;
    }

    // ─── event-type-consumers ────────────────────────────────────────────────

    private static Command BuildEventTypeConsumersListCommand(Option<SymplrEnvironment> envOption)
    {
        var output = OutputOption();
        var cmd = new Command("list", "List all event type consumer mappings");
        cmd.AddOption(output);
        cmd.SetHandler(async (env, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var mappings = await client.GetEventTypeConsumersAsync();
                if (mappings is null || mappings.Length == 0) { Console.WriteLine("No event type consumer mappings found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(mappings); return; }

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
            });
        }, envOption, output);
        return cmd;
    }

    private static Command BuildEventTypeConsumersGetCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<Guid>("id", "Event Type Consumer ID (UUID)");
        var output = OutputOption();
        var cmd = new Command("get", "Get an event type consumer mapping by ID");
        cmd.AddArgument(idArg);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, id, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var m = await client.GetEventTypeConsumerAsync(id);
                if (m is null) { Formatter.Error($"Event type consumer {id} not found."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(m); return; }

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
        }, envOption, idArg, output);
        return cmd;
    }

    private static Command BuildEventTypeConsumersByConsumerCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<Guid>("consumer-id", "Event Consumer ID (UUID)");
        var output = OutputOption();
        var cmd = new Command("by-consumer", "List all event type mappings for a given event consumer");
        cmd.AddArgument(idArg);
        cmd.AddOption(output);
        cmd.SetHandler(async (env, id, fmt) =>
        {
            await RunTcmAsync(env, async client =>
            {
                var mappings = await client.GetEventTypeConsumersByConsumerAsync(id);
                if (mappings is null || mappings.Length == 0) { Console.WriteLine("No mappings found for event consumer."); return; }

                if (fmt == OutputFormat.Json) { Formatter.PrintJson(mappings); return; }

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
            });
        }, envOption, idArg, output);
        return cmd;
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static Option<OutputFormat> OutputOption() =>
        new("--output", () => OutputFormat.Table, "Output format: table or json");

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

        var client = TcmClient.Create(EnvironmentConfig.For(env), token.AccessToken);
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
