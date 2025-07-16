using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Migrations
{
    /// <inheritdoc />
    public partial class fixSecureApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "users");

            migrationBuilder.RenameTable(
                name: "Users",
                schema: "auth",
                newName: "Users",
                newSchema: "users");

            migrationBuilder.RenameColumn(
                name: "UserId",
                schema: "users",
                table: "Users",
                newName: "UUID");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "users",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.RenameTable(
                name: "Users",
                schema: "users",
                newName: "Users",
                newSchema: "auth");

            migrationBuilder.RenameColumn(
                name: "UUID",
                schema: "auth",
                table: "Users",
                newName: "UserId");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "auth",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier")
                .Annotation("SqlServer:Identity", "1, 1");
        }
    }
}
