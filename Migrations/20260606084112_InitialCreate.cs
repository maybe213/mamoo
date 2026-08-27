using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrugInventoryPro.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    category_id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    category_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    related_disease = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.category_id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Department_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Department_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contact_title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contact_firstname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contact_lastname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Created_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Department_id);
                });

            migrationBuilder.CreateTable(
                name: "Dispense",
                columns: table => new
                {
                    Dispense_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Dispense_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Department_id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dispense", x => x.Dispense_id);
                });

            migrationBuilder.CreateTable(
                name: "MedicineUnits",
                columns: table => new
                {
                    Unit_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Unit_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Group_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineUnits", x => x.Unit_id);
                });

            migrationBuilder.CreateTable(
                name: "Receives",
                columns: table => new
                {
                    Receive_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Receive_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Invoice_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Received_by = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Po_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receives", x => x.Receive_id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    User_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Firstname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Lastname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    P_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Em = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Department_id = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.User_id);
                    table.ForeignKey(
                        name: "FK_Users_Departments_Department_id",
                        column: x => x.Department_id,
                        principalTable: "Departments",
                        principalColumn: "Department_id");
                });

            migrationBuilder.CreateTable(
                name: "Medicines",
                columns: table => new
                {
                    medicine_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    medicine_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    category_id = table.Column<string>(type: "nvarchar(10)", nullable: true),
                    Unit_id = table.Column<int>(type: "int", nullable: true),
                    quantity = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    expired_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: true),
                    Packing_Size = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Account_Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    lot = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medicines", x => x.medicine_id);
                    table.ForeignKey(
                        name: "FK_Medicines_Categories_category_id",
                        column: x => x.category_id,
                        principalTable: "Categories",
                        principalColumn: "category_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Medicines_MedicineUnits_Unit_id",
                        column: x => x.Unit_id,
                        principalTable: "MedicineUnits",
                        principalColumn: "Unit_id");
                });

            migrationBuilder.CreateTable(
                name: "DispenseDetails",
                columns: table => new
                {
                    Dispense_detail_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Dispense_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Medicine_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Quantity_dispensed = table.Column<int>(type: "int", nullable: true),
                    Quantity_requested = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispenseDetails", x => x.Dispense_detail_id);
                    table.ForeignKey(
                        name: "FK_DispenseDetails_Dispense_Dispense_id",
                        column: x => x.Dispense_id,
                        principalTable: "Dispense",
                        principalColumn: "Dispense_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DispenseDetails_Medicines_Medicine_id",
                        column: x => x.Medicine_id,
                        principalTable: "Medicines",
                        principalColumn: "medicine_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    poid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ponumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    podate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    medicine_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false)
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
                name: "ReceiveDetails",
                columns: table => new
                {
                    Receive_detail_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Receive_id = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Medicine_id = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Quantity_received = table.Column<int>(type: "int", nullable: false),
                    Lot_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Expiry_date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiveDetails", x => x.Receive_detail_id);
                    table.ForeignKey(
                        name: "FK_ReceiveDetails_Medicines_Medicine_id",
                        column: x => x.Medicine_id,
                        principalTable: "Medicines",
                        principalColumn: "medicine_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiveDetails_Receives_Receive_id",
                        column: x => x.Receive_id,
                        principalTable: "Receives",
                        principalColumn: "Receive_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Stock",
                columns: table => new
                {
                    StockId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    medicine_id = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stock", x => x.StockId);
                    table.ForeignKey(
                        name: "FK_Stock_Medicines_medicine_id",
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
                    POID = table.Column<int>(type: "int", nullable: false),
                    Medicine_id = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                name: "IX_DispenseDetails_Dispense_id",
                table: "DispenseDetails",
                column: "Dispense_id");

            migrationBuilder.CreateIndex(
                name: "IX_DispenseDetails_Medicine_id",
                table: "DispenseDetails",
                column: "Medicine_id");

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_category_id",
                table: "Medicines",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_Unit_id",
                table: "Medicines",
                column: "Unit_id");

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

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveDetails_Medicine_id",
                table: "ReceiveDetails",
                column: "Medicine_id");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveDetails_Receive_id",
                table: "ReceiveDetails",
                column: "Receive_id");

            migrationBuilder.CreateIndex(
                name: "IX_Stock_medicine_id",
                table: "Stock",
                column: "medicine_id");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Department_id",
                table: "Users",
                column: "Department_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispenseDetails");

            migrationBuilder.DropTable(
                name: "PurchaseOrderDetail");

            migrationBuilder.DropTable(
                name: "ReceiveDetails");

            migrationBuilder.DropTable(
                name: "Stock");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Dispense");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "Receives");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Medicines");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "MedicineUnits");
        }
    }
}
