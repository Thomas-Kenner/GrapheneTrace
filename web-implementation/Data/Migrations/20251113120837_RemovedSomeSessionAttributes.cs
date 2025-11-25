using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrapheneTrace.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovedSomeSessionAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PatientSnapshotDatas_UserId",
                table: "PatientSnapshotDatas");

            migrationBuilder.DropIndex(
                name: "IX_PatientSessionDatas_UserId",
                table: "PatientSessionDatas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PatientSnapshotDatas_UserId",
                table: "PatientSnapshotDatas",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientSessionDatas_UserId",
                table: "PatientSessionDatas",
                column: "UserId");
        }
    }
}
