using GrampsDbTool.Configuration;
using GrampsDbTool.Data;
using GrampsDbTool.Safety;
using GrampsDbTool.Services;
using GrampsDbTool.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var runtimeOptions = ConfigLoader.LoadRuntimeOptions(args);
var grampsConfig = ConfigLoader.LoadConfig(runtimeOptions.ConfigPath);
var databasePaths = await new GrampsMetadataReader(grampsConfig).ReadDatabasePathsAsync();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(runtimeOptions);
builder.Services.AddSingleton(grampsConfig);
builder.Services.AddSingleton(databasePaths);
builder.Services.AddSingleton<WriteGuard>();
builder.Services.AddSingleton<IMediaPathService, MediaPathService>();
builder.Services.AddSingleton<GrampsRepository>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithTools<PersonTools>()
    .WithTools<MediaTools>()
    .WithTools<NoteTools>()
    .WithTools<CitationTools>();

var app = builder.Build();

app.MapMcp("/gramps");

await app.RunAsync();
