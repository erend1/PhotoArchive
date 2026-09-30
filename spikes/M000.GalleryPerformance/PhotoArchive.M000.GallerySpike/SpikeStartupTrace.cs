namespace PhotoArchive.M000.GallerySpike;

internal static class SpikeStartupTrace
{
    private static readonly object Gate = new();
    private static string? _tracePath;

    public static void Write(string message)
    {
        try
        {
            var path = GetTracePath();
            if (path is null)
            {
                return;
            }

            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostic tracing must never change spike behavior.
        }
    }

    private static string? GetTracePath()
    {
        if (_tracePath is not null)
        {
            return _tracePath;
        }

        var reportArg = Environment.GetCommandLineArgs()
            .FirstOrDefault(static arg => arg.StartsWith("--report=", StringComparison.OrdinalIgnoreCase));
        if (reportArg is null)
        {
            return null;
        }

        var reportPath = Path.GetFullPath(reportArg["--report=".Length..].Trim('"'));
        _tracePath = Path.Combine(Path.GetDirectoryName(reportPath)!, "startup-trace.log");
        return _tracePath;
    }
}
