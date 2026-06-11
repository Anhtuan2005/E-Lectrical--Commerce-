using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrossSellOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrossSellOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnchorProductId = table.Column<int>(type: "int", nullable: false),
                    AddOnProductId = table.Column<int>(type: "int", nullable: false),
                    DiscountPercent = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossSellOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrossSellOffers_Products_AddOnProductId",
                        column: x => x.AddOnProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CrossSellOffers_Products_AnchorProductId",
                        column: x => x.AnchorProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrossSellOffers_AddOnProductId",
                table: "CrossSellOffers",
                column: "AddOnProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CrossSellOffers_AnchorProductId_AddOnProductId",
                table: "CrossSellOffers",
                columns: new[] { "AnchorProductId", "AddOnProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrossSellOffers");
        }
    }
}
