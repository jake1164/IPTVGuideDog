using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPTVGuideDog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigYamlSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "config_source_path",
                table: "providers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "needs_env_var_substitution",
                table: "providers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "config_source_path",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "needs_env_var_substitution",
                table: "providers");
        }
    }
}
