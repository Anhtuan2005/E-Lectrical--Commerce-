using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerSegmentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerSegmentId",
                table: "Vouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerSegments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RuleDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RecommendedAction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerSegments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerSegmentMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerSegmentId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TotalOrders = table.Column<int>(type: "int", nullable: false),
                    TotalSpent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastOrderAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerSegmentMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerSegmentMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerSegmentMembers_CustomerSegments_CustomerSegmentId",
                        column: x => x.CustomerSegmentId,
                        principalTable: "CustomerSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CustomerSegmentId",
                table: "Vouchers",
                column: "CustomerSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSegmentMembers_CustomerSegmentId_UserId",
                table: "CustomerSegmentMembers",
                columns: new[] { "CustomerSegmentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSegmentMembers_UserId",
                table: "CustomerSegmentMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSegments_Code",
                table: "CustomerSegments",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_CustomerSegments_CustomerSegmentId",
                table: "Vouchers",
                column: "CustomerSegmentId",
                principalTable: "CustomerSegments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_CustomerSegments_CustomerSegmentId",
                table: "Vouchers");

            migrationBuilder.DropTable(
                name: "CustomerSegmentMembers");

            migrationBuilder.DropTable(
                name: "CustomerSegments");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_CustomerSegmentId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "CustomerSegmentId",
                table: "Vouchers");
        }
    }
}
