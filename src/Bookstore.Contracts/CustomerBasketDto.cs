namespace Bookstore.Contracts;

/// <summary>
/// Represents a customer's active shopping basket containing line items.
/// </summary>
/// <param name="CustomerId">The unique customer identifier owning the basket.</param>
/// <param name="Items">The collection of items currently placed in the basket.</param>
/// <remarks>
/// 12-Factor Principle: Factor I — Codebase
/// One codebase tracked in revision control, many deploys.
/// 
/// Sharing immutable contract records like <see cref="CustomerBasketDto"/> inside a single codebase prevents
/// DTO divergence and ensures consistent domain calculations (such as basket totals) across API services.
/// </remarks>
public sealed record CustomerBasketDto(string CustomerId, IReadOnlyList<BasketItemDto> Items)
{
    /// <summary>
    /// Gets the total price for all line items within this shopping basket.
    /// </summary>
    public decimal TotalPrice => Items.Sum(i => i.Quantity * i.UnitPrice);
}
