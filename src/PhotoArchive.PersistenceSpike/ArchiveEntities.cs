namespace PhotoArchive.PersistenceSpike;

public sealed class ArchiveAsset
{
    public Guid AssetId { get; set; }
    public DateTimeOffset CaptureDate { get; set; }
    public string MediaKind { get; set; } = string.Empty;
    public DateTimeOffset ImportedAt { get; set; }
    public bool IsFavorite { get; set; }
    public DateTimeOffset? LastIndexedAtUtc { get; set; }

    public ICollection<MediaResource> Resources { get; set; } = new List<MediaResource>();
    public ICollection<SourceObservation> SourceObservations { get; set; } = new List<SourceObservation>();
}

public sealed class MediaResource
{
    public Guid ResourceId { get; set; }
    public Guid AssetId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long ByteLength { get; set; }

    public ArchiveAsset Asset { get; set; } = null!;
}

public sealed class SourceDevice
{
    public Guid DeviceId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ConnectorType { get; set; } = string.Empty;

    public ICollection<SourceObservation> Observations { get; set; } = new List<SourceObservation>();
}

public sealed class SourceObservation
{
    public long ObservationId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid AssetId { get; set; }
    public string SourceAssetIdentifier { get; set; } = string.Empty;
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public SourceDevice Device { get; set; } = null!;
    public ArchiveAsset Asset { get; set; } = null!;
}
