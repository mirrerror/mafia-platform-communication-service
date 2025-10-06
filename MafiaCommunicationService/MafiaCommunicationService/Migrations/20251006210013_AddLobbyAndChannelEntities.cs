using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MafiaCommunicationService.Migrations
{
    /// <inheritdoc />
    public partial class AddLobbyAndChannelEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lobbies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IsGlobalChatEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lobbies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrivateChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LobbyId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivateChannels_Lobbies_LobbyId",
                        column: x => x.LobbyId,
                        principalTable: "Lobbies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrivateChannelMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<long>(type: "bigint", nullable: false),
                    PrivateChannelId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateChannelMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivateChannelMembers_PrivateChannels_PrivateChannelId",
                        column: x => x.PrivateChannelId,
                        principalTable: "PrivateChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateChannelMembers_PrivateChannelId_MemberId",
                table: "PrivateChannelMembers",
                columns: new[] { "PrivateChannelId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivateChannels_LobbyId_Name",
                table: "PrivateChannels",
                columns: new[] { "LobbyId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrivateChannelMembers");

            migrationBuilder.DropTable(
                name: "PrivateChannels");

            migrationBuilder.DropTable(
                name: "Lobbies");
        }
    }
}
