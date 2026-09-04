using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farol.Web.Migrations;

/// <inheritdoc />
public partial class AddSiteExpiry : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ExpiresAt",
            table: "Sites",
            type: "datetimeoffset",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExpiresAt",
            table: "Sites");
    }
}
