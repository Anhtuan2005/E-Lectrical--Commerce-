using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenOrderLifecycleAndProductConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentExpiresAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE Orders SET PaymentExpiresAt = DATEADD(minute, 15, CreatedAt)
                WHERE PaymentMethod = N'VNPAY';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentMethod_IsPaid_Status_PaymentExpiresAt",
                table: "Orders",
                columns: new[] { "PaymentMethod", "IsPaid", "Status", "PaymentExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentMethod_IsPaid_Status_PaymentExpiresAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PaymentExpiresAt",
                table: "Orders");
        }
    }
}
