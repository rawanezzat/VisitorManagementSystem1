using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisitorManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class MoveNationalIdToVisitor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "Visits");

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "Visitors",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visitors_NationalId",
                table: "Visitors",
                column: "NationalId",
                unique: true,
                filter: "[NationalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Visitors_NationalId",
                table: "Visitors");

            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "Visitors");

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "Visits",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
