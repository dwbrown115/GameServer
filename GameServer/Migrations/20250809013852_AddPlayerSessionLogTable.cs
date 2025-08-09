using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerSessionLogTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "gameplay");

            migrationBuilder.CreateTable(
                name: "PlayerSessionLog",
                schema: "gameplay",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlayerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SessionStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SessionEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientObjCount = table.Column<int>(type: "int", nullable: true),
                    ServerObjCount = table.Column<int>(type: "int", nullable: true),
                    ObjectSyncHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HashMismatch = table.Column<bool>(type: "bit", nullable: true),
                    SyncStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DesyncResolution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RadiusEnforced = table.Column<bool>(type: "bit", nullable: true),
                    ObjectLifecycleLog = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScoreServer = table.Column<int>(type: "int", nullable: false),
                    AttemptedClientScore = table.Column<int>(type: "int", nullable: false),
                    FakeObjectDetected = table.Column<bool>(type: "bit", nullable: true),
                    PickupEventsVerified = table.Column<int>(type: "int", nullable: true),
                    SpawnRequests = table.Column<int>(type: "int", nullable: true),
                    ValidatedSpawns = table.Column<int>(type: "int", nullable: true),
                    BlockedSpawns = table.Column<int>(type: "int", nullable: true),
                    SpawnRateFlagged = table.Column<bool>(type: "bit", nullable: true),
                    SessionMetadata = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GameVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FlaggedForReview = table.Column<bool>(type: "bit", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSessionLog", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerSessionLog",
                schema: "gameplay");
        }
    }
}
