using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortReusableSprint2Features : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockLogs_ProductId",
                table: "StockLogs");

            migrationBuilder.AddColumn<string>(
                name: "MovementType",
                table: "StockLogs",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Điều chỉnh thủ công");

            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "StockLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityAfter",
                table: "StockLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityBefore",
                table: "StockLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StoreRepliedAt",
                table: "Reviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreReply",
                table: "Reviews",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CpuCores",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CpuThreads",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GpuVramGb",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "M2Slots",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemoryCapacityGb",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemorySpeedMt",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecificationSourceUrl",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageCapacityGb",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageReadMb",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageWriteMb",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundConfirmedBy",
                table: "Orders",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundMethod",
                table: "Orders",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundReference",
                table: "Orders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockLogs_OrderId",
                table: "StockLogs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLogs_ProductId_ChangedAt",
                table: "StockLogs",
                columns: new[] { "ProductId", "ChangedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_StockLogs_Orders_OrderId",
                table: "StockLogs",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            // Delivered COD orders from the old schema represent collected cash.
            migrationBuilder.Sql("""
                UPDATE Orders SET IsPaid = 1, PaidAt = COALESCE(PaidAt, UpdatedAt)
                WHERE PaymentMethod = 'COD' AND Status = N'Đã giao' AND IsPaid = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockLogs_Orders_OrderId",
                table: "StockLogs");

            migrationBuilder.DropIndex(
                name: "IX_StockLogs_OrderId",
                table: "StockLogs");

            migrationBuilder.DropIndex(
                name: "IX_StockLogs_ProductId_ChangedAt",
                table: "StockLogs");

            migrationBuilder.DropColumn(
                name: "MovementType",
                table: "StockLogs");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "StockLogs");

            migrationBuilder.DropColumn(
                name: "QuantityAfter",
                table: "StockLogs");

            migrationBuilder.DropColumn(
                name: "QuantityBefore",
                table: "StockLogs");

            migrationBuilder.DropColumn(
                name: "StoreRepliedAt",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "StoreReply",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "CpuCores",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CpuThreads",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "GpuVramGb",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "M2Slots",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MemoryCapacityGb",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MemorySpeedMt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SpecificationSourceUrl",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StorageCapacityGb",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StorageReadMb",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StorageWriteMb",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RefundConfirmedBy",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RefundMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RefundReference",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_StockLogs_ProductId",
                table: "StockLogs",
                column: "ProductId");
        }
    }
}
