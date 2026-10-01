using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PhotoArchive.Spikes.AppleDevice.Output;

/// <summary>Accumulates sanitized findings and renders them as Markdown (for the GitHub issue) and JSON.</summary>
internal sealed class ReportDocument
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<ReportSection> Sections { get; } = [];

    public ReportSection Section(string title)
    {
        var section = new ReportSection(title);
        Sections.Add(section);
        return section;
    }

    public string ToMarkdown(string heading)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {heading}");
        sb.AppendLine();
        foreach (var section in Sections)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"## {section.Title}");
            sb.AppendLine();
            if (section.Facts.Count > 0)
            {
                sb.AppendLine("| Fact | Value |");
                sb.AppendLine("|---|---|");
                foreach (var (key, value) in section.Facts)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"| {Escape(key)} | {Escape(Format(value))} |");
                }

                sb.AppendLine();
            }

            foreach (var line in section.Lines)
            {
                sb.AppendLine(line);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public string ToJson() =>
        JsonSerializer.Serialize(
            Sections.Select(s => new { s.Title, Facts = s.Facts.ToDictionary(kv => kv.Key, kv => Format(kv.Value)), s.Lines }),
            JsonOptions);

    internal static string Format(object? value) => value switch
    {
        null => "(not available)",
        bool b => b ? "yes" : "no",
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ").Replace("\n", " ");
}

internal sealed class ReportSection(string title)
{
    public string Title { get; } = title;

    public SortedDictionary<string, object?> Facts { get; } = new(StringComparer.Ordinal);

    public List<string> Lines { get; } = [];

    public ReportSection Fact(string key, object? value)
    {
        Facts[key] = value;
        return this;
    }

    public ReportSection Line(string markdown)
    {
        Lines.Add(markdown);
        return this;
    }

    public ReportSection Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        Lines.Add("| " + string.Join(" | ", headers) + " |");
        Lines.Add("|" + string.Concat(Enumerable.Repeat("---|", headers.Count)));
        foreach (var row in rows)
        {
            Lines.Add("| " + string.Join(" | ", row.Select(c => c.Replace("|", "\\|", StringComparison.Ordinal))) + " |");
        }

        Lines.Add(string.Empty);
        return this;
    }
}
