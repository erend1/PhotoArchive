using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoArchive.PersistenceSpike.Migrations;

[DbContext(typeof(ArchiveDbContext))]
[Migration("202609300002_AddRebuildableAndCatalogOnlyFields")]
public sealed class AddRebuildableAndCatalogOnlyFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsFavorite",
            table: "ArchiveAssets",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastIndexedAtUtc",
            table: "ArchiveAssets",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsFavorite", table: "ArchiveAssets");
        migrationBuilder.DropColumn(name: "LastIndexedAtUtc", table: "ArchiveAssets");
    }
}
