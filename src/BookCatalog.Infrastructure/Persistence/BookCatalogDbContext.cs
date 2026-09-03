using Microsoft.EntityFrameworkCore;

namespace BookCatalog.Infrastructure.Persistence;

public sealed class BookCatalogDbContext(DbContextOptions<BookCatalogDbContext> options)
    : DbContext(options);
