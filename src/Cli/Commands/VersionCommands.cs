using System.CommandLine;
using System.Net.Http.Json;
using SymplrCli.Auth;
using SymplrCli.Output;
using SymplrCli.Platform;

namespace SymplrCli.Commands;

public record ServiceVersionResponse(
    string Name,
    int Major,
    int Minor,
    int Build,
    string Version,
    string ReleaseTag);

public static class VersionCommands
{
    public static Command BuildSubCommand(
        Option<SymplrEnvironment> envOption,
        string serviceKey,
        string defaultRoutePrefix)
    {
        var output = new Option<OutputFormat>("--output")
        {
            Description = "Output format: table or json",
            DefaultValueFactory = _ => OutputFormat.Table,
        };
        var cmd = new Command("version", "Show the deployed version of this service");
        cmd.Options.Add(output);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env = parseResult.GetValue(envOption);
            var fmt = parseResult.GetValue(output);

            var baseUrl = ServiceUrlResolver.Resolve(env, serviceKey, defaultRoutePrefix, new TokenStore());
            using var http = new HttpClient();
            ServiceVersionResponse? v;
            try
            {
                v = await http.GetFromJsonAsync(
                    $"{baseUrl}/version",
                    SymplrJsonContext.Default.ServiceVersionResponse,
                    ct);
            }
            catch (HttpRequestException ex)
            {
                Formatter.Error($"Request failed: {(int?)ex.StatusCode} {ex.Message}");
                return;
            }

            if (v is null) { Formatter.Error("No version information returned."); return; }

            if (fmt == OutputFormat.Json)
            {
                Formatter.PrintJson(v, SymplrJsonContext.Default.ServiceVersionResponse);
                return;
            }

            Formatter.PrintTable(
                ["FIELD", "VALUE"],
                [
                    ["Name",        v.Name],
                    ["Version",     v.Version],
                    ["Release Tag", v.ReleaseTag],
                    ["Major",       v.Major.ToString()],
                    ["Minor",       v.Minor.ToString()],
                    ["Build",       v.Build.ToString()],
                ]);
        });
        return cmd;
    }
}
