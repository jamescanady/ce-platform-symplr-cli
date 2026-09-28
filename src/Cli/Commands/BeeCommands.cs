using System.CommandLine;
using System.Net;
using System.Text.Json;
using SymplrCli.Auth;
using SymplrCli.Output;
using SymplrCli.Platform;

namespace SymplrCli.Commands;

public static class BeeCommands
{
    internal const string ServiceKey           = "bee";
    internal const string DefaultRoutePrefix   = "event-engine-validation";
    internal const string AuditServiceKey      = "bee-audit";
    internal const string AuditRoutePrefix     = "event-engine-audit";

    public static Command Build(Option<SymplrEnvironment> envOption)
    {
        var bee = new Command("bee", "Event Engine");
        bee.Subcommands.Add(BuildEventCommand(envOption));
        bee.Subcommands.Add(BuildAuditsCommand(envOption));
        bee.Subcommands.Add(VersionCommands.BuildSubCommand(envOption, ServiceKey, DefaultRoutePrefix));
        return bee;
    }

    // ─── event ───────────────────────────────────────────────────────────────

    private static Command BuildEventCommand(Option<SymplrEnvironment> envOption)
    {
        var tenantIdOption  = new Option<Guid?>("--tenant-id")      { Description = "Tenant ID (UUID)" };
        var productOption   = new Option<string?>("--product-name") { Description = "Product name" };
        var eventNameOption = new Option<string?>("--event-name")   { Description = "Event name / type" };
        var payloadOption   = new Option<string?>("--payload")
        {
            Description = "JSON payload as an object or array of objects. Omit to use the default sample payload.",
        };

        var cmd = new Command("event", "Submit an event to the Event Engine");
        cmd.Options.Add(tenantIdOption);
        cmd.Options.Add(productOption);
        cmd.Options.Add(eventNameOption);
        cmd.Options.Add(payloadOption);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env = parseResult.GetValue(envOption);

            var tenantId   = parseResult.GetValue(tenantIdOption) ?? PromptGuid("Tenant ID");
            var product    = parseResult.GetValue(productOption)  ?? Prompt("Product name");
            var eventName  = parseResult.GetValue(eventNameOption) ?? Prompt("Event name");
            var envString  = Prompt("Environment", env.ToString().ToLowerInvariant());
            var payloadJson = parseResult.GetValue(payloadOption);

            JsonElement[] payload;
            if (payloadJson is null)
            {
                var defaultItem = new BeeDefaultPayloadItem("John Smith", Guid.NewGuid());
                payload = [JsonSerializer.SerializeToElement(defaultItem, SymplrJsonContext.Default.BeeDefaultPayloadItem)];
            }
            else
            {
                try
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    payload = doc.RootElement.ValueKind switch
                    {
                        JsonValueKind.Array  => doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToArray(),
                        JsonValueKind.Object => [doc.RootElement.Clone()],
                        _ => throw new JsonException("Payload must be a JSON object or array of objects."),
                    };
                }
                catch (JsonException ex)
                {
                    Formatter.Error($"Invalid --payload: {ex.Message}");
                    return;
                }
            }

            var request = new BeeEventRequest(
                EventName:   eventName,
                TenantId:    tenantId,
                ProductName: product,
                Environment: envString,
                Uri:         null,
                Counter:     1,
                Timestamp:   DateTimeOffset.UtcNow,
                Payload:     payload);

