using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrapheneTrace.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientIdToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PatientId",
                table: "PatientSessionDatas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientSessionDatas_PatientId",
                table: "PatientSessionDatas",
                column: "PatientId");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientSessionDatas_AspNetUsers_PatientId",
                table: "PatientSessionDatas",
                column: "PatientId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientSessionDatas_AspNetUsers_PatientId",
                table: "PatientSessionDatas");

            migrationBuilder.DropIndex(
                name: "IX_PatientSessionDatas_PatientId",
                table: "PatientSessionDatas");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "PatientSessionDatas");
        }
    }
}
