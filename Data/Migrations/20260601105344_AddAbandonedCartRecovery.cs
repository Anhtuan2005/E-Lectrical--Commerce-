using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAbandonedCartRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetUserId",
                table: "Vouchers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Carts",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.CreateTable(
                name: "AbandonedCartReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CartId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: true),
                    RecoveryCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CartUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmailSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastShownAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbandonedCartReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbandonedCartReminders_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AbandonedCartReminders_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AbandonedCartReminders_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_TargetUserId",
                table: "Vouchers",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_UpdatedAt",
                table: "Carts",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCartReminders_CartId_CartUpdatedAt",
                table: "AbandonedCartReminders",
                columns: new[] { "CartId", "CartUpdatedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCartReminders_UserId",
                table: "AbandonedCartReminders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbandonedCartReminders_VoucherId",
                table: "AbandonedCartReminders",
                column: "VoucherId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_AspNetUsers_TargetUserId",
                table: "Vouchers",
                column: "TargetUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_AspNetUsers_TargetUserId",
                table: "Vouchers");

            migrationBuilder.DropTable(
                name: "AbandonedCartReminders");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_TargetUserId",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Carts_UpdatedAt",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "TargetUserId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Carts");
        }
    }
}
