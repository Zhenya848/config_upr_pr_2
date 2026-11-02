using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DependencyGraphApp.Models;

namespace DependencyGraphApp.Repositories;

public sealed class JsonTestRepository : IPackageRepository
{
    private static readonly Regex UppercaseName = new("^[A-Z]+$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    private readonly string _filePath;
    
    public JsonTestRepository(string filePath)
    {
        _filePath = filePath;
    }
    
    public async Task<IReadOnlyList<PackageRecord>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException("Файл тестового репозитория не найден.",
                _filePath);
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        var document = JsonSerializer.Deserialize<TestRepositoryDocument>(
            json, JsonOptions) ?? throw new JsonException("Пустое описание репозитория.");
        
        return ConvertPackages(document);
    }

    private static IReadOnlyList<PackageRecord> ConvertPackages(
        TestRepositoryDocument document)
    {
        if (document.Packages is null || document.Packages.Count == 0)
        {
            throw new InvalidDataException("В тестовом репозитории нет пакетов.");
        }

        var result = new List<PackageRecord>();
        
        foreach (var package in document.Packages)
        {
            ValidatePackage(package);
            
            result.Add(new PackageRecord(
                package.Name!, package.Version!, NormalizeDependencies(package.Dependencies),
                NormalizeDependencies(package.Provides)));
        }

        return result;
    }

    private static void ValidatePackage(TestPackage? package)
    {
        if (package is null || !UppercaseName.IsMatch(package.Name ?? string.Empty))
        {
            throw new InvalidDataException(
                "Имена пакетов тестового репозитория должны состоять " +
                "из заглавных латинских букв (A-Z).");
        }

        if (string.IsNullOrWhiteSpace(package.Version))
        {
            throw new InvalidDataException($"Для пакета {package.Name} не указана версия.");
        }
    }

    private static IReadOnlyList<string> NormalizeDependencies(IReadOnlyList<string>? dependencies)
    {
        return (dependencies ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}

internal sealed class TestRepositoryDocument
{
    [JsonPropertyName("packages")]
    public List<TestPackage>? Packages { get; init; }
}

internal sealed class TestPackage
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("dependencies")]
    public List<string>? Dependencies { get; init; }

    [JsonPropertyName("provides")]
    public List<string>? Provides { get; init; }
}
