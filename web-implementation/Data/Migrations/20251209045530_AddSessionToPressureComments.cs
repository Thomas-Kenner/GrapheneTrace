using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrapheneTrace.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionToPressureComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FrameIndex",
                table: "PressureComments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionId",
                table: "PressureComments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FrameIndex",
                table: "PressureComments");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "PressureComments");
        }
    }
}
