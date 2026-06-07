using GrampsDbTool.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(new GrampsDatabaseOptions(GetDatabasePath(args), WritesEnabled(args), GetMediaDirectory(args)));
builder.Services.AddSingleton<GrampsContext>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapMcp();
await app.RunAsync();

static string? GetDatabasePath(string[] args)
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

    return Environment.GetEnvironmentVariable("GRAMPS_SQLITE_PATH");
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

static string? GetMediaDirectory(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--media-dir" && i + 1 < args.Length)
        {
            return args[i + 1];
        }

        const string mediaDirectoryPrefix = "--media-dir=";
        if (args[i].StartsWith(mediaDirectoryPrefix, StringComparison.Ordinal))
        {
            return args[i][mediaDirectoryPrefix.Length..];
        }
    }

    return Environment.GetEnvironmentVariable("GRAMPS_MEDIA_DIR");
}
