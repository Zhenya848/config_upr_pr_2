namespace DependencyGraphApp.Models;

public sealed record PackageRecord(
    string Name,
    string Version,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string>? Provides = null);
