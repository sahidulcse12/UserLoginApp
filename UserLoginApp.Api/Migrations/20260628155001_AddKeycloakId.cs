using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserLoginApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKeycloakId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KeycloakId",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeycloakId",
                table: "Users");
        }
    }
}
