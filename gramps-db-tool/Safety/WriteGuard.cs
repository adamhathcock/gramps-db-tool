using GrampsDbTool.Configuration;

namespace GrampsDbTool.Safety;

public sealed class WriteGuard(RuntimeOptions options)
{
    public bool AllowWrites => options.AllowWrites;

    public void RequireWritesEnabled()
    {
        if (!AllowWrites)
        {
            throw new InvalidOperationException("Writes are disabled. Start with --allow-writes or GRAMPS_ALLOW_WRITES=1 to enable write tools.");
        }
    }
}
