using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farol.Web.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Sites",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CheckIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sites", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SiteChecks",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SiteId = table.Column<int>(type: "int", nullable: false),
                CheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsUp = table.Column<bool>(type: "bit", nullable: false),
                StatusCode = table.Column<int>(type: "int", nullable: true),
                ResponseTimeMs = table.Column<int>(type: "int", nullable: false),
                SslExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SiteChecks", x => x.Id);
                table.ForeignKey(
                    name: "FK_SiteChecks_Sites_SiteId",
                    column: x => x.SiteId,
                    principalTable: "Sites",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SiteChecks_SiteId_CheckedAt",
            table: "SiteChecks",
            columns: new[] { "SiteId", "CheckedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Sites_Url",
            table: "Sites",
            column: "Url",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SiteChecks");

        migrationBuilder.DropTable(
            name: "Sites");
    }
}