            await RunBeeAsync(env, async client =>
            {
                var response = await client.PostEventAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    var correlationId = GetCorrelationId(response);
                    Console.WriteLine($"Event submitted ({(int)response.StatusCode} {response.ReasonPhrase}).");
                    if (correlationId is not null)
                        Console.WriteLine($"Correlation ID: {correlationId}");
                    return;
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                Formatter.Error($"Request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                if (!string.IsNullOrWhiteSpace(body))
                    Console.Error.WriteLine(body);
            });
        });
        return cmd;
    }

    // ─── audits ──────────────────────────────────────────────────────────────

    private static Command BuildAuditsCommand(Option<SymplrEnvironment> envOption)
    {
        var correlationIdOption = new Option<string?>("--correlation-id") { Description = "Filter by correlation ID" };
        var consumerIdOption    = new Option<string?>("--consumer-id")    { Description = "Filter by consumer ID (UUID)" };
        var tenantIdOption      = new Option<string?>("--tenant-id")      { Description = "Filter by tenant ID (UUID)" };
        var productNameOption   = new Option<string?>("--product-name")   { Description = "Filter by product name (requires --tenant-id)" };
        var environmentOption   = new Option<string?>("--environment")    { Description = "Filter by environment (requires --tenant-id, defaults to current env)" };
        var output              = new Option<OutputFormat>("--output")
        {
            Description         = "Output format: table or json",
            DefaultValueFactory = _ => OutputFormat.Table,
        };
        var watchOption         = new Option<bool>("--watch")    { Description = "Continuously refresh results (Ctrl+C to stop)" };
        var intervalOption      = new Option<int>("--interval")
        {
            Description         = "Refresh interval in seconds when using --watch (default: 5)",
            DefaultValueFactory = _ => 5,
        };

        var cmd = new Command("audits", "Query Event Engine audit records");
        cmd.Options.Add(correlationIdOption);
        cmd.Options.Add(consumerIdOption);
        cmd.Options.Add(tenantIdOption);
        cmd.Options.Add(productNameOption);
        cmd.Options.Add(environmentOption);
        cmd.Options.Add(output);
        cmd.Options.Add(watchOption);
        cmd.Options.Add(intervalOption);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env           = parseResult.GetValue(envOption);
            var correlationId = parseResult.GetValue(correlationIdOption);
            var consumerId    = parseResult.GetValue(consumerIdOption);
            var tenantId      = parseResult.GetValue(tenantIdOption);
            var productName   = parseResult.GetValue(productNameOption);
            var environment   = parseResult.GetValue(environmentOption)
                                ?? (tenantId is not null ? env.ToString().ToLowerInvariant() : null);
            var fmt           = parseResult.GetValue(output);
            var watch         = parseResult.GetValue(watchOption);
            var interval      = parseResult.GetValue(intervalOption);

            var groupCount = (correlationId is not null ? 1 : 0)
                           + (consumerId    is not null ? 1 : 0)
                           + (tenantId      is not null ? 1 : 0);

            if (groupCount == 0)
            {
                Formatter.Error("Provide exactly one filter group:\n" +
                                "  --correlation-id <id>\n" +
                                "  --consumer-id <id>\n" +
                                "  --tenant-id <id> --product-name <name> [--environment <env>]");
                return;
            }

            if (groupCount > 1)
            {
                Formatter.Error("Only one filter group may be used at a time: --correlation-id, --consumer-id, or --tenant-id.");
                return;
            }

            if (tenantId is not null && productName is null)
            {
                Formatter.Error("--product-name is required when using --tenant-id.");
                return;
            }

            async Task RunOnce()
            {
                await RunBeeAuditAsync(env, async client =>
                {
                    var result = await client.GetAuditsAsync(tenantId, productName, environment, consumerId, correlationId, ct);
                    var events = result?.Events;

                    if (events is null || events.Length == 0) { Console.WriteLine("No audit records found."); return; }

                    if (fmt == OutputFormat.Json)
                    {
                        Formatter.PrintJson(result!, SymplrJsonContext.Default.AuditSearchResponse);
                        return;
                    }

                    Formatter.PrintTable(
                        ["CREATED", "EVENT NAME", "STATUS", "ERROR CODE", "CORRELATION ID"],
                        events.Select(e => new[]
                        {
                            e.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                            e.EventName     ?? "",
                            e.Status        ?? "",
                            e.ErrorCode     ?? "",
                            e.CorrelationId ?? "",
                        }));

                    if (events.Any(e => !string.IsNullOrEmpty(e.ErrorMessage)))
                    {
                        Console.WriteLine();
                        foreach (var e in events.Where(e => !string.IsNullOrEmpty(e.ErrorMessage)))
                            Console.WriteLine($"  [{e.CorrelationId}] {e.ErrorMessage}");
                    }
                });
            }

            if (!watch)
            {
                await RunOnce();
                return;
            }

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    Console.Clear();
                    Console.WriteLine($"Every {interval}s: symplr bee audits  —  {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}  —  Ctrl+C to stop");
                    Console.WriteLine();
                    await RunOnce();
                    await Task.Delay(TimeSpan.FromSeconds(interval), ct);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine();
            }
        });
        return cmd;
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private static string Prompt(string label, string? defaultValue = null)
    {
        Console.Write(defaultValue is not null ? $"{label} [{defaultValue}]: " : $"{label}: ");
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? (defaultValue ?? "") : input;
    }

    private static Guid PromptGuid(string label)
    {
        while (true)
        {
            Console.Write($"{label}: ");
            var input = Console.ReadLine()?.Trim();
            if (Guid.TryParse(input, out var id)) return id;
            Console.Error.WriteLine("  Invalid UUID — please enter a value like 00000000-0000-0000-0000-000000000000");
        }
    }

    private static string? GetCorrelationId(HttpResponseMessage response)
    {
        foreach (var name in new[] { "X-Correlation-Id", "X-Correlation-ID", "Correlation-Id", "x-correlation-id" })
        {
            if (response.Headers.TryGetValues(name, out var values))
                return values.FirstOrDefault();
        }
        return null;
    }

    private static async Task RunBeeAuditAsync(SymplrEnvironment env, Func<BeeAuditClient, Task> action)
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

        var baseUrl = ServiceUrlResolver.Resolve(env, AuditServiceKey, AuditRoutePrefix, store);
        var client  = BeeAuditClient.Create(baseUrl, token.AccessToken);
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

    private static async Task RunBeeAsync(SymplrEnvironment env, Func<BeeClient, Task> action)
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
        var client  = BeeClient.Create(baseUrl, token.AccessToken);
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
}
