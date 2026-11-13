using Bookstore.Contracts;

namespace Catalog.Api.Services;

/// <summary>
/// Abstraction for querying and persisting catalog items in an attached backing datastore.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// Defining a narrow contract (<see cref="ICatalogRepository"/>) decouples <c>Catalog.Api</c> request handlers
/// from the underlying relational engine details while <see cref="PostgresCatalogRepository"/> binds to any
/// PostgreSQL-compatible endpoint supplied via environment configuration.
/// </remarks>
public interface ICatalogRepository
{
    /// <summary>
    /// Retrieves all books from the attached catalog datastore ordered by title.
    /// </summary>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="BookDto"/> records.</returns>
    Task<IReadOnlyList<BookDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single book by its unique identifier from the attached catalog datastore.
    /// </summary>
    /// <param name="id">The unique identifier of the book.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The matching <see cref="BookDto"/> if found; otherwise, <see langword="null"/>.</returns>
    Task<BookDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Inserts a new book or updates an existing book in the attached catalog datastore.
    /// </summary>
    /// <param name="book">The book contract state to persist.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The persisted <see cref="BookDto"/> record.</returns>
    Task<BookDto> UpsertAsync(BookDto book, CancellationToken ct = default);
}
