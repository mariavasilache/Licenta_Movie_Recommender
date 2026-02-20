using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Licenta_Movie_Recommender.Migrations
{
    /// <inheritdoc />
    public partial class AdaugatMovieLensId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MovieLensId",
                table: "Movies",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MovieLensId",
                table: "Movies");
        }
    }
}
