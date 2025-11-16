using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrapheneTrace.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientSettings",
                columns: table => new
                {
                    PatientSettingsId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LowPressureThreshold = table.Column<int>(type: "integer", nullable: false),
                    HighPressureThreshold = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientSettings", x => x.PatientSettingsId);
                    table.ForeignKey(
                        name: "FK_PatientSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientSettings_UpdatedAt",
                table: "PatientSettings",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PatientSettings_UserId",
                table: "PatientSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientSettings");
        }
    }
}
