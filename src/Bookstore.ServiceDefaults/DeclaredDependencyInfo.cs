namespace Bookstore.ServiceDefaults;

/// <summary>
/// Represents an explicitly declared compile-time and runtime assembly dependency.
/// </summary>
/// <remarks>
/// 12-Factor Principle: Factor II — Dependencies
/// Explicitly declare and isolate dependencies.
///
/// A twelve-factor application never relies on the implicit existence of system-wide packages or libraries.
/// It declares all dependencies, completely and exactly, via a dependency declaration manifest
/// (<c>Directory.Packages.props</c> and project references), and uses a dependency isolation tool during
/// execution so no implicit dependencies leak in from the surrounding system.
///
/// <see cref="DeclaredDependencyInfo"/> captures each referenced assembly's canonical name and pinned version
/// as embedded in the compiled assembly manifest, enabling runtime verification of explicit dependency graphs.
/// </remarks>
/// <param name="AssemblyName">The simple name of the referenced assembly.</param>
/// <param name="Version">The four-part version string of the referenced assembly (or "0.0.0.0" if unspecified).</param>
public sealed record DeclaredDependencyInfo(string AssemblyName, string Version);
