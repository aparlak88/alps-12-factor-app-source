using Bookstore.Contracts;

namespace Ordering.Api.Services;

/// <summary>
/// Abstraction for reading, upserting, and deleting customer shopping baskets in an attached key-value backing service.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// Defining <see cref="IBasketStore"/> decouples <c>Ordering.Api</c> domain logic from any specific Redis deployment
/// topology while <see cref="RedisBasketStore"/> binds purely via environment-injected configuration handles
/// (<c>IOptions&lt;BookstoreConfigOptions&gt;</c>). A local OrbStack Redis container can be swapped for Amazon ElastiCache
/// or Azure Cache for Redis without modifying application code.
/// </remarks>
public interface IBasketStore
{
    /// <summary>
    /// Retrieves the shopping basket for the specified customer from the attached key-value store.
    /// </summary>
    /// <param name="customerId">The unique customer identifier owning the basket.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The <see cref="CustomerBasketDto"/> if found; otherwise, <see langword="null"/>.</returns>
    Task<CustomerBasketDto?> GetBasketAsync(string customerId, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a customer's shopping basket in the attached key-value store.
    /// </summary>
    /// <param name="basket">The shopping basket state to persist.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The persisted <see cref="CustomerBasketDto"/> instance.</returns>
    Task<CustomerBasketDto> UpsertBasketAsync(CustomerBasketDto basket, CancellationToken ct = default);

    /// <summary>
    /// Removes the shopping basket for the specified customer from the attached key-value store.
    /// </summary>
    /// <param name="customerId">The unique customer identifier owning the basket.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns><see langword="true"/> if the basket existed and was removed; otherwise, <see langword="false"/>.</returns>
    Task<bool> DeleteBasketAsync(string customerId, CancellationToken ct = default);
}
