using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrugInventoryPro.Migrations
{
    /// <inheritdoc />
    public partial class RemovePurchaseOrdersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseOrderDetail");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "MedicineUnits",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "MedicineUnits",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    poid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    medicine_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    podate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ponumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.poid);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Medicines_medicine_id",
                        column: x => x.medicine_id,
                        principalTable: "Medicines",
                        principalColumn: "medicine_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderDetail",
                columns: table => new
                {
                    PODetailID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Medicine_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    POID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Unit_idPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderDetail", x => x.PODetailID);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDetail_Medicines_Medicine_id",
                        column: x => x.Medicine_id,
                        principalTable: "Medicines",
                        principalColumn: "medicine_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderDetail_PurchaseOrders_POID",
                        column: x => x.POID,
                        principalTable: "PurchaseOrders",
                        principalColumn: "poid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDetail_Medicine_id",
                table: "PurchaseOrderDetail",
                column: "Medicine_id");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDetail_POID",
                table: "PurchaseOrderDetail",
                column: "POID");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_medicine_id",
                table: "PurchaseOrders",
                column: "medicine_id");
        }
    }
}
