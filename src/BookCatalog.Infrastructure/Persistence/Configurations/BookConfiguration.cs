using BookCatalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookCatalog.Infrastructure.Persistence.Configurations;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books", table =>
            table.HasCheckConstraint(
                "CK_Books_PublicationYear_Minimum",
                "\"PublicationYear\" >= 1450"));

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Id)
            .ValueGeneratedNever();
        builder.Property(entity => entity.Title)
            .IsRequired()
            .HasMaxLength(200);
        builder.HasOne(entity => entity.Author)
            .WithMany()
            .HasForeignKey(entity => entity.AuthorId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(entity => entity.Isbn)
            .IsRequired()
            .HasMaxLength(13);
        builder.Property(entity => entity.PublicationYear)
            .IsRequired();
        builder.Property(entity => entity.Description)
            .HasMaxLength(2000);

        builder.HasIndex(entity => entity.Isbn)
            .IsUnique();
        builder.HasIndex(entity => new { entity.Title, entity.Id });
    }
}
