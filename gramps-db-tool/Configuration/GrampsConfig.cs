namespace GrampsDbTool.Configuration;

public sealed record GrampsConfig(
    string ConfigDirectory,
    string DatabasePath,
    string? BackupPath
);

public sealed record GrampsDatabasePaths(
    string MediaBasePath,
    string? SavePath
);

public sealed record RuntimeOptions(
    string ConfigPath,
    bool AllowWrites
);
