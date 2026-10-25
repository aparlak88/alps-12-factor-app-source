namespace Bookstore.Contracts;

/// <summary>
/// Represents a book in the catalog domain.
/// </summary>
/// <param name="Id">The unique identifier of the book.</param>
/// <param name="Isbn">The International Standard Book Number.</param>
/// <param name="Title">The title of the book.</param>
/// <param name="Author">The author of the book.</param>
/// <param name="Price">The retail price of the book.</param>
/// <param name="StockQuantity">The available inventory count for the book.</param>
/// <remarks>
/// 12-Factor Principle: Factor I — Codebase
/// One codebase tracked in revision control, many deploys.
/// 
/// Sharing immutable contract records like <see cref="BookDto"/> inside a single tracked repository
/// prevents the Factor I anti-pattern of copy-pasting DTO classes across disconnected repositories
/// or drifting contract schemas between catalog, ordering, and inventory services.
/// </remarks>
public sealed record BookDto(Guid Id, string Isbn, string Title, string Author, decimal Price, int StockQuantity)
{
    /// <summary>
    /// Validates domain invariants for the book.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="Price"/> or <see cref="StockQuantity"/> is negative.
    /// </exception>
    public void Validate()
    {
        if (Price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Price), Price, "Price cannot be negative.");
        }

        if (StockQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StockQuantity), StockQuantity, "Stock quantity cannot be negative.");
        }
    }
}
