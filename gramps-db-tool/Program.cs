using GrampsDbTool.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(new GrampsDatabaseOptions(GetDatabasePath(args)));
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
