using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PhotoArchive.PersistenceSpike;

namespace PhotoArchive.PersistenceSpike.Tests;

public sealed class ArchivePortabilityIntegrationTests
{
    private readonly ArchiveCatalog _catalog = new();

    [Fact]
    public async Task NewArchiveCreation_InitializesLocalSqliteWithoutExternalProvisioning()
    {
        using var sandbox = new TemporaryDirectory();
        var root = sandbox.Combine("ArchiveA");

        await _catalog.CreateAsync(root);

        Assert.True(File.Exists(ArchivePaths.CatalogPath(root)));
        Assert.True(File.Exists(Path.Combine(root, ".archive", "archive.json")));
        Assert.True(Directory.Exists(Path.Combine(root, "Media")));
        Assert.True(Directory.Exists(ArchivePaths.ManifestsDirectory(root)));

        await using var db = ArchiveDbContextFactory.Create(root);
        Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task ExistingArchive_ReopensWithoutCredentialsOrManualDatabaseSetup()
    {
        using var sandbox = new TemporaryDirectory();
        var root = sandbox.Combine("ArchiveA");
        var fixture = await CreatePopulatedArchiveAsync(root);

        await _catalog.OpenAsync(root);

        await using var db = ArchiveDbContextFactory.Create(root);
        var resource = await db.MediaResources.SingleAsync();
        Assert.Equal(fixture.Manifest.Resources.Single().RelativePath, resource.RelativePath);
    }

    [Fact]
    public async Task MovedArchive_ReopensAndResolvesMediaFromNewRoot()
    {
        using var sandbox = new TemporaryDirectory();
        var originalRoot = sandbox.Combine("first-root", "PhotoArchive");
        var fixture = await CreatePopulatedArchiveAsync(originalRoot);
        var movedRoot = sandbox.Combine("different-root", "nested", "PhotoArchive");
        Directory.CreateDirectory(Path.GetDirectoryName(movedRoot)!);

        Directory.Move(originalRoot, movedRoot);
        await _catalog.OpenAsync(movedRoot);

        await using var db = ArchiveDbContextFactory.Create(movedRoot);
        var resource = await db.MediaResources.SingleAsync();
        Assert.False(Path.IsPathRooted(resource.RelativePath));
        Assert.DoesNotContain(originalRoot, resource.RelativePath, StringComparison.OrdinalIgnoreCase);

        var resolved = ArchivePaths.ResolveArchiveRelative(movedRoot, resource.RelativePath);
        Assert.Equal(fixture.MediaBytes, await File.ReadAllBytesAsync(resolved));
    }

    [Fact]
    public async Task Migration_FromV1_CreatesRecoverableBackupAndDoesNotTouchMedia()
    {
        using var sandbox = new TemporaryDirectory();
        var root = sandbox.Combine("MigrationArchive");
        Directory.CreateDirectory(Path.Combine(root, "Media", "2026", "09"));
        Directory.CreateDirectory(ArchivePaths.ManifestsDirectory(root));
        await File.WriteAllTextAsync(Path.Combine(root, ".archive", "archive.json"), "{\"formatVersion\":1}");

        var mediaPath = Path.Combine(root, "Media", "2026", "09", "migration-sentinel.bin");
        var mediaBytes = new byte[] { 3, 1, 4, 1, 5, 9, 2, 6 };
        await File.WriteAllBytesAsync(mediaPath, mediaBytes);
        var mediaHashBefore = Hash(mediaBytes);

        await using (var v1Context = ArchiveDbContextFactory.Create(root))
        {
            var migrations = v1Context.Database.GetMigrations()
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(2, migrations.Length);

            var migrator = v1Context.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(migrations[0]);
            await v1Context.Database.ExecuteSqlRawAsync(
                "INSERT INTO ArchiveAssets (AssetId, CaptureDate, MediaKind, ImportedAt) VALUES ({0}, {1}, {2}, {3});",
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "Photo",
                DateTimeOffset.UtcNow);
        }

        var backupPath = await _catalog.MigrateWithBackupAsync(root);

        Assert.NotNull(backupPath);
        Assert.True(File.Exists(backupPath));
        Assert.True(new FileInfo(backupPath!).Length > 0);
        Assert.Equal(mediaHashBefore, Hash(await File.ReadAllBytesAsync(mediaPath)));

        await using (var currentContext = ArchiveDbContextFactory.Create(root))
        {
            Assert.Empty(await currentContext.Database.GetPendingMigrationsAsync());
            var columns = await ReadTableColumnsAsync(currentContext, "ArchiveAssets");
            Assert.Contains("IsFavorite", columns);
            Assert.Contains("LastIndexedAtUtc", columns);
        }

        await using var backupContext = CreateContextForDatabase(backupPath!);
        var backupColumns = await ReadTableColumnsAsync(backupContext, "ArchiveAssets");
        Assert.DoesNotContain("IsFavorite", backupColumns);
        Assert.Equal(1L, await ScalarInt64Async(backupContext, "SELECT COUNT(*) FROM ArchiveAssets;"));
    }

    [Fact]
    public async Task DeletedCatalog_RebuildsCoreRowsFromManifestWithoutMediaLoss()
    {
        using var sandbox = new TemporaryDirectory();
        var root = sandbox.Combine("RecoveryArchive");
        var fixture = await CreatePopulatedArchiveAsync(root, lastIndexedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-5));
        var mediaHashBefore = Hash(fixture.MediaBytes);

        File.Delete(ArchivePaths.CatalogPath(root));
        Assert.False(File.Exists(ArchivePaths.CatalogPath(root)));
        Assert.True(File.Exists(fixture.MediaPath));

        await _catalog.RebuildCatalogAsync(root);

        Assert.Equal(mediaHashBefore, Hash(await File.ReadAllBytesAsync(fixture.MediaPath)));
        await using var db = ArchiveDbContextFactory.Create(root);
        var asset = await db.ArchiveAssets
            .Include(x => x.Resources)
            .Include(x => x.SourceObservations)
            .SingleAsync();

        Assert.Equal(fixture.Manifest.AssetId, asset.AssetId);
        Assert.Equal(fixture.Manifest.IsFavorite, asset.IsFavorite);
        Assert.Null(asset.LastIndexedAtUtc);
        Assert.Equal(fixture.Manifest.Resources.Single().Sha256, asset.Resources.Single().Sha256);
        Assert.Equal(fixture.Manifest.Resources.Single().RelativePath, asset.Resources.Single().RelativePath);
        Assert.Equal(fixture.Manifest.SourceObservations.Single().SourceAssetIdentifier,
            asset.SourceObservations.Single().SourceAssetIdentifier);
        Assert.Equal(1, await db.SourceDevices.CountAsync());
    }

