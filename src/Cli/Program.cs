using System.CommandLine;
using SymplrCli.Auth;
using SymplrCli.Commands;
using SymplrCli.Platform;

var envOption = new Option<SymplrEnvironment>("--env", "-e")
{
    Description = "Target symplr environment (dev, qa, stable, staging, production). Overrides SYMPLR_ENVIRONMENT.",
    Recursive = true,
    DefaultValueFactory = _ =>
    {
        var raw = Environment.GetEnvironmentVariable("SYMPLR_ENVIRONMENT");
        if (raw is not null && Enum.TryParse<SymplrEnvironment>(raw, ignoreCase: true, out var fromVar))
            return fromVar;
        var active = new TokenStore().ActiveEnvironment();
        return active ?? SymplrEnvironment.Stable;
    }
};

var root = new RootCommand("symplr CLI — query symplr Platform services");
root.Options.Add(envOption);
root.Subcommands.Add(AuthCommands.Build(envOption));
root.Subcommands.Add(TcmCommands.Build(envOption));
root.Subcommands.Add(BeeCommands.Build(envOption));
root.Subcommands.Add(ConfigCommands.Build(envOption));

return root.Parse(args).Invoke();
