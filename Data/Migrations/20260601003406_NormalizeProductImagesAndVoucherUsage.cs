using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeProductImagesAndVoucherUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'Products', N'ImageUrl') IS NOT NULL
BEGIN
    INSERT INTO [ProductImages] ([ProductId], [ImageUrl], [SortOrder])
    SELECT [p].[Id], [p].[ImageUrl], 0
    FROM [Products] AS [p]
    WHERE NULLIF(LTRIM(RTRIM([p].[ImageUrl])), N'') IS NOT NULL
      AND NOT EXISTS (
          SELECT 1
          FROM [ProductImages] AS [pi]
          WHERE [pi].[ProductId] = [p].[Id]
            AND [pi].[ImageUrl] = [p].[ImageUrl]);
END");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'Orders', N'VoucherCode') IS NOT NULL
BEGIN
    ;WITH [Candidates] AS (
        SELECT
            [v].[Id] AS [VoucherId],
            [o].[UserId],
            [o].[Id] AS [OrderId],
            [o].[CreatedAt] AS [UsedAt],
            ROW_NUMBER() OVER (
                PARTITION BY [v].[Id], [o].[UserId]
                ORDER BY [o].[CreatedAt], [o].[Id]) AS [UserVoucherRank]
        FROM [Orders] AS [o]
        INNER JOIN [Vouchers] AS [v] ON [v].[Code] = [o].[VoucherCode]
        WHERE NULLIF(LTRIM(RTRIM([o].[VoucherCode])), N'') IS NOT NULL
          AND NOT EXISTS (
              SELECT 1
              FROM [VoucherUsages] AS [vu]
              WHERE [vu].[OrderId] = [o].[Id])
    )
    INSERT INTO [VoucherUsages] ([VoucherId], [UserId], [OrderId], [UsedAt])
    SELECT [c].[VoucherId], [c].[UserId], [c].[OrderId], [c].[UsedAt]
    FROM [Candidates] AS [c]
    WHERE [c].[UserVoucherRank] = 1
      AND NOT EXISTS (
          SELECT 1
          FROM [VoucherUsages] AS [vu]
          WHERE [vu].[VoucherId] = [c].[VoucherId]
            AND [vu].[UserId] = [c].[UserId]);
END");

            migrationBuilder.DropIndex(
                name: "IX_VoucherUsages_OrderId",
                table: "VoucherUsages");

            migrationBuilder.DropIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId",
                table: "CartItems");

            migrationBuilder.Sql(@"
;WITH [RankedVoucherUsages] AS (
    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [OrderId] ORDER BY [Id]) AS [RowNumber]
    FROM [VoucherUsages]
)
DELETE FROM [RankedVoucherUsages]
WHERE [RowNumber] > 1;");

            migrationBuilder.Sql(@"
;WITH [MergedCartItems] AS (
    SELECT [CartId], [ProductId], MIN([Id]) AS [KeepId], SUM([Quantity]) AS [Quantity]
    FROM [CartItems]
    GROUP BY [CartId], [ProductId]
    HAVING COUNT(*) > 1
)
UPDATE [keep]
SET [Quantity] = [merged].[Quantity]
FROM [CartItems] AS [keep]
INNER JOIN [MergedCartItems] AS [merged] ON [merged].[KeepId] = [keep].[Id];

;WITH [MergedCartItems] AS (
    SELECT [CartId], [ProductId], MIN([Id]) AS [KeepId]
    FROM [CartItems]
    GROUP BY [CartId], [ProductId]
    HAVING COUNT(*) > 1
)
DELETE [item]
FROM [CartItems] AS [item]
INNER JOIN [MergedCartItems] AS [merged]
    ON [merged].[CartId] = [item].[CartId]
   AND [merged].[ProductId] = [item].[ProductId]
WHERE [item].[Id] <> [merged].[KeepId];");

            migrationBuilder.Sql(@"
;WITH [RankedProductImages] AS (
    SELECT [Id], ROW_NUMBER() OVER (
        PARTITION BY [ProductId], [ImageUrl]
        ORDER BY [SortOrder], [Id]) AS [RowNumber]
    FROM [ProductImages]
)
DELETE FROM [RankedProductImages]
WHERE [RowNumber] > 1;

;WITH [OrderedProductImages] AS (
    SELECT
        [Id],
        ROW_NUMBER() OVER (PARTITION BY [ProductId] ORDER BY [SortOrder], [Id]) - 1 AS [NewSortOrder]
    FROM [ProductImages]
)
UPDATE [image]
SET [SortOrder] = [ordered].[NewSortOrder]
FROM [ProductImages] AS [image]
INNER JOIN [OrderedProductImages] AS [ordered] ON [ordered].[Id] = [image].[Id];");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VoucherCode",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_OrderId",
                table: "VoucherUsages",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId_ImageUrl",
                table: "ProductImages",
                columns: new[] { "ProductId", "ImageUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId_SortOrder",
                table: "ProductImages",
                columns: new[] { "ProductId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VoucherUsages_OrderId",
                table: "VoucherUsages");

            migrationBuilder.DropIndex(
                name: "IX_ProductImages_ProductId_ImageUrl",
                table: "ProductImages");

            migrationBuilder.DropIndex(
                name: "IX_ProductImages_ProductId_SortOrder",
                table: "ProductImages");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_ProductId",
                table: "CartItems");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VoucherCode",
                table: "Orders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE [product]
SET [ImageUrl] = COALESCE([image].[ImageUrl], N'')
FROM [Products] AS [product]
OUTER APPLY (
    SELECT TOP (1) [pi].[ImageUrl]
    FROM [ProductImages] AS [pi]
    WHERE [pi].[ProductId] = [product].[Id]
    ORDER BY [pi].[SortOrder], [pi].[Id]
) AS [image];");

            migrationBuilder.Sql(@"
UPDATE [order]
SET [VoucherCode] = [voucher].[Code]
FROM [Orders] AS [order]
INNER JOIN [VoucherUsages] AS [usage] ON [usage].[OrderId] = [order].[Id]
INNER JOIN [Vouchers] AS [voucher] ON [voucher].[Id] = [usage].[VoucherId];");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_OrderId",
                table: "VoucherUsages",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId",
                table: "CartItems",
                column: "CartId");
        }
    }
}
