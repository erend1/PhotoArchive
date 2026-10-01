using PhotoArchive.Spikes.AppleDevice.Analysis;
using PhotoArchive.Spikes.AppleDevice.Cli;
using PhotoArchive.Spikes.AppleDevice.Output;

namespace PhotoArchive.Spikes.AppleDevice.Tests;

public sealed class JpegInfoTests
{
    [Fact]
    public void Reads_dimensions_from_sof0_after_app_segments()
    {
        byte[] jpeg =
        [
            0xFF, 0xD8,
            0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, // APP0 (length 4)
            0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x78, 0x00, 0xA0, 0x03, // SOF0: 8-bit, height 120, width 160
            0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
            0xFF, 0xD9,
        ];

        Assert.Equal((160, 120), JpegInfo.TryReadDimensions(jpeg));
    }

    [Fact]
    public void Returns_null_for_non_jpeg_or_truncated_input()
    {
        Assert.Null(JpegInfo.TryReadDimensions([0x89, 0x50, 0x4E, 0x47]));
        Assert.Null(JpegInfo.TryReadDimensions([0xFF, 0xD8, 0xFF, 0xE0, 0x00]));
    }
}

public sealed class CommandLineTests
{
    [Fact]
    public void Parses_command_options_flags_and_positionals()
    {
        var cli = CommandLine.Parse(["Identity-Compare", "a.json", "b.json", "--device", "1", "--raw", "--out=C:\\runs"]);

        Assert.Equal("identity-compare", cli.Command);
        Assert.Equal(["a.json", "b.json"], cli.Positionals);
        Assert.Equal(1, cli.GetInt("device", -1));
        Assert.True(cli.Has("raw"));
        Assert.Null(cli.Get("raw"));
        Assert.Equal("C:\\runs", cli.Get("out"));
        Assert.Equal(7, cli.GetInt("missing", 7));
    }

    [Fact]
    public void Missing_command_means_help()
    {
        Assert.Equal("help", CommandLine.Parse([]).Command);
        Assert.Equal("help", CommandLine.Parse(["--device", "0"]).Command);
        Assert.Equal(0, CommandLine.Parse(["--device", "0"]).GetInt("device", -1));
    }
}

public sealed class ReportDocumentTests
{
    [Fact]
    public void Markdown_escapes_table_cells_and_formats_values_invariantly()
    {
        var report = new ReportDocument();
        report.Section("S")
            .Fact("pipe|key", "a|b")
            .Fact("flag", true)
            .Fact("missing", null)
            .Table(["H1", "H2"], [["x|y", "z"]]);

        var md = report.ToMarkdown("Title");

        Assert.Contains("| pipe\\|key | a\\|b |", md, StringComparison.Ordinal);
        Assert.Contains("| flag | yes |", md, StringComparison.Ordinal);
        Assert.Contains("| missing | (not available) |", md, StringComparison.Ordinal);
        Assert.Contains("| x\\|y | z |", md, StringComparison.Ordinal);
        Assert.Contains("\"Title\": \"S\"", report.ToJson(), StringComparison.Ordinal);
    }
}
