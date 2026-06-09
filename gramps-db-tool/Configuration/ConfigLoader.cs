using System.Text.Json;

namespace GrampsDbTool.Configuration;

public static class ConfigLoader
{
    private const string ConfigEnvironmentVariable = "GRAMPS_DB_TOOL_CONFIG";
    private const string AllowWritesEnvironmentVariable = "GRAMPS_ALLOW_WRITES";

    public static RuntimeOptions LoadRuntimeOptions(string[] args)
    {
        string? configPath = null;
        var allowWrites = IsTruthy(Environment.GetEnvironmentVariable(AllowWritesEnvironmentVariable));

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--config")
            {
                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException("--config requires a path.");
                }

                configPath = args[++i];
                continue;
            }

            if (arg.StartsWith("--config=", StringComparison.Ordinal))
            {
                configPath = arg["--config=".Length..];
                continue;
            }

            if (arg == "--allow-writes")
            {
                allowWrites = true;
                continue;
            }

            if (arg == "--database" || arg == "-d" || arg.StartsWith("--database=", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Database path must be set with databasePath in gramps-db-tool.json, not CLI arguments.");
            }
        }

        configPath ??= Environment.GetEnvironmentVariable(ConfigEnvironmentVariable);
        configPath = string.IsNullOrWhiteSpace(configPath) ? "gramps-db-tool.json" : configPath;

        return new RuntimeOptions(Path.GetFullPath(configPath), allowWrites);
    }

    public static GrampsConfig LoadConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file not found: {configPath}", configPath);
        }

        using var stream = File.OpenRead(configPath);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        if (!root.TryGetProperty("databasePath", out var databasePathElement) || databasePathElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Config file {configPath} must contain a string databasePath property.");
        }

        var databasePath = databasePathElement.GetString();
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new InvalidOperationException($"Config file {configPath} has an empty databasePath property.");
        }

        if (root.TryGetProperty("allowWrites", out _))
        {
            throw new InvalidOperationException("allowWrites is not supported in config. Use --allow-writes or GRAMPS_ALLOW_WRITES for runtime write enablement.");
        }

        string? backupPath = null;
        if (root.TryGetProperty("backupPath", out var backupPathElement))
        {
            if (backupPathElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException($"Config file {configPath} has a non-string backupPath property.");
            }

            backupPath = backupPathElement.GetString();
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                throw new InvalidOperationException($"Config file {configPath} has an empty backupPath property.");
            }
        }

        var configDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Directory.GetCurrentDirectory();
        var resolvedDatabasePath = Path.IsPathRooted(databasePath)
            ? databasePath
            : Path.GetFullPath(databasePath, configDirectory);
        var resolvedBackupPath = backupPath is null
            ? null
            : Path.IsPathRooted(backupPath)
                ? backupPath
                : Path.GetFullPath(backupPath, configDirectory);

        return new GrampsConfig(configDirectory, resolvedDatabasePath, resolvedBackupPath);
    }

    private static bool IsTruthy(string? value)
    {
        return value is not null &&
            (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}
