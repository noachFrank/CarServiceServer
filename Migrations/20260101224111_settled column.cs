using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DispatchApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class settledcolumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Settled",
                table: "Rides",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Settled",
                table: "Rides");
        }
    }
}
