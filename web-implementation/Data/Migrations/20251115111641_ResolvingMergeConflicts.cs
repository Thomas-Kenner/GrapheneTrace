using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrapheneTrace.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResolvingMergeConflicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PatientSnapshotDatas");

            migrationBuilder.DropColumn(
                name: "RawData",
                table: "PatientSessionDatas");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PatientSessionDatas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "PatientSnapshotDatas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RawData",
                table: "PatientSessionDatas",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "PatientSessionDatas",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
