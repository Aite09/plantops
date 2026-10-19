using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PlantOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlantUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantUnits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlantUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tag = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipment_PlantUnits_PlantUnitId",
                        column: x => x.PlantUnitId,
                        principalTable: "PlantUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlantUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrders_PlantUnits_PlantUnitId",
                        column: x => x.PlantUnitId,
                        principalTable: "PlantUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "PlantUnits",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000100"), "U-100", "Crude Distillation" },
                    { new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000200"), "U-200", "Diesel Hydrotreater" },
                    { new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000300"), "U-300", "Cooling Water System" },
                    { new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000400"), "U-400", "Steam and Utilities" }
                });

            migrationBuilder.InsertData(
                table: "Equipment",
                columns: new[] { "Id", "Name", "PlantUnitId", "Tag" },
                values: new object[,]
                {
                    { new Guid("8a4e1f30-5d6c-4b7a-8e21-000000000101"), "Crude charge pump A", new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000100"), "P-101A" },
                    { new Guid("8a4e1f30-5d6c-4b7a-8e21-000000000102"), "Crude preheat exchanger", new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000100"), "E-102" },
                    { new Guid("8a4e1f30-5d6c-4b7a-8e21-000000000103"), "Crude charge heater", new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000100"), "F-101" },
                    { new Guid("8a4e1f30-5d6c-4b7a-8e21-000000000201"), "Recycle gas compressor", new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000200"), "C-201" },
                    { new Guid("8a4e1f30-5d6c-4b7a-8e21-000000000202"), "Hydrotreating reactor", new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000200"), "R-201" },
                    { new Guid("8a4e1f30-5d6c-4b7a-8e21-000000000203"), "Reactor feed pump", new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000200"), "P-205" },
                    { new Guid("8a4e1f30-5d6c-4b7a-8e21-000000000301"), "Cooling tower fan 1", new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000300"), "CT-301" },
                    { new Guid("8a4e1f30-5d6c-4b7a-8e21-000000000302"), "Cooling water pump B", new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000300"), "P-301B" },
                    { new Guid("8a4e1f30-5d6c-4b7a-8e21-000000000401"), "Package boiler", new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000400"), "B-401" },
                    { new Guid("8a4e1f30-5d6c-4b7a-8e21-000000000402"), "Steam drum safety valve", new Guid("6f0d2c1a-1b2e-4c3d-9a10-000000000400"), "PSV-410" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_PlantUnitId",
                table: "Equipment",
                column: "PlantUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Tag",
                table: "Equipment",
                column: "Tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlantUnits_Code",
                table: "PlantUnits",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_EquipmentId",
                table: "WorkOrders",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_PlantUnitId",
                table: "WorkOrders",
                column: "PlantUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "PlantUnits");
        }
    }
}
