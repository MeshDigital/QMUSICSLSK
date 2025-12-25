using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SLSKDONET.Migrations
{
    /// <inheritdoc />
    public partial class AddQueuePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Integrity",
                table: "PlaylistTracks");

            migrationBuilder.AddColumn<int>(
                name: "MinBitrateOverride",
                table: "Tracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryTime",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredFormats",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Tracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePlaylistId",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePlaylistName",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Danceability",
                table: "PlaylistTracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Energy",
                table: "PlaylistTracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Valence",
                table: "PlaylistTracks",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "WaveformData",
                table: "PlaylistTracks",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BronzeCount",
                table: "LibraryHealth",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GoldCount",
                table: "LibraryHealth",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SilverCount",
                table: "LibraryHealth",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "LibraryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinBitrateOverride",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "NextRetryTime",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "PreferredFormats",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "SourcePlaylistId",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "SourcePlaylistName",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Danceability",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "Energy",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "Valence",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "WaveformData",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "BronzeCount",
                table: "LibraryHealth");

            migrationBuilder.DropColumn(
                name: "GoldCount",
                table: "LibraryHealth");

            migrationBuilder.DropColumn(
                name: "SilverCount",
                table: "LibraryHealth");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "LibraryEntries");

            migrationBuilder.AddColumn<int>(
                name: "Integrity",
                table: "PlaylistTracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
