using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrapheneTrace.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedByToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBy",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ApprovedBy",
                table: "AspNetUsers",
                column: "ApprovedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_ApprovedBy",
                table: "AspNetUsers",
                column: "ApprovedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_ApprovedBy",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ApprovedBy",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "AspNetUsers");
        }
    }
}
