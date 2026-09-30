using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace PhotoArchive.PersistenceSpike;

public sealed class ArchiveCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task CreateAsync(string archiveRoot, CancellationToken cancellationToken = default)
    {
        PrepareLayout(archiveRoot);
        await MigrateWithBackupAsync(archiveRoot, cancellationToken);
    }

    public async Task OpenAsync(string archiveRoot, CancellationToken cancellationToken = default)
    {
        var archiveJsonPath = Path.Combine(ArchivePaths.MetadataDirectory(archiveRoot), "archive.json");
        if (!File.Exists(archiveJsonPath))
        {
            throw new InvalidOperationException("The selected folder is not an initialized PhotoArchive archive.");
        }

        await MigrateWithBackupAsync(archiveRoot, cancellationToken);
    }

    public async Task<string?> MigrateWithBackupAsync(
        string archiveRoot,
        CancellationToken cancellationToken = default)
    {
        PrepareLayout(archiveRoot);

        string[] pendingMigrations;
        await using (var inspectionContext = ArchiveDbContextFactory.Create(archiveRoot))
        {
            pendingMigrations = (await inspectionContext.Database
                .GetPendingMigrationsAsync(cancellationToken))
                .ToArray();
        }

        if (pendingMigrations.Length == 0)
        {
            return null;
        }

        string? backupPath = null;
        var catalogPath = ArchivePaths.CatalogPath(archiveRoot);
        if (File.Exists(catalogPath) && new FileInfo(catalogPath).Length > 0)
        {
            Directory.CreateDirectory(ArchivePaths.BackupsDirectory(archiveRoot));
            backupPath = Path.Combine(
                ArchivePaths.BackupsDirectory(archiveRoot),
                $"catalog.pre-migration.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.sqlite");
            File.Copy(catalogPath, backupPath, overwrite: false);
        }

        await using var migrationContext = ArchiveDbContextFactory.Create(archiveRoot);
        await migrationContext.Database.MigrateAsync(cancellationToken);
        return backupPath;
    }

    public async Task WriteManifestAsync(
        string archiveRoot,
        ArchiveManifest manifest,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ArchivePaths.ManifestsDirectory(archiveRoot));
        ValidateManifestPaths(archiveRoot, manifest);

        var destination = ManifestPath(archiveRoot, manifest.AssetId);
        var temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
        }

        File.Move(temporary, destination, overwrite: true);
    }

    public async Task RebuildCatalogAsync(string archiveRoot, CancellationToken cancellationToken = default)
    {
        PrepareLayout(archiveRoot);
        DeleteCatalogFiles(archiveRoot);

        await using (var schemaContext = ArchiveDbContextFactory.Create(archiveRoot))
        {
            await schemaContext.Database.MigrateAsync(cancellationToken);
        }

        var manifests = await ReadManifestsAsync(archiveRoot, cancellationToken);
        await using var rebuildContext = ArchiveDbContextFactory.Create(archiveRoot);

        foreach (var manifest in manifests)
        {
            ValidateManifestPaths(archiveRoot, manifest);

            var asset = new ArchiveAsset
            {
                AssetId = manifest.AssetId,
                CaptureDate = manifest.CaptureDate,
                MediaKind = manifest.MediaKind,
                ImportedAt = manifest.ImportedAt,
                IsFavorite = manifest.IsFavorite,
                LastIndexedAtUtc = null
            };

            foreach (var resource in manifest.Resources)
            {
                asset.Resources.Add(new MediaResource
                {
                    ResourceId = resource.ResourceId,
                    AssetId = manifest.AssetId,
                    Role = resource.Role,
                    RelativePath = resource.RelativePath,
                    Sha256 = resource.Sha256,
                    ByteLength = resource.ByteLength
                });
            }

            foreach (var observation in manifest.SourceObservations)
            {
                var device = await rebuildContext.SourceDevices.FindAsync(
                    [observation.DeviceId],
                    cancellationToken);

                if (device is null)
                {
                    device = new SourceDevice
                    {
                        DeviceId = observation.DeviceId,
                        DisplayName = observation.DisplayName,
                        ConnectorType = observation.ConnectorType
                    };
                    rebuildContext.SourceDevices.Add(device);
                }

                asset.SourceObservations.Add(new SourceObservation
                {
                    DeviceId = observation.DeviceId,
                    AssetId = manifest.AssetId,
                    SourceAssetIdentifier = observation.SourceAssetIdentifier,
                    FirstSeenAt = observation.FirstSeenAt,
                    LastSeenAt = observation.LastSeenAt,
                    Device = device
                });
            }

            rebuildContext.ArchiveAssets.Add(asset);
        }

        await rebuildContext.SaveChangesAsync(cancellationToken);
    }

    private static void PrepareLayout(string archiveRoot)
    {
        Directory.CreateDirectory(Path.GetFullPath(archiveRoot));
        Directory.CreateDirectory(Path.Combine(archiveRoot, "Media"));
        Directory.CreateDirectory(ArchivePaths.MetadataDirectory(archiveRoot));
        Directory.CreateDirectory(ArchivePaths.ManifestsDirectory(archiveRoot));

        var archiveJsonPath = Path.Combine(ArchivePaths.MetadataDirectory(archiveRoot), "archive.json");
        if (!File.Exists(archiveJsonPath))
        {
            var archiveMetadata = JsonSerializer.Serialize(new { formatVersion = 1 }, JsonOptions);
            File.WriteAllText(archiveJsonPath, archiveMetadata);
        }
    }

    private static async Task<IReadOnlyList<ArchiveManifest>> ReadManifestsAsync(
        string archiveRoot,
        CancellationToken cancellationToken)
    {
        var results = new List<ArchiveManifest>();
        foreach (var file in Directory.EnumerateFiles(
                     ArchivePaths.ManifestsDirectory(archiveRoot),
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            await using var stream = File.OpenRead(file);
            var manifest = await JsonSerializer.DeserializeAsync<ArchiveManifest>(
                stream,
                JsonOptions,
                cancellationToken);
            if (manifest is null)
            {
                throw new InvalidDataException($"Manifest '{file}' could not be read.");
            }

            results.Add(manifest);
        }

        return results;
    }

    private static string ManifestPath(string archiveRoot, Guid assetId) =>
        Path.Combine(ArchivePaths.ManifestsDirectory(archiveRoot), $"{assetId:N}.json");

    private static void ValidateManifestPaths(string archiveRoot, ArchiveManifest manifest)
    {
        foreach (var resource in manifest.Resources)
        {
            _ = ArchivePaths.ResolveArchiveRelative(archiveRoot, resource.RelativePath);
        }
    }

    private static void DeleteCatalogFiles(string archiveRoot)
    {
        var catalogPath = ArchivePaths.CatalogPath(archiveRoot);
        foreach (var path in new[] { catalogPath, $"{catalogPath}-wal", $"{catalogPath}-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
