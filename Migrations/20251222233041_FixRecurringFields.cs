using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DispatchApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class FixRecurringFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reoccurrings");

            migrationBuilder.DropIndex(
                name: "IX_Rides_RouteId",
                table: "Rides");

            migrationBuilder.RenameColumn(
                name: "IsReoccurring",
                table: "Rides",
                newName: "IsRecurring");

            migrationBuilder.AddColumn<int>(
                name: "RecurringId",
                table: "Rides",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Recurrings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recurrings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rides_RecurringId",
                table: "Rides",
                column: "RecurringId");

            migrationBuilder.CreateIndex(
                name: "IX_Rides_RouteId",
                table: "Rides",
                column: "RouteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rides_Recurrings_RecurringId",
                table: "Rides",
                column: "RecurringId",
                principalTable: "Recurrings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rides_Recurrings_RecurringId",
                table: "Rides");

            migrationBuilder.DropTable(
                name: "Recurrings");

            migrationBuilder.DropIndex(
                name: "IX_Rides_RecurringId",
                table: "Rides");

            migrationBuilder.DropIndex(
                name: "IX_Rides_RouteId",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "RecurringId",
                table: "Rides");

            migrationBuilder.RenameColumn(
                name: "IsRecurring",
                table: "Rides",
                newName: "IsReoccurring");

            migrationBuilder.CreateTable(
                name: "Reoccurrings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RideId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reoccurrings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reoccurrings_Rides_RideId",
                        column: x => x.RideId,
                        principalTable: "Rides",
                        principalColumn: "RideId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rides_RouteId",
                table: "Rides",
                column: "RouteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reoccurrings_RideId",
                table: "Reoccurrings",
                column: "RideId",
                unique: true);
        }
    }
}
