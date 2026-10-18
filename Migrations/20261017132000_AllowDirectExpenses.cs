using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bill_splitter_asp.Migrations;

public partial class AllowDirectExpenses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // GroupId is nullable so direct one-to-one expenses can live outside a group.
        migrationBuilder.AlterColumn<Guid>(
            name: "GroupId",
            table: "Expenses",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "GroupId",
            table: "Expenses",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);
    }
}