    [Fact]
    public async Task ArchiveRelativePaths_RejectAbsoluteAndEscapingPaths()
    {
        using var sandbox = new TemporaryDirectory();
        var root = sandbox.Combine("ArchiveA");
        await _catalog.CreateAsync(root);

        var mediaPath = Path.Combine(root, "Media", "sample.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(mediaPath)!);
        await File.WriteAllBytesAsync(mediaPath, new byte[] { 1, 2, 3 });

        var relative = ArchivePaths.ToArchiveRelative(root, mediaPath);
        Assert.Equal("Media/sample.jpg", relative);
        Assert.Equal(Path.GetFullPath(mediaPath), ArchivePaths.ResolveArchiveRelative(root, relative));
        Assert.Throws<ArgumentException>(() => ArchivePaths.ResolveArchiveRelative(root, Path.GetFullPath(mediaPath)));
        Assert.Throws<InvalidOperationException>(() => ArchivePaths.ResolveArchiveRelative(root, "../outside.jpg"));
    }

    private async Task<Fixture> CreatePopulatedArchiveAsync(
        string root,
        DateTimeOffset? lastIndexedAtUtc = null)
    {
        await _catalog.CreateAsync(root);

        var assetId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var captureDate = new DateTimeOffset(2026, 9, 30, 9, 30, 0, TimeSpan.Zero);
        var importedAt = captureDate.AddHours(1);
        var firstSeen = captureDate.AddMinutes(-10);
        var mediaBytes = new byte[] { 0x50, 0x48, 0x4F, 0x54, 0x4F, 0x01, 0x02, 0x03 };
        var mediaPath = Path.Combine(root, "Media", "2026", "09", "sample.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(mediaPath)!);
        await File.WriteAllBytesAsync(mediaPath, mediaBytes);
        var relativePath = ArchivePaths.ToArchiveRelative(root, mediaPath);
        var sha256 = Hash(mediaBytes);

        var manifest = new ArchiveManifest(
            assetId,
            captureDate,
            "Photo",
            importedAt,
            true,
            [new MediaResourceManifest(resourceId, "OriginalPhoto", relativePath, sha256, mediaBytes.Length)],
            [new SourceObservationManifest(
                deviceId,
                "Synthetic iPhone",
                "M000Synthetic",
                "source-asset-001",
                firstSeen,
                captureDate)]);

        await _catalog.WriteManifestAsync(root, manifest);

        await using var db = ArchiveDbContextFactory.Create(root);
        var device = new SourceDevice
        {
            DeviceId = deviceId,
            DisplayName = "Synthetic iPhone",
            ConnectorType = "M000Synthetic"
        };
        var asset = new ArchiveAsset
        {
            AssetId = assetId,
            CaptureDate = captureDate,
            MediaKind = "Photo",
            ImportedAt = importedAt,
            IsFavorite = true,
            LastIndexedAtUtc = lastIndexedAtUtc
        };
        asset.Resources.Add(new MediaResource
        {
            ResourceId = resourceId,
            AssetId = assetId,
            Role = "OriginalPhoto",
            RelativePath = relativePath,
            Sha256 = sha256,
            ByteLength = mediaBytes.Length
        });
        asset.SourceObservations.Add(new SourceObservation
        {
            DeviceId = deviceId,
            AssetId = assetId,
            SourceAssetIdentifier = "source-asset-001",
            FirstSeenAt = firstSeen,
            LastSeenAt = captureDate,
            Device = device
        });
        db.Add(asset);
        await db.SaveChangesAsync();

        return new Fixture(manifest, mediaPath, mediaBytes);
    }

    private static ArchiveDbContext CreateContextForDatabase(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        };
        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(builder.ToString())
            .Options;
        return new ArchiveDbContext(options);
    }

    private static async Task<HashSet<string>> ReadTableColumnsAsync(ArchiveDbContext db, string tableName)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"PRAGMA table_info('{tableName.Replace("'", "''", StringComparison.Ordinal)}');";
            await using var reader = await command.ExecuteReaderAsync();
            var columns = new HashSet<string>(StringComparer.Ordinal);
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }

            return columns;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<long> ScalarInt64Async(ArchiveDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record Fixture(ArchiveManifest Manifest, string MediaPath, byte[] MediaBytes);

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            "PhotoArchive-M000",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(_path);

        public string Combine(params string[] parts)
        {
            var allParts = new[] { _path }.Concat(parts).ToArray();
            return Path.Combine(allParts);
        }

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, recursive: true);
            }
        }
    }
}
