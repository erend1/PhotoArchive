using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoArchive.PersistenceSpike.Migrations;

[DbContext(typeof(ArchiveDbContext))]
[Migration("202609300001_InitialCatalog")]
public sealed class InitialCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ArchiveAssets",
            columns: table => new
            {
                AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                CaptureDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                MediaKind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ImportedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ArchiveAssets", x => x.AssetId));

        migrationBuilder.CreateTable(
            name: "SourceDevices",
            columns: table => new
            {
                DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ConnectorType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SourceDevices", x => x.DeviceId));

        migrationBuilder.CreateTable(
            name: "MediaResources",
            columns: table => new
            {
                ResourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                Role = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                RelativePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ByteLength = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MediaResources", x => x.ResourceId);
                table.ForeignKey(
                    name: "FK_MediaResources_ArchiveAssets_AssetId",
                    column: x => x.AssetId,
                    principalTable: "ArchiveAssets",
                    principalColumn: "AssetId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SourceObservations",
            columns: table => new
            {
                ObservationId = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                SourceAssetIdentifier = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SourceObservations", x => x.ObservationId);
                table.ForeignKey(
                    name: "FK_SourceObservations_ArchiveAssets_AssetId",
                    column: x => x.AssetId,
                    principalTable: "ArchiveAssets",
                    principalColumn: "AssetId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SourceObservations_SourceDevices_DeviceId",
                    column: x => x.DeviceId,
                    principalTable: "SourceDevices",
                    principalColumn: "DeviceId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MediaResources_AssetId",
            table: "MediaResources",
            column: "AssetId");

        migrationBuilder.CreateIndex(
            name: "IX_MediaResources_Sha256",
            table: "MediaResources",
            column: "Sha256");

        migrationBuilder.CreateIndex(
            name: "IX_SourceObservations_AssetId",
            table: "SourceObservations",
            column: "AssetId");

        migrationBuilder.CreateIndex(
            name: "IX_SourceObservations_DeviceId_SourceAssetIdentifier",
            table: "SourceObservations",
            columns: new[] { "DeviceId", "SourceAssetIdentifier" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MediaResources");
        migrationBuilder.DropTable(name: "SourceObservations");
        migrationBuilder.DropTable(name: "ArchiveAssets");
        migrationBuilder.DropTable(name: "SourceDevices");
    }
}
