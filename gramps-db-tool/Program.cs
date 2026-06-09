using GrampsDbTool.Configuration;
using GrampsDbTool.Data;
using GrampsDbTool.Safety;
using GrampsDbTool.Services;
using GrampsDbTool.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var runtimeOptions = ConfigLoader.LoadRuntimeOptions(args);
var grampsConfig = ConfigLoader.LoadConfig(runtimeOptions.ConfigPath);
var databasePaths = await new GrampsMetadataReader(grampsConfig).ReadDatabasePathsAsync();
if (string.IsNullOrEmpty(databasePaths.SavePath))
{
    databasePaths =  databasePaths with { SavePath = grampsConfig.BackupPath };
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(runtimeOptions);
builder.Services.AddSingleton(grampsConfig);
builder.Services.AddSingleton(databasePaths);
builder.Services.AddSingleton<WriteGuard>();
builder.Services.AddSingleton<SingleWriterLock>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddSingleton<GrampsConnectionFactory>();
builder.Services.AddSingleton<IMediaPathService, MediaPathService>();
builder.Services.AddSingleton<GrampsRepository>();
builder.Services.AddSingleton<MediaWriteService>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithTools<PersonTools>()
    .WithTools<BackupTools>()
    .WithTools<MediaTools>()
    .WithTools<NoteTools>()
    .WithTools<CitationTools>()
    .WithTools<FamilyTools>()
    .WithTools<EventTools>()
    .WithTools<SourceTools>();

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

logger.LogInformation("Using Gramps DB Tool config: {ConfigPath}", runtimeOptions.ConfigPath);
logger.LogInformation("Using Gramps SQLite database: {DatabasePath}", grampsConfig.DatabasePath);
logger.LogInformation("Using Gramps media base path: {MediaBasePath}", databasePaths.MediaBasePath);
logger.LogInformation("Using Gramps backup path: {BackupPath}", databasePaths.SavePath);
logger.LogInformation("Write tools are {WriteMode}", runtimeOptions.AllowWrites ? "enabled" : "disabled");

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    databaseConfigured = !string.IsNullOrWhiteSpace(grampsConfig.DatabasePath),
    mediaBasePathConfigured = !string.IsNullOrWhiteSpace(databasePaths.MediaBasePath),
    writesEnabled = runtimeOptions.AllowWrites
}));
app.MapMcp("/gramps");

await app.RunAsync();
