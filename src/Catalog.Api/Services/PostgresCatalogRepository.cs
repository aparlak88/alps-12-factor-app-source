using Bookstore.Contracts;
using Catalog.Api.Data;
using Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

/// <summary>
/// PostgreSQL implementation of <see cref="ICatalogRepository"/> backed by <see cref="CatalogDbContext"/>.
/// </summary>
/// <param name="dbContext">The <see cref="CatalogDbContext"/> connected to the attached PostgreSQL instance.</param>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// <see cref="PostgresCatalogRepository"/> operates identically against a local Docker PostgreSQL container,
/// a staging database, or a multi-AZ managed cloud instance (such as Amazon RDS or Google Cloud SQL). Because
/// the underlying <see cref="CatalogDbContext"/> resolves its connection string exclusively from environment-bound
/// <c>IOptions&lt;BookstoreConfigOptions&gt;</c>, attaching or swapping the catalog database requires zero code changes.
/// </remarks>
public sealed class PostgresCatalogRepository(CatalogDbContext dbContext) : ICatalogRepository
{
    private readonly CatalogDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    public async Task<IReadOnlyList<BookDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _dbContext.Books
            .AsNoTracking()
            .OrderBy(b => b.Title)
            .ToListAsync(ct);

        return entities.Select(b => b.ToDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<BookDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbContext.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        return entity?.ToDto();
    }

    /// <inheritdoc />
    public async Task<BookDto> UpsertAsync(BookDto book, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        book.Validate();

        var existing = await _dbContext.Books
            .FirstOrDefaultAsync(b => b.Id == book.Id, ct);

        if (existing is null)
        {
            var newEntity = new Book
            {
                Id = book.Id == Guid.Empty ? Guid.NewGuid() : book.Id,
                Isbn = book.Isbn,
                Title = book.Title,
                Author = book.Author,
                Price = book.Price,
                StockQuantity = book.StockQuantity
            };

            _dbContext.Books.Add(newEntity);
            await _dbContext.SaveChangesAsync(ct);
            return newEntity.ToDto();
        }

        existing.Isbn = book.Isbn;
        existing.Title = book.Title;
        existing.Author = book.Author;
        existing.Price = book.Price;
        existing.StockQuantity = book.StockQuantity;

        await _dbContext.SaveChangesAsync(ct);
        return existing.ToDto();
    }
}
