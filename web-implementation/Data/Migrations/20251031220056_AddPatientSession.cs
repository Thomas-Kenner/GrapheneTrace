using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrapheneTrace.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientSessionDatas",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RawData = table.Column<string>(type: "text", nullable: false),
                    PeakSessionPressure = table.Column<int>(type: "integer", nullable: true),
                    ClinicianFlag = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientSessionDatas", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "PatientSnapshotDatas",
                columns: table => new
                {
                    SnapshotId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    SnapshotTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SnapshotData = table.Column<string>(type: "text", nullable: false),
                    PeakSnapshotPressure = table.Column<int>(type: "integer", nullable: true),
                    ContactAreaPercent = table.Column<float>(type: "real", nullable: true),
                    CoefficientOfVariation = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientSnapshotDatas", x => x.SnapshotId);
                    table.ForeignKey(
                        name: "FK_PatientSnapshotDatas_PatientSessionDatas_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PatientSessionDatas",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientSessionDatas_DeviceId",
                table: "PatientSessionDatas",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientSessionDatas_SessionId",
                table: "PatientSessionDatas",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientSessionDatas_UserId",
                table: "PatientSessionDatas",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientSnapshotDatas_DeviceId",
                table: "PatientSnapshotDatas",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientSnapshotDatas_SessionId",
                table: "PatientSnapshotDatas",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientSnapshotDatas_SnapshotId",
                table: "PatientSnapshotDatas",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientSnapshotDatas_UserId",
                table: "PatientSnapshotDatas",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientSnapshotDatas");

            migrationBuilder.DropTable(
                name: "PatientSessionDatas");
        }
    }
}
