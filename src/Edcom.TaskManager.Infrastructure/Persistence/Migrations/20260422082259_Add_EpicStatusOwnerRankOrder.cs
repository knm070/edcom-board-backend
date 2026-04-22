using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Edcom.TaskManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_EpicStatusOwnerRankOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "epics",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "owner_id",
                table: "epics",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "rank_order",
                table: "epics",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddForeignKey(
                name: "fk_epics_users_owner_id",
                table: "epics",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "ix_epics_space_id_rank_order",
                table: "epics",
                columns: new[] { "space_id", "rank_order" });

            migrationBuilder.CreateIndex(
                name: "ix_epics_space_id_status",
                table: "epics",
                columns: new[] { "space_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_epics_space_id_rank_order",
                table: "epics");

            migrationBuilder.DropIndex(
                name: "ix_epics_space_id_status",
                table: "epics");

            migrationBuilder.DropForeignKey(
                name: "fk_epics_users_owner_id",
                table: "epics");

            migrationBuilder.DropColumn(
                name: "status",
                table: "epics");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "epics");

            migrationBuilder.DropColumn(
                name: "rank_order",
                table: "epics");
        }
    }
}
