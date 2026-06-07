using GrampsDbTool.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var config = LoadConfig(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(new GrampsDatabaseOptions(GetDatabasePath(args, config), WritesEnabled(args)));
builder.Services.AddSingleton<GrampsContext>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapMcp();
await app.RunAsync();

static string? GetDatabasePath(string[] args, GrampsToolConfig? config)
{
    for (var i = 0; i < args.Length; i++)
    {
        if ((args[i] == "--database" || args[i] == "-d") && i + 1 < args.Length)
        {
            return args[i + 1];
        }

        const string databasePrefix = "--database=";
        if (args[i].StartsWith(databasePrefix, StringComparison.Ordinal))
        {
            return args[i][databasePrefix.Length..];
        }
    }

    return Environment.GetEnvironmentVariable("GRAMPS_SQLITE_PATH") ?? config?.DatabasePath;
}

static bool WritesEnabled(string[] args)
{
    if (args.Contains("--allow-writes", StringComparer.Ordinal))
    {
        return true;
    }

    var value = Environment.GetEnvironmentVariable("GRAMPS_ALLOW_WRITES");
    return value is not null
        && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
}

static GrampsToolConfig? LoadConfig(string[] args)
{
    var explicitConfigPath = false;
    var configPath = GetConfigPath(args, out explicitConfigPath);

    if (configPath is null)
    {
        return null;
    }

    if (!File.Exists(configPath))
    {
        if (explicitConfigPath)
        {
            throw new FileNotFoundException("Configured Gramps DB tool config file was not found.", configPath);
        }

        return null;
    }

    try
    {
        return JsonSerializer.Deserialize<GrampsToolConfig>(File.ReadAllText(configPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    catch (JsonException exception)
    {
        throw new InvalidOperationException($"Config file is not valid JSON: {configPath}", exception);
    }
}

static string? GetConfigPath(string[] args, out bool explicitConfigPath)
{
    explicitConfigPath = true;
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--config" && i + 1 < args.Length)
        {
            return args[i + 1];
        }

        const string configPrefix = "--config=";
        if (args[i].StartsWith(configPrefix, StringComparison.Ordinal))
        {
            return args[i][configPrefix.Length..];
        }
    }

    if (Environment.GetEnvironmentVariable("GRAMPS_DB_TOOL_CONFIG") is { Length: > 0 } configPath)
    {
        return configPath;
    }

    explicitConfigPath = false;
    return Path.Combine(Directory.GetCurrentDirectory(), "gramps-db-tool.json");
}

public sealed record GrampsToolConfig(string? DatabasePath);
