namespace SymplrCli.Platform;

public enum SymplrEnvironment { Dev, Qa, Stable, Staging, Production }

public record EnvironmentConfig(string SsoBaseUrl)
{
    public string TokenEndpoint      => $"{SsoBaseUrl}/as/token.oauth2";
    public string DeviceAuthEndpoint => $"{SsoBaseUrl}/as/device_authz.oauth2";
    public string RevocationEndpoint => $"{SsoBaseUrl}/as/revoke_token.oauth2";

    public static EnvironmentConfig For(SymplrEnvironment env) => env switch
    {
        SymplrEnvironment.Dev        => new("https://dev-sso.symplr.com"),
        SymplrEnvironment.Qa         => new("https://dev-sso.symplr.com"),
        SymplrEnvironment.Stable     => new("https://qa-sso.symplr.com"),
        SymplrEnvironment.Staging    => new("https://stg-sso.symplr.com"),
        SymplrEnvironment.Production => new("https://sso.symplr.com"),
        _ => throw new ArgumentOutOfRangeException(nameof(env))
    };
}
