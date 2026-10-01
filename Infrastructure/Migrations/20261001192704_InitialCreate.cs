using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auction_states",
                columns: table => new
                {
                    state_id = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_states", x => new { x.state_id, x.language });
                });

            migrationBuilder.CreateTable(
                name: "bid_states",
                columns: table => new
                {
                    state_id = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bid_states", x => new { x.state_id, x.language });
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    response_body_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_records", x => new { x.idempotency_key, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    availability_id = table.Column<int>(type: "integer", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    processed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    availability_id = table.Column<int>(type: "integer", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_organizations_org_id",
                        column: x => x.org_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auction_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    before_state_id = table.Column<int>(type: "integer", nullable: false),
                    after_state_id = table.Column<int>(type: "integer", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_auction_audit_logs_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auctions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    starting_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    reserve_min_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    min_increment = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    starts_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    state_id = table.Column<int>(type: "integer", nullable: false),
                    current_high_bid_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    availability_id = table.Column<int>(type: "integer", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auctions", x => x.id);
                    table.ForeignKey(
                        name: "fk_auctions_organizations_org_id",
                        column: x => x.org_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_auctions_users_seller_user_id",
                        column: x => x.seller_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bids",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bidder_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    placed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state_id = table.Column<int>(type: "integer", nullable: false),
                    reject_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    availability_id = table.Column<int>(type: "integer", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bids", x => x.id);
                    table.ForeignKey(
                        name: "fk_bids_auctions_auction_id",
                        column: x => x.auction_id,
                        principalTable: "auctions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bids_users_bidder_user_id",
                        column: x => x.bidder_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "auction_states",
                columns: new[] { "language", "state_id", "name" },
                values: new object[,]
                {
                    { "ar", 1, "مسودة" },
                    { "en", 1, "Draft" },
                    { "ar", 2, "مجدول" },
                    { "en", 2, "Scheduled" },
                    { "ar", 3, "نشط" },
                    { "en", 3, "Live" },
                    { "ar", 4, "مغلق" },
                    { "en", 4, "Closed" },
                    { "ar", 5, "تمت التسوية" },
                    { "en", 5, "Settled" },
                    { "ar", 6, "ملغى" },
                    { "en", 6, "Cancelled" }
                });

            migrationBuilder.InsertData(
                table: "bid_states",
                columns: new[] { "language", "state_id", "name" },
                values: new object[,]
                {
                    { "ar", 1, "مقبول" },
                    { "en", 1, "Accepted" },
                    { "ar", 2, "مرفوض" },
                    { "en", 2, "Rejected" },
                    { "ar", 3, "تم تجاوزه" },
                    { "en", 3, "Outbid" },
                    { "ar", 4, "فائز" },
                    { "en", 4, "Winning" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_auction_audit_logs_actor_user_id",
                table: "auction_audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_auction_audit_logs_auction_id_id",
                table: "auction_audit_logs",
                columns: new[] { "auction_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_auctions_current_high_bid_id",
                table: "auctions",
                column: "current_high_bid_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Listing_Pagination",
                table: "auctions",
                columns: new[] { "org_id", "availability_id", "created_utc", "id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_auctions_seller_user_id",
                table: "auctions",
                column: "seller_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_bids_bidder_user_id_placed_utc",
                table: "bids",
                columns: new[] { "bidder_user_id", "placed_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_bids_idempotency_key",
                table: "bids",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bids_Leaderboard_Amount",
                table: "bids",
                columns: new[] { "auction_id", "availability_id", "amount", "placed_utc" },
                descending: new[] { false, false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_utc",
                table: "outbox_messages",
                column: "occurred_utc",
                filter: "\"processed_utc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_org_id",
                table: "users",
                column: "org_id");

            migrationBuilder.AddForeignKey(
                name: "fk_auction_audit_logs_auctions_auction_id",
                table: "auction_audit_logs",
                column: "auction_id",
                principalTable: "auctions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_auctions_bids_current_high_bid_id",
                table: "auctions",
                column: "current_high_bid_id",
                principalTable: "bids",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bids_auctions_auction_id",
                table: "bids");

            migrationBuilder.DropTable(
                name: "auction_audit_logs");

            migrationBuilder.DropTable(
                name: "auction_states");

            migrationBuilder.DropTable(
                name: "bid_states");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "auctions");

            migrationBuilder.DropTable(
                name: "bids");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
