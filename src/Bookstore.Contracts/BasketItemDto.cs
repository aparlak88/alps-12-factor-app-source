namespace Bookstore.Contracts;

/// <summary>
/// Represents an item line within a customer's shopping basket.
/// </summary>
/// <param name="BookId">The unique identifier of the referenced book.</param>
/// <param name="Title">The title of the book at the time of addition.</param>
/// <param name="Quantity">The quantity of the book in the basket.</param>
/// <param name="UnitPrice">The unit price of the book at the time of addition.</param>
/// <remarks>
/// 12-Factor Principle: Factor I — Codebase
/// One codebase tracked in revision control, many deploys.
/// 
/// Sharing immutable contract records like <see cref="BasketItemDto"/> inside a single codebase prevents
/// DTO duplication and schema drift across microservices (such as Ordering and Fulfillment) while ensuring
/// all deploy targets share a canonical representation of a line item.
/// </remarks>
public sealed record BasketItemDto(Guid BookId, string Title, int Quantity, decimal UnitPrice);
