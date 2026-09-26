using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using SymplrCli.Auth;
using SymplrCli.Commands;
using SymplrCli.Platform;

var envOption = new Option<SymplrEnvironment>(
    ["--env", "-e"],
    () =>
    {
        var raw = Environment.GetEnvironmentVariable("SYMPLR_ENVIRONMENT");
        if (raw is not null && Enum.TryParse<SymplrEnvironment>(raw, ignoreCase: true, out var fromVar))
            return fromVar;
        var active = new TokenStore().ActiveEnvironment();
        return active ?? SymplrEnvironment.Stable;
    },
    "Target symplr environment (dev, qa, stable, staging, production). Overrides SYMPLR_ENVIRONMENT.");

var root = new RootCommand("symplr CLI — query symplr Platform services");
root.AddGlobalOption(envOption);
root.AddCommand(AuthCommands.Build(envOption));
root.AddCommand(TcmCommands.Build(envOption));
root.AddCommand(ConfigCommands.Build(envOption));

return await new CommandLineBuilder(root)
    .UseDefaults()
    .Build()
    .InvokeAsync(args);
