using BookCatalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookCatalog.Infrastructure.Persistence.Configurations;

internal sealed class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.ToTable("Authors", table => table.HasCheckConstraint(
            "CK_Authors_Name_NotBlank", "btrim(\"Name\") <> ''"));
        builder.HasKey(author => author.Id);
        builder.Property(author => author.Id).ValueGeneratedNever();
        builder.Property(author => author.Name).IsRequired().HasMaxLength(150);
        builder.HasIndex(author => new { author.Name, author.Id });
    }
}
