using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PhotoArchive.PersistenceSpike;

public sealed class ArchiveDbContext(DbContextOptions<ArchiveDbContext> options) : DbContext(options)
{
    public DbSet<ArchiveAsset> ArchiveAssets => Set<ArchiveAsset>();
    public DbSet<MediaResource> MediaResources => Set<MediaResource>();
    public DbSet<SourceDevice> SourceDevices => Set<SourceDevice>();
    public DbSet<SourceObservation> SourceObservations => Set<SourceObservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArchiveAsset>(entity =>
        {
            entity.HasKey(x => x.AssetId);
            entity.Property(x => x.MediaKind).HasMaxLength(64);
        });

        modelBuilder.Entity<MediaResource>(entity =>
        {
            entity.HasKey(x => x.ResourceId);
            entity.Property(x => x.Role).HasMaxLength(64);
            entity.Property(x => x.RelativePath).HasMaxLength(1024);
            entity.Property(x => x.Sha256).HasMaxLength(64);
            entity.HasIndex(x => x.Sha256);
            entity.HasOne(x => x.Asset)
                .WithMany(x => x.Resources)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceDevice>(entity =>
        {
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.DisplayName).HasMaxLength(256);
            entity.Property(x => x.ConnectorType).HasMaxLength(64);
        });

        modelBuilder.Entity<SourceObservation>(entity =>
        {
            entity.HasKey(x => x.ObservationId);
            entity.Property(x => x.SourceAssetIdentifier).HasMaxLength(512);
            entity.HasIndex(x => new { x.DeviceId, x.SourceAssetIdentifier });
            entity.HasOne(x => x.Device)
                .WithMany(x => x.Observations)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset)
                .WithMany(x => x.SourceObservations)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public static class ArchiveDbContextFactory
{
    public static ArchiveDbContext Create(string archiveRoot)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = ArchivePaths.CatalogPath(archiveRoot),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };

        var options = new DbContextOptionsBuilder<ArchiveDbContext>()
            .UseSqlite(builder.ToString())
            .Options;

        return new ArchiveDbContext(options);
    }
}
