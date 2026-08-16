using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoufirTours.Migrations
{
    /// <inheritdoc />
    public partial class initialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "AutoTripPlans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    is_enabled = table.Column<int>(type: "int", nullable: false),
                    is_done = table.Column<bool>(type: "bit", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    schedule_type = table.Column<int>(type: "int", nullable: false),
                    specific_date = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    activation_mode = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoTripPlans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "buses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    bus_number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    chassis_number = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    plate_number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    manufacturer = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    model_name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    model_year = table.Column<int>(type: "int", nullable: true),
                    bus_type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    seats_count = table.Column<int>(type: "int", nullable: true),
                    color = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    specs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<int>(type: "int", nullable: false),
                    is_archived = table.Column<int>(type: "int", nullable: false),
                    archived_at = table.Column<long>(type: "bigint", nullable: true),
                    archived_by_user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    layout_width = table.Column<int>(type: "int", nullable: false),
                    layout_height = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    full_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    created_at_unix = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    full_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    national_id = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    license_number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    license_expiry_at = table.Column<long>(type: "bigint", nullable: true),
                    joined_at = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<int>(type: "int", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    is_archived = table.Column<int>(type: "int", nullable: false),
                    archived_at = table.Column<long>(type: "bigint", nullable: true),
                    archived_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drivers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalSupports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ComplaintsPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    UpdatedAtUnix = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsSingleton = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalSupports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    username = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    pass_hash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    is_active = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bus_seats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    bus_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    seat_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    pos_x = table.Column<int>(type: "int", nullable: false),
                    pos_y = table.Column<int>(type: "int", nullable: false),
                    element_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<int>(type: "int", nullable: false),
                    role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    assigned_driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    door_side = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    door_offset = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bus_seats", x => x.id);
                    table.ForeignKey(
                        name: "FK_bus_seats_buses_bus_id",
                        column: x => x.bus_id,
                        principalTable: "buses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AutoTripPlanItems",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    plan_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_no = table.Column<int>(type: "int", nullable: false),
                    is_enabled = table.Column<int>(type: "int", nullable: false),
                    trip_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    depart_time = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    price_type = table.Column<int>(type: "int", nullable: false),
                    from_city = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    to_city = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    pickup_place = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    pickup_lat = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                    pickup_lon = table.Column<decimal>(type: "decimal(10,7)", precision: 10, scale: 7, nullable: false),
                    dropoff_place = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    seat_price_go = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    seat_price_return = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    bus_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoTripPlanItems", x => x.id);
                    table.ForeignKey(
                        name: "FK_AutoTripPlanItems_AutoTripPlans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "AutoTripPlans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AutoTripPlanItems_buses_bus_id",
                        column: x => x.bus_id,
                        principalTable: "buses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AutoTripPlanItems_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "driver_phones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    phone_number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    is_primary = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_phones", x => x.id);
                    table.ForeignKey(
                        name: "FK_driver_phones_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    action = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    entity = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    entity_id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_log_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    trip_origin = table.Column<int>(type: "int", nullable: false),
                    auto_plan_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    auto_plan_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    trip_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    depart_date = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    depart_time = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    from_city = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    to_city = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    pickup_place = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    pickup_lat = table.Column<decimal>(type: "decimal(10,7)", nullable: true),
                    pickup_lon = table.Column<decimal>(type: "decimal(10,7)", nullable: true),
                    dropoff_lat = table.Column<decimal>(type: "decimal(10,7)", nullable: true),
                    dropoff_lon = table.Column<decimal>(type: "decimal(10,7)", nullable: true),
                    dropoff_place = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    driver_phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    driver_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    seat_price_go = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    seat_price_return = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    is_archived = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<int>(type: "int", nullable: false),
                    archived_at = table.Column<long>(type: "bigint", nullable: true),
                    archived_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    bus_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    driver_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    price_type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trips", x => x.id);
                    table.ForeignKey(
                        name: "FK_trips_buses_bus_id",
                        column: x => x.bus_id,
                        principalTable: "buses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_trips_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_trips_users_driver_user_id",
                        column: x => x.driver_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "trip_places",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    place_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    place_type = table.Column<int>(type: "int", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    lat = table.Column<double>(type: "float", nullable: true),
                    lon = table.Column<double>(type: "float", nullable: true),
                    is_active = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_places", x => x.id);
                    table.ForeignKey(
                        name: "FK_trip_places_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    customer_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    company_from = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    seats = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    return_datetime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    paid_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    is_canceled = table.Column<int>(type: "int", nullable: false),
                    canceled_at = table.Column<long>(type: "bigint", nullable: true),
                    canceled_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    cancel_note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    booking_type = table.Column<int>(type: "int", nullable: false),
                    return_trip_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    seats_return = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    destination_place_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    destination_place_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    return_destination_place_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    return_destination_place_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.id);
                    table.ForeignKey(
                        name: "FK_bookings_trip_places_destination_place_id",
                        column: x => x.destination_place_id,
                        principalTable: "trip_places",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_bookings_trip_places_return_destination_place_id",
                        column: x => x.return_destination_place_id,
                        principalTable: "trip_places",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_bookings_trips_return_trip_id",
                        column: x => x.return_trip_id,
                        principalTable: "trips",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_bookings_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bookings_users_canceled_by_user_id",
                        column: x => x.canceled_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_bookings_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_codes",
                columns: table => new
                {
                    booking_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_codes", x => x.booking_id);
                    table.ForeignKey(
                        name: "FK_booking_codes_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    booking_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    method = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    collected_at = table.Column<long>(type: "bigint", nullable: false),
                    collected_by_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_collections", x => x.id);
                    table.ForeignKey(
                        name: "FK_booking_collections_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_collections_users_collected_by_user_id",
                        column: x => x.collected_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_audit_user",
                table: "audit_log",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_AutoTripPlanItems_bus_id",
                table: "AutoTripPlanItems",
                column: "bus_id");

            migrationBuilder.CreateIndex(
                name: "IX_AutoTripPlanItems_driver_id",
                table: "AutoTripPlanItems",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_AutoTripPlanItems_plan_id_order_no",
                table: "AutoTripPlanItems",
                columns: new[] { "plan_id", "order_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_booking_codes_code",
                table: "booking_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_booking_collections_booking",
                table: "booking_collections",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "idx_booking_collections_user",
                table: "booking_collections",
                column: "collected_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_bookings_created_by_user",
                table: "bookings",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_bookings_return_trip",
                table: "bookings",
                column: "return_trip_id");

            migrationBuilder.CreateIndex(
                name: "idx_bookings_trip",
                table: "bookings",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_canceled_by_user_id",
                table: "bookings",
                column: "canceled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_destination_place_id",
                table: "bookings",
                column: "destination_place_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_return_destination_place_id",
                table: "bookings",
                column: "return_destination_place_id");

            migrationBuilder.CreateIndex(
                name: "ux_bus_seats_busid_seatcode",
                table: "bus_seats",
                columns: new[] { "bus_id", "seat_code" },
                unique: true,
                filter: "[seat_code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_buses_bus_number",
                table: "buses",
                column: "bus_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_buses_chassis_number",
                table: "buses",
                column: "chassis_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_buses_plate_number",
                table: "buses",
                column: "plate_number",
                unique: true,
                filter: "[plate_number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_customers_name_phone",
                table: "customers",
                columns: new[] { "full_name", "phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_driver_phones_driverid_phone",
                table: "driver_phones",
                columns: new[] { "driver_id", "phone_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_driver_phones_phone_number",
                table: "driver_phones",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_drivers_national_id",
                table: "drivers",
                column: "national_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_places_trip_id",
                table: "trip_places",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "idx_trips_active",
                table: "trips",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_trips_archived",
                table: "trips",
                column: "is_archived");

            migrationBuilder.CreateIndex(
                name: "idx_trips_bus_id",
                table: "trips",
                column: "bus_id");

            migrationBuilder.CreateIndex(
                name: "idx_trips_depart_dt",
                table: "trips",
                columns: new[] { "depart_date", "depart_time" });

            migrationBuilder.CreateIndex(
                name: "idx_trips_driver_id",
                table: "trips",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_trips_driver_user_id",
                table: "trips",
                column: "driver_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_trips_depdate_busid",
                table: "trips",
                columns: new[] { "depart_date", "bus_id" },
                unique: true,
                filter: "[is_archived] = 0 AND [bus_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_trips_depdate_driverid",
                table: "trips",
                columns: new[] { "depart_date", "driver_id" },
                unique: true,
                filter: "[is_archived] = 0 AND [driver_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_trips_depdate_tripname",
                table: "trips",
                columns: new[] { "depart_date", "trip_name" },
                unique: true,
                filter: "[is_archived] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "AutoTripPlanItems");

            migrationBuilder.DropTable(
                name: "booking_codes");

            migrationBuilder.DropTable(
                name: "booking_collections");

            migrationBuilder.DropTable(
                name: "bus_seats");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "driver_phones");

            migrationBuilder.DropTable(
                name: "TechnicalSupports");

            migrationBuilder.DropTable(
                name: "AutoTripPlans");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "trip_places");

            migrationBuilder.DropTable(
                name: "trips");

            migrationBuilder.DropTable(
                name: "buses");

            migrationBuilder.DropTable(
                name: "drivers");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
