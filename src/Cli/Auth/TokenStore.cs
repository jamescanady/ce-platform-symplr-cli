using System.Text.Json;
using System.Text.Json.Serialization;
using SymplrCli.Platform;

namespace SymplrCli.Auth;

public record StoredToken(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);

public class TokenStore
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "symplr");

    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed class Config
    {
        public Dictionary<string, StoredToken> Sessions { get; set; } = [];
        public string? ActiveEnvironment { get; set; }
    }

    public StoredToken? Load(SymplrEnvironment env)
    {
        var config = ReadConfig();
        config.Sessions.TryGetValue(Key(env), out var token);
        return token;
    }

    public SymplrEnvironment? ActiveEnvironment()
    {
        var raw = ReadConfig().ActiveEnvironment;
        return raw is not null && Enum.TryParse<SymplrEnvironment>(raw, ignoreCase: true, out var env) ? env : null;
    }

    public void Save(SymplrEnvironment env, StoredToken token)
    {
        var config = ReadConfig();
        config.Sessions[Key(env)] = token;
        config.ActiveEnvironment = Key(env);
        WriteConfig(config);
    }

    public void Remove(SymplrEnvironment env)
    {
        var config = ReadConfig();
        config.Sessions.Remove(Key(env));
        if (string.Equals(config.ActiveEnvironment, Key(env), StringComparison.OrdinalIgnoreCase))
            config.ActiveEnvironment = config.Sessions.Keys.FirstOrDefault();
        WriteConfig(config);
    }

    public void SetActive(SymplrEnvironment env)
    {
        var config = ReadConfig();
        config.ActiveEnvironment = Key(env);
        WriteConfig(config);
    }

    public IReadOnlyDictionary<string, StoredToken> All() => ReadConfig().Sessions;

    private static string Key(SymplrEnvironment env) => env.ToString().ToLowerInvariant();

    private Config ReadConfig()
    {
        if (!File.Exists(ConfigPath)) return new Config();
        try { return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath), JsonOptions) ?? new Config(); }
        catch { return new Config(); }
    }

    private void WriteConfig(Config config)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
    }
}
