using System.ComponentModel;
using GrampsDbTool.Models;
using GrampsDbTool.Services;
using ModelContextProtocol.Server;

namespace GrampsDbTool.Tools;

[McpServerToolType]
public sealed class ExportTools(FanChartExportService fanChartExportService)
{
    [McpServerTool(Name = "export_webtrees_fan_chart", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description(
        "Export the complete current Gramps library as the source/family.json format used by webtrees-fan-chart. Uses Gramps IDs as xrefs, returns the payload without writing a file, includes private records, and resolves the first existing image through the Gramps media-path metadata.")]
    public Task<FanChartExportDto> ExportWebtreesFanChart(
        [Description("Optional Gramps person handle for config.defaultXref. Defaults to the Gramps home person, then the first person.")]
        string? defaultPersonHandle = null,
        [Description("Optional user-visible Gramps person ID for config.defaultXref. Supply instead of defaultPersonHandle.")]
        string? defaultPersonGrampsId = null,
        CancellationToken cancellationToken = default)
    {
        return fanChartExportService.ExportAsync(defaultPersonHandle, defaultPersonGrampsId, cancellationToken);
    }
}
