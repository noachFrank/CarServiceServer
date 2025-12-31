using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DispatchApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddReoccurringAndRouteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedDuration",
                table: "Rides");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EstimatedDuration",
                table: "Routes",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "CarSeat",
                table: "Rides",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReoccurring",
                table: "Rides",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Reoccurrings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RideId = table.Column<int>(type: "int", nullable: false)
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
                name: "IX_Reoccurrings_RideId",
                table: "Reoccurrings",
                column: "RideId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reoccurrings");

            migrationBuilder.DropColumn(
                name: "EstimatedDuration",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "CarSeat",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "IsReoccurring",
                table: "Rides");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EstimatedDuration",
                table: "Rides",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));
        }
    }
}
