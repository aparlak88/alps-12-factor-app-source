var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Run();

/// <summary>
/// Entry point for the Ordering.Api service.
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
