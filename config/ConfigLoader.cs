using System.Text.Json;

public static class ConfigLoader
{
    static string GetConfigPath()
    {
        // Basisverzeichnis (bin/Debug/...)
        var baseDir = AppContext.BaseDirectory;

        // 👉 Config liegt im Output unter /Config/
        var path = Path.Combine(baseDir, "Config", "config.json");

        return path;
    }

    public static AppConfig Load()
    {
        var path = GetConfigPath();

        if (!File.Exists(path))
            throw new FileNotFoundException($"Config not found: {path}");

        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<AppConfig>(json)!;
    }

    public static void Save(AppConfig config)
    {
        var path = GetConfigPath();

        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(path, json);
    }
}