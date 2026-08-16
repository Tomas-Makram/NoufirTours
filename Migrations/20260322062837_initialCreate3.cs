using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoufirTours.Migrations
{
    /// <inheritdoc />
    public partial class initialCreate3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deleted_tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    booking_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ticket_code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    return_trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    customer_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    company_from = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    seats = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    seats_return = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    booking_type = table.Column<int>(type: "int", nullable: false),
                    paid_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    destination_place_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    return_destination_place_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<long>(type: "bigint", nullable: false),
                    deleted_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    delete_reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    trip_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    trip_depart_date = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    trip_depart_time = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    trip_from_city = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    trip_to_city = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    return_trip_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    return_trip_depart_date = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    return_trip_depart_time = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    return_trip_from_city = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    return_trip_to_city = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deleted_tickets", x => x.id);
                    table.ForeignKey(
                        name: "FK_deleted_tickets_users_deleted_by_user_id",
                        column: x => x.deleted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_deleted_tickets_deleted_by_user_id",
                table: "deleted_tickets",
                column: "deleted_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deleted_tickets");
        }
    }
}
