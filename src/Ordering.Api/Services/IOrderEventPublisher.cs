using Bookstore.Contracts;

namespace Ordering.Api.Services;

/// <summary>
/// Abstraction for publishing order integration events to an attached message broker backing service.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor IV — Backing Services
/// Treat backing services as attached resources.
///
/// Defining <see cref="IOrderEventPublisher"/> decouples order placement workflows in <c>Ordering.Api</c> from
/// broker transport specifics while <see cref="RabbitMqOrderEventPublisher"/> attaches to any AMQP 0-9-1 endpoint
/// configured via <c>Bookstore__RabbitMq__Uri</c> without code changes.
/// </remarks>
public interface IOrderEventPublisher
{
    /// <summary>
    /// Publishes an <see cref="OrderPlacedEvent"/> message to the attached message broker queue.
    /// </summary>
    /// <param name="orderEvent">The order-placed integration event to publish.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    Task PublishOrderPlacedAsync(OrderPlacedEvent orderEvent, CancellationToken ct = default);
}
