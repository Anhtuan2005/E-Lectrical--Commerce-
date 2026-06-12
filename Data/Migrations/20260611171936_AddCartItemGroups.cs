using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCartItemGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupItemLabel",
                table: "CartItems",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupKey",
                table: "CartItems",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                table: "CartItems",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupSortOrder",
                table: "CartItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupSource",
                table: "CartItems",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_GroupKey",
                table: "CartItems",
                columns: new[] { "CartId", "GroupKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_GroupKey",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "GroupItemLabel",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "GroupKey",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "GroupName",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "GroupSortOrder",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "GroupSource",
                table: "CartItems");
        }
    }
}
