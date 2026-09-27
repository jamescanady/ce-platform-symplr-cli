using SymplrCli.Auth;

namespace SymplrCli.Platform;

public static class ServiceUrlResolver
{
    public static string DefaultPlatformHost(SymplrEnvironment env) => env switch
    {
        SymplrEnvironment.Production => "platform.symplr.com",
        _ => $"{env.ToString().ToLowerInvariant()}-platform.symplr.com",
    };

    public static string Resolve(SymplrEnvironment env, string serviceKey, string defaultRoutePrefix, TokenStore store)
    {
        var host = store.GetPlatformHost(env) ?? DefaultPlatformHost(env);
        var prefix = store.GetRoutePrefix(serviceKey) ?? defaultRoutePrefix;
        return $"https://{host.TrimEnd('/')}/{prefix.TrimStart('/')}";
    }
}
