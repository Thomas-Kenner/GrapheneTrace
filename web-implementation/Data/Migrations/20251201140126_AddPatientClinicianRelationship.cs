using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrapheneTrace.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientClinicianRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientClinicianRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResponseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientClinicianRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientClinicianRequests_AspNetUsers_ClinicianId",
                        column: x => x.ClinicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PatientClinicianRequests_AspNetUsers_PatientId",
                        column: x => x.PatientId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientClinicians",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnassignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientClinicians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientClinicians_AspNetUsers_ClinicianId",
                        column: x => x.ClinicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PatientClinicians_AspNetUsers_PatientId",
                        column: x => x.PatientId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientClinicianRequests_ClinicianId",
                table: "PatientClinicianRequests",
                column: "ClinicianId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientClinicianRequests_PatientId",
                table: "PatientClinicianRequests",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientClinicianRequests_PatientId_ClinicianId",
                table: "PatientClinicianRequests",
                columns: new[] { "PatientId", "ClinicianId" },
                unique: true,
                filter: "\"Status\" = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_PatientClinicianRequests_Status",
                table: "PatientClinicianRequests",
                column: "Status",
                filter: "\"Status\" = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_PatientClinicians_ClinicianId",
                table: "PatientClinicians",
                column: "ClinicianId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientClinicians_PatientId",
                table: "PatientClinicians",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientClinicians_PatientId_ClinicianId",
                table: "PatientClinicians",
                columns: new[] { "PatientId", "ClinicianId" },
                unique: true,
                filter: "\"UnassignedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientClinicianRequests");

            migrationBuilder.DropTable(
                name: "PatientClinicians");
        }
    }
}
