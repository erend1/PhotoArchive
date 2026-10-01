using System.Globalization;
using System.Text;

namespace PhotoArchive.Spikes.AppleDevice.Output;

/// <summary>
/// Per-invocation output context. All artifacts go to a run folder OUTSIDE the repository by default
/// (%LOCALAPPDATA%\PhotoArchive\M000-AppleDevice\runs\...) so personal media/metadata cannot be committed by accident.
/// </summary>
internal sealed class RunContext : IDisposable
{
    private readonly StreamWriter _log;

    private RunContext(string runDirectory, Sanitizer sanitizer, bool writeRaw)
    {
        RunDirectory = runDirectory;
        Sanitizer = sanitizer;
        WriteRaw = writeRaw;
        Directory.CreateDirectory(runDirectory);
        _log = new StreamWriter(Path.Combine(runDirectory, "probe.log"), append: true, new UTF8Encoding(false)) { AutoFlush = true };
    }

    public string RunDirectory { get; }

    public Sanitizer Sanitizer { get; }

    /// <summary>When true, unsanitized per-object data is written under <c>raw/</c>. Never commit that folder.</summary>
    public bool WriteRaw { get; }

    public ReportDocument Report { get; } = new();

    internal static string DefaultRunsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoArchive", "M000-AppleDevice", "runs");

    internal static RunContext Create(string? outputRoot, string command, bool sanitize, bool writeRaw)
    {
        var root = string.IsNullOrWhiteSpace(outputRoot) ? DefaultRunsRoot : Path.GetFullPath(outputRoot);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var dir = Path.Combine(root, $"{stamp}-{command}");
        return new RunContext(dir, new Sanitizer(sanitize), writeRaw);
    }

    public string RawDirectory
    {
        get
        {
            var dir = Path.Combine(RunDirectory, "raw");
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "README-DO-NOT-COMMIT.txt"),
                "This folder contains unsanitized device metadata and/or media bytes copied from a personal device.\r\n" +
                "Do not commit, upload, or attach it to public issues.\r\n");
            return dir;
        }
    }

    public void Info(string message) => Write("INFO", message, ConsoleColor.Gray);

    public void Good(string message) => Write("OK", message, ConsoleColor.Green);

    public void Warn(string message) => Write("WARN", message, ConsoleColor.Yellow);

    public void Error(string message) => Write("ERROR", message, ConsoleColor.Red);

    public string SaveReport(string heading)
    {
        var md = Path.Combine(RunDirectory, "report.md");
        var json = Path.Combine(RunDirectory, "report.json");
        File.WriteAllText(md, Report.ToMarkdown(heading), new UTF8Encoding(false));
        File.WriteAllText(json, Report.ToJson(), new UTF8Encoding(false));
        return md;
    }

    public void Dispose() => _log.Dispose();

    private void Write(string level, string message, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(level == "INFO" ? message : $"[{level}] {message}");
        Console.ForegroundColor = previous;
        _log.WriteLine($"{DateTime.Now:O} [{level}] {message}");
    }
}
