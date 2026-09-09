using BookCatalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookCatalog.Infrastructure.Persistence.Configurations;

internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    internal const string ActiveLoanIndex = "IX_Loans_BookId_Active";
    internal const string BookForeignKey = "FK_Loans_Books_BookId";
    internal const string UserForeignKey = "FK_Loans_Users_UserId";

    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("Loans", table => table.HasCheckConstraint(
            "CK_Loans_ReturnedAt_AfterBorrowedAt", "\"ReturnedAt\" IS NULL OR \"ReturnedAt\" >= \"BorrowedAt\""));
        builder.HasKey(loan => loan.Id);
        builder.Property(loan => loan.Id).ValueGeneratedNever();
        builder.Property(loan => loan.BorrowedAt).IsRequired();
        builder.HasOne(loan => loan.Book).WithMany().HasForeignKey(loan => loan.BookId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName(BookForeignKey);
        builder.HasOne(loan => loan.User).WithMany().HasForeignKey(loan => loan.UserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName(UserForeignKey);

        builder.HasIndex(loan => loan.BookId).IsUnique()
            .HasFilter("\"ReturnedAt\" IS NULL").HasDatabaseName(ActiveLoanIndex);
        builder.HasIndex(loan => new { loan.BookId, loan.BorrowedAt, loan.Id }).IsDescending(false, true, true);
        builder.HasIndex(loan => new { loan.UserId, loan.BorrowedAt, loan.Id }).IsDescending(false, true, true);
    }
}
