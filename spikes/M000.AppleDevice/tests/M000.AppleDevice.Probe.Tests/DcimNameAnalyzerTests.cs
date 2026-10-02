using PhotoArchive.Spikes.AppleDevice.Analysis;

namespace PhotoArchive.Spikes.AppleDevice.Tests;

public sealed class DcimNameAnalyzerTests
{
    [Theory]
    [InlineData("IMG_1234.HEIC", "IMG_", "", "1234", "HEIC", nameof(DcimResourceKind.Still))]
    [InlineData("IMG_E1234.HEIC", "IMG_", "E", "1234", "HEIC", nameof(DcimResourceKind.Still))]
    [InlineData("IMG_1234.MOV", "IMG_", "", "1234", "MOV", nameof(DcimResourceKind.Video))]
    [InlineData("IMG_E1234.MOV", "IMG_", "E", "1234", "MOV", nameof(DcimResourceKind.Video))]
    [InlineData("IMG_1234.AAE", "IMG_", "", "1234", "AAE", nameof(DcimResourceKind.AdjustmentSidecar))]
    [InlineData("IMG_O1234.AAE", "IMG_", "O", "1234", "AAE", nameof(DcimResourceKind.AdjustmentSidecar))]
    [InlineData("img_0001.jpg", "img_", "", "0001", "JPG", nameof(DcimResourceKind.Still))]
    [InlineData("IMG_1234.DNG", "IMG_", "", "1234", "DNG", nameof(DcimResourceKind.Still))]
    [InlineData("IMG_1234.XYZ", "IMG_", "", "1234", "XYZ", nameof(DcimResourceKind.Other))]
    public void Parse_recognizes_dcim_names(string name, string stem, string marker, string number, string extension, string kind)
    {
        var parsed = DcimNameAnalyzer.Parse(name);

        Assert.NotNull(parsed);
        Assert.Equal(stem, parsed.Stem);
        Assert.Equal(marker, parsed.Marker);
        Assert.Equal(number, parsed.Number);
        Assert.Equal(extension, parsed.Extension);
        Assert.Equal(Enum.Parse<DcimResourceKind>(kind), parsed.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DCIM")]
    [InlineData("notes.txt")]
    [InlineData("IMG_12.JPG")]
    public void Parse_returns_null_for_non_dcim_names(string? name) => Assert.Null(DcimNameAnalyzer.Parse(name));

    [Fact]
    public void Summarize_derives_candidate_relationships_per_folder()
    {
        var files = new (string?, string?)[]
        {
            // Live Photo candidate (still + motion, same number).
            ("f1", "IMG_0001.HEIC"), ("f1", "IMG_0001.MOV"),
            // Edited photo with adjustment sidecar and exposed original.
            ("f1", "IMG_0002.HEIC"), ("f1", "IMG_E0002.HEIC"), ("f1", "IMG_0002.AAE"),
            // Edited render WITHOUT an exposed original (completeness red flag).
            ("f1", "IMG_E0003.JPG"),
            // Orphan adjustment sidecar.
            ("f1", "IMG_0004.AAE"),
            // Plain video and plain still.
            ("f1", "IMG_0005.MOV"), ("f1", "IMG_0006.PNG"),
            // Same number in another folder must not be grouped with f1.
            ("f2", "IMG_0001.MOV"),
            // Two original stills with the same number.
            ("f2", "IMG_0007.HEIC"), ("f2", "IMG_0007.JPG"),
            ("f2", "README.TXT"),
        };

        var summary = DcimNameAnalyzer.Summarize(files);

        Assert.Equal(13, summary.TotalFiles);
        Assert.Equal(1, summary.UnparsedFiles);
        Assert.Equal(8, summary.Groups);
        Assert.Equal(1, summary.LivePhotoCandidates);
        Assert.Equal(3, summary.StillOnlyGroups);
        Assert.Equal(2, summary.EditedRenderGroups);
        Assert.Equal(1, summary.EditedRenderWithAdjustmentGroups);
        Assert.Equal(1, summary.EditedRenderWithoutOriginalGroups);
        Assert.Equal(2, summary.AdjustmentSidecarGroups);
        Assert.Equal(1, summary.OrphanAdjustmentGroups);
        Assert.Equal(2, summary.VideoOnlyGroups);
        Assert.Equal(1, summary.MultipleOriginalStillGroups);
        Assert.Equal(2, summary.ExtensionCounts["E:HEIC"] + summary.ExtensionCounts["E:JPG"]);
    }

    [Theory]
    [InlineData("202409__", "DDDDDD__")]
    [InlineData("100APPLE", "DDDAAAAA")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Shape_keeps_only_character_classes(string? value, string expected) => Assert.Equal(expected, DcimNameAnalyzer.Shape(value));
}
