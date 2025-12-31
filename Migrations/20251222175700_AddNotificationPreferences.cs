using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DispatchApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    MessagesEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BroadcastMessagesEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NewCallEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CallAvailableAgainEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MyCallReassignedEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MyCallCanceledEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_DriverId",
                table: "NotificationPreferences",
                column: "DriverId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationPreferences");
        }
    }
}
