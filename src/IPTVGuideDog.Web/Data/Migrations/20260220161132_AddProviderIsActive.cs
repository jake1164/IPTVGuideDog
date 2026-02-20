using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPTVGuideDog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "providers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "idx_providers_is_active",
                table: "providers",
                column: "is_active",
                unique: true,
                filter: "is_active = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_providers_is_active",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "providers");
        }
    }
}
