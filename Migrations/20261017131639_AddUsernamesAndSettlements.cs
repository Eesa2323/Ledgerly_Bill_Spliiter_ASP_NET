using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bill_splitter_asp.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernamesAndSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SettledAtUtc",
                table: "ExpenseSplits",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SettledAtUtc",
                table: "ExpenseSplits");
        }
    }
}
