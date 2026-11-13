using Bookstore.Contracts;

namespace Catalog.Api.Domain;

/// <summary>
/// Persistence entity representing a book stored in the attached PostgreSQL catalog database.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// The <see cref="Book"/> entity models the relational row persisted in whichever PostgreSQL instance
/// is currently attached to <c>Catalog.Api</c> via environment configuration (for example, a local
/// Docker container during development or a managed Amazon RDS / Google Cloud SQL instance in production).
/// Application code maps this persistence model to the transport-neutral <see cref="BookDto"/> contract
/// via <see cref="ToDto"/> without coupling domain logic to a specific database deployment topology.
/// </remarks>
public sealed class Book
{
    /// <summary>
    /// Gets or sets the unique identifier of the book.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique International Standard Book Number (ISBN).
    /// </summary>
    public string Isbn { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the book.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author of the book.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the retail unit price of the book.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Gets or sets the available inventory count for the book.
    /// </summary>
    public int StockQuantity { get; set; }

    /// <summary>
    /// Projects this persistence entity into an immutable <see cref="BookDto"/> contract record.
    /// </summary>
    /// <returns>A <see cref="BookDto"/> containing the current book state.</returns>
    public BookDto ToDto() => new(Id, Isbn, Title, Author, Price, StockQuantity);
}
