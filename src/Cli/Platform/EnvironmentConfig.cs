namespace SymplrCli.Platform;

public enum SymplrEnvironment { Dev, Qa, Stable, Staging, Production }

public record EnvironmentConfig(string SsoBaseUrl, string TcmBaseUrl)
{
    public string TokenEndpoint          => $"{SsoBaseUrl}/as/token.oauth2";
    public string DeviceAuthEndpoint     => $"{SsoBaseUrl}/as/device_authz.oauth2";
    public string RevocationEndpoint     => $"{SsoBaseUrl}/as/revoke_token.oauth2";

    public static EnvironmentConfig For(SymplrEnvironment env) => env switch
    {
        SymplrEnvironment.Dev        => new("https://dev-sso.symplr.com",  "https://dev-platform.symplr.com/ce-platform-tenant-configuration-service"),
        SymplrEnvironment.Qa         => new("https://dev-sso.symplr.com",  "https://qa-platform.symplr.com/ce-platform-tenant-configuration-service"),
        SymplrEnvironment.Stable     => new("https://qa-sso.symplr.com",   "https://stable-platform.symplr.com/ce-platform-tenant-configuration-service"),
        SymplrEnvironment.Staging    => new("https://stg-sso.symplr.com",  "https://stg-platform.symplr.com/ce-platform-tenant-configuration-service"),
        SymplrEnvironment.Production => new("https://sso.symplr.com",      "https://platform.symplr.com/ce-platform-tenant-configuration-service"),
        _ => throw new ArgumentOutOfRangeException(nameof(env))
    };
}
