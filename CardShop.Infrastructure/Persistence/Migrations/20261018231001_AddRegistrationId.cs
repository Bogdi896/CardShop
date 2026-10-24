using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RegistrationId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_RegistrationId",
                table: "AspNetUsers",
                column: "RegistrationId",
                unique: true,
                filter: "[RegistrationId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_RegistrationId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RegistrationId",
                table: "AspNetUsers");
        }
    }
}
