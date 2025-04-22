using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class RoundstartEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_equipment",
                table: "equipment");

            migrationBuilder.AddPrimaryKey(
                name: "PK_equipment",
                table: "equipment",
                columns: new[] { "player_user_id", "proto_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_equipment",
                table: "equipment");

            migrationBuilder.AddPrimaryKey(
                name: "PK_equipment",
                table: "equipment",
                columns: new[] { "player_user_id", "proto_id", "amount" });
        }
    }
}
