namespace PhotoArchive.PersistenceSpike;

public sealed record ArchiveManifest(
    Guid AssetId,
    DateTimeOffset CaptureDate,
    string MediaKind,
    DateTimeOffset ImportedAt,
    bool IsFavorite,
    IReadOnlyList<MediaResourceManifest> Resources,
    IReadOnlyList<SourceObservationManifest> SourceObservations);

public sealed record MediaResourceManifest(
    Guid ResourceId,
    string Role,
    string RelativePath,
    string Sha256,
    long ByteLength);

public sealed record SourceObservationManifest(
    Guid DeviceId,
    string DisplayName,
    string ConnectorType,
    string SourceAssetIdentifier,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);
