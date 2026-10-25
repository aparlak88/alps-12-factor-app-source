namespace Bookstore.Contracts;

/// <summary>
/// Integration event published when a customer places an order.
/// </summary>
/// <param name="OrderId">The unique identifier of the created order.</param>
/// <param name="CustomerId">The customer identifier who placed the order.</param>
/// <param name="Items">The items included in the placed order.</param>
/// <param name="PlacedAtUtc">The UTC timestamp indicating when the order was placed.</param>
/// <remarks>
/// 12-Factor Principle: Factor I — Codebase
/// One codebase tracked in revision control, many deploys.
/// 
/// Sharing immutable event contracts like <see cref="OrderPlacedEvent"/> within one tracked repository prevents
/// schema drift between event publishers (e.g., Ordering.Api) and asynchronous subscribers (e.g., Fulfillment.Worker).
/// It avoids the anti-pattern of maintaining independent, out-of-sync event definitions in separate repositories.
/// </remarks>
public sealed record OrderPlacedEvent(Guid OrderId, string CustomerId, IReadOnlyList<BasketItemDto> Items, DateTimeOffset PlacedAtUtc)
{
    /// <summary>
    /// Validates that the order placed event satisfies domain invariants.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="CustomerId"/> is null, empty, or whitespace, or when <see cref="Items"/> is null or empty.
    /// </exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CustomerId))
        {
            throw new ArgumentException("Customer ID cannot be null or whitespace.", nameof(CustomerId));
        }

        if (Items is null || Items.Count == 0)
        {
            throw new ArgumentException("Order must contain at least one item.", nameof(Items));
        }
    }
}
