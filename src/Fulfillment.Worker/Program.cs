var builder = Host.CreateApplicationBuilder(args);
var host = builder.Build();
host.Run();

/// <summary>
/// Entry point for the Fulfillment.Worker service.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor I — Codebase
/// Part of the TwelveFactor.Bookstore single tracked codebase.
/// </remarks>
public partial class Program
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Program"/> class.
    /// </summary>
    protected Program() { }
}
