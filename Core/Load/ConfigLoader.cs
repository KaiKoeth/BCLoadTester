using System.Text.Json;

public static class ConfigLoader
{
    static string GetConfigPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Config", "config.json");
    }

    public static AppConfig Load()
    {
        var path = GetConfigPath();

        if (!File.Exists(path))
            throw new FileNotFoundException($"Config not found: {path}");

        var json = File.ReadAllText(path);

        var config = JsonSerializer.Deserialize<AppConfig>(json)!;

        // 🔥 NEU: ConnectionString → Felder parsen
        ParseConnectionString(config);

        return config;
    }

    public static void Save(AppConfig config)
    {
        var path = GetConfigPath();

        // 🔥 NEU: Felder → ConnectionString bauen
        BuildConnectionString(config);

        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(path, json);
    }

    // =========================
    // 🔧 HELPER
    // =========================

    private static void ParseConnectionString(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.connectionString))
            return;

        var parts = config.connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;

            var key = kv[0].Trim().ToLower();
            var value = kv[1].Trim();

            switch (key)
            {
                case "server":
                    var split = value.Split(',');

                    config.sqlServer = split[0];

                    if (split.Length > 1 && int.TryParse(split[1], out int port))
                        config.sqlPort = port;
                    break;

                case "database":
                    config.database = value;
                    break;

                case "user id":
                case "uid":
                    config.dbUser = value;
                    break;

                case "password":
                case "pwd":
                    config.dbPassword = value;
                    break;
            }
        }
    }

    private static void BuildConnectionString(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.sqlServer))
            return;

        var portPart = config.sqlPort > 0 ? $",{config.sqlPort}" : "";

        config.connectionString =
            $"Server={config.sqlServer}{portPart};" +
            $"Database={config.database};" +
            $"User Id={config.dbUser};" +
            $"Password={config.dbPassword};" +
            $"TrustServerCertificate=True;";
    }

    public static void SaveAs(AppConfig config, string path)
    {
        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(path, json);
    }

    public static AppConfig LoadFrom(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config not found: {path}");

        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<AppConfig>(json)!;
    }
}