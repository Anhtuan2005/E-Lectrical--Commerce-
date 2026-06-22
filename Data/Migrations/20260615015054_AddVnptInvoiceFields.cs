using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVnptInvoiceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceErrorMessage",
                table: "Orders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceFkey",
                table: "Orders",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoiceIssuedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceLookupCode",
                table: "Orders",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Orders",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoicePattern",
                table: "Orders",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceProvider",
                table: "Orders",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceRawResponse",
                table: "Orders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceSerial",
                table: "Orders",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceStatus",
                table: "Orders",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Chưa xuất");

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoiceSyncedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceViewUrl",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_InvoiceFkey",
                table: "Orders",
                column: "InvoiceFkey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_InvoiceFkey",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceErrorMessage",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceFkey",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceIssuedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceLookupCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoicePattern",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceProvider",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceRawResponse",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceSerial",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceSyncedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceViewUrl",
                table: "Orders");
        }
    }
}
