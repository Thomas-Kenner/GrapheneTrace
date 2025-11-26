using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrapheneTrace.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResolvingMergeConflicts2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PatientSnapshotDatas_DeviceId",
                table: "PatientSnapshotDatas");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "PatientSnapshotDatas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "PatientSnapshotDatas",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PatientSnapshotDatas_DeviceId",
                table: "PatientSnapshotDatas",
                column: "DeviceId");
        }
    }
}
