using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnWarrantyRequestsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Orders SET Status = N'Chờ xác nhận' WHERE Status = N'Chá» xÃ¡c nháº­n';
                UPDATE Orders SET Status = N'Đã xác nhận' WHERE Status = N'ÄÃ£ xÃ¡c nháº­n';
                UPDATE Orders SET Status = N'Đang giao' WHERE Status = N'Äang giao';
                UPDATE Orders SET Status = N'Đã giao' WHERE Status = N'ÄÃ£ giao';
                UPDATE Orders SET Status = N'Huỷ' WHERE Status = N'Huá»·';

                UPDATE Orders SET RefundStatus = N'Không cần hoàn tiền' WHERE RefundStatus = N'KhÃ´ng cáº§n hoÃ n tiá»n';
                UPDATE Orders SET RefundStatus = N'Cần hoàn tiền thủ công' WHERE RefundStatus = N'Cáº§n hoÃ n tiá»n thá»§ cÃ´ng';
                UPDATE Orders SET RefundStatus = N'Đã hoàn tiền' WHERE RefundStatus = N'ÄÃ£ hoÃ n tiá»n';

                UPDATE ShippingInfos SET Status = N'Chưa gán vận chuyển' WHERE Status = N'ChÆ°a gÃ¡n váº­n chuyá»ƒn';
                UPDATE ShippingInfos SET Status = N'Chờ lấy hàng' WHERE Status = N'Chá» láº¥y hÃ ng';
                UPDATE ShippingInfos SET Status = N'Đang vận chuyển' WHERE Status = N'Äang váº­n chuyá»ƒn';
                UPDATE ShippingInfos SET Status = N'Đã giao' WHERE Status = N'ÄÃ£ giao';
                UPDATE ShippingInfos SET Status = N'Chậm trễ' WHERE Status = N'Cháº­m trá»…';
                """);

            migrationBuilder.CreateTable(
                name: "ReturnWarrantyRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    PreferredResolution = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnWarrantyRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnWarrantyRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnWarrantyRequests_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LinkUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnWarrantyRequestImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnWarrantyRequestId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnWarrantyRequestImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnWarrantyRequestImages_ReturnWarrantyRequests_ReturnWarrantyRequestId",
                        column: x => x.ReturnWarrantyRequestId,
                        principalTable: "ReturnWarrantyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnWarrantyRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnWarrantyRequestId = table.Column<int>(type: "int", nullable: false),
                    OrderItemId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnWarrantyRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnWarrantyRequestItems_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnWarrantyRequestItems_ReturnWarrantyRequests_ReturnWarrantyRequestId",
                        column: x => x.ReturnWarrantyRequestId,
                        principalTable: "ReturnWarrantyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnWarrantyRequestImages_ReturnWarrantyRequestId",
                table: "ReturnWarrantyRequestImages",
                column: "ReturnWarrantyRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnWarrantyRequestItems_OrderItemId",
                table: "ReturnWarrantyRequestItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnWarrantyRequestItems_ReturnWarrantyRequestId",
                table: "ReturnWarrantyRequestItems",
                column: "ReturnWarrantyRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnWarrantyRequests_OrderId",
                table: "ReturnWarrantyRequests",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnWarrantyRequests_Status_Type",
                table: "ReturnWarrantyRequests",
                columns: new[] { "Status", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnWarrantyRequests_UserId_CreatedAt",
                table: "ReturnWarrantyRequests",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_IsRead_CreatedAt",
                table: "UserNotifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Orders SET Status = N'Chá» xÃ¡c nháº­n' WHERE Status = N'Chờ xác nhận';
                UPDATE Orders SET Status = N'ÄÃ£ xÃ¡c nháº­n' WHERE Status = N'Đã xác nhận';
                UPDATE Orders SET Status = N'Äang giao' WHERE Status = N'Đang giao';
                UPDATE Orders SET Status = N'ÄÃ£ giao' WHERE Status = N'Đã giao';
                UPDATE Orders SET Status = N'Huá»·' WHERE Status = N'Huỷ';

                UPDATE Orders SET RefundStatus = N'KhÃ´ng cáº§n hoÃ n tiá»n' WHERE RefundStatus = N'Không cần hoàn tiền';
                UPDATE Orders SET RefundStatus = N'Cáº§n hoÃ n tiá»n thá»§ cÃ´ng' WHERE RefundStatus = N'Cần hoàn tiền thủ công';
                UPDATE Orders SET RefundStatus = N'ÄÃ£ hoÃ n tiá»n' WHERE RefundStatus = N'Đã hoàn tiền';

                UPDATE ShippingInfos SET Status = N'ChÆ°a gÃ¡n váº­n chuyá»ƒn' WHERE Status = N'Chưa gán vận chuyển';
                UPDATE ShippingInfos SET Status = N'Chá» láº¥y hÃ ng' WHERE Status = N'Chờ lấy hàng';
                UPDATE ShippingInfos SET Status = N'Äang váº­n chuyá»ƒn' WHERE Status = N'Đang vận chuyển';
                UPDATE ShippingInfos SET Status = N'ÄÃ£ giao' WHERE Status = N'Đã giao';
                UPDATE ShippingInfos SET Status = N'Cháº­m trá»…' WHERE Status = N'Chậm trễ';
                """);

            migrationBuilder.DropTable(
                name: "ReturnWarrantyRequestImages");

            migrationBuilder.DropTable(
                name: "ReturnWarrantyRequestItems");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "ReturnWarrantyRequests");
        }
    }
}
