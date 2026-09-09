using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookCatalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "Books",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Loans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BorrowedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReturnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => x.Id);
                    table.CheckConstraint("CK_Loans_ReturnedAt_AfterBorrowedAt", "\"ReturnedAt\" IS NULL OR \"ReturnedAt\" >= \"BorrowedAt\"");
                    table.ForeignKey(
                        name: "FK_Loans_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Loans_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_BookId_Active",
                table: "Loans",
                column: "BookId",
                unique: true,
                filter: "\"ReturnedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_BookId_BorrowedAt_Id",
                table: "Loans",
                columns: new[] { "BookId", "BorrowedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_UserId_BorrowedAt_Id",
                table: "Loans",
                columns: new[] { "UserId", "BorrowedAt", "Id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Loans");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "Books");
        }
    }
}
