using Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

/// <summary>
/// Entity Framework Core database context for the <c>Catalog.Api</c> PostgreSQL backing service.
/// </summary>
/// <param name="options">The options configuring this context instance, bound from environment configuration.</param>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// <see cref="CatalogDbContext"/> treats the PostgreSQL relational database strictly as an attached resource.
/// It never embeds hardcoded connection strings or environment-conditional connection logic; instead, its
/// <see cref="DbContextOptions{TContext}"/> are configured at startup from <c>IOptions&lt;BookstoreConfigOptions&gt;</c>
/// (<c>Bookstore__Postgres__ConnectionString</c>). Operators can detach a local PostgreSQL container and attach
/// a managed Amazon RDS or Google Cloud SQL instance solely by modifying the environment variable without
/// touching or recompiling application code.
/// </remarks>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the queryable set of <see cref="Book"/> entities in the attached catalog database.
    /// </summary>
    public DbSet<Book> Books => Set<Book>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.HasIndex(b => b.Isbn)
                .IsUnique();

            entity.Property(b => b.Isbn)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(b => b.Title)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(b => b.Author)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(b => b.Price)
                .HasPrecision(18, 2);
        });
    }
}
