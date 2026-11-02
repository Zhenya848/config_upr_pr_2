using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using DependencyGraphApp.Models;

namespace DependencyGraphApp.Repositories;

public sealed class AlpineRepository : IPackageRepository
{
    private const string IndexFileName = "APKINDEX.tar.gz";
    private readonly string _repositoryUrl;
    private readonly HttpClient _httpClient;
    
    public AlpineRepository(string repositoryUrl)
    {
        _repositoryUrl = repositoryUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }
    
    public async Task<IReadOnlyList<PackageRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var indexUrl = BuildIndexUrl(_repositoryUrl);
        using var response = await _httpClient.GetAsync(
            indexUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        
        return await ReadIndexAsync(source, cancellationToken);
    }

    private static string BuildIndexUrl(string repositoryUrl)
    {
        var trimmed = repositoryUrl.TrimEnd('/');
        return trimmed.EndsWith(IndexFileName, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/{IndexFileName}";
    }

    private static async Task<IReadOnlyList<PackageRecord>> ReadIndexAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        await using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        
        TarEntry? entry;
        
        while ((entry = tar.GetNextEntry(copyData: true)) is not null)
        {
            if (entry.Name.EndsWith("APKINDEX", StringComparison.OrdinalIgnoreCase) && entry.DataStream is not null)
            {
                using var reader = new StreamReader(
                    entry.DataStream, Encoding.UTF8, true, 4096, leaveOpen: true);
                var content = await reader.ReadToEndAsync(cancellationToken);
                
                return AlpineIndexParser.Parse(content);
            }
        }

        throw new InvalidDataException("В APKINDEX.tar.gz не найден файл APKINDEX.");
    }
}

public static class AlpineIndexParser
{
    public static IReadOnlyList<PackageRecord> Parse(string content)
    {
        var packages = new List<PackageRecord>();
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        
        foreach (var line in content.Split('\n'))
        {
            ParseLine(line.TrimEnd('\r'), fields, packages);
        }

        AddPackage(fields, packages);
        
        if (packages.Count == 0)
        {
            throw new InvalidDataException("Файл APKINDEX не содержит пакетов.");
        }

        return packages;
    }

    private static void ParseLine(
        string line,
        Dictionary<string, string> fields,
        List<PackageRecord> packages)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            AddPackage(fields, packages);
            return;
        }

        var separator = line.IndexOf(':');
        
        if (separator > 0)
        {
            fields[line[..separator]] = line[(separator + 1)..].Trim();
        }
    }

    private static void AddPackage(
        Dictionary<string, string> fields,
        List<PackageRecord> packages)
    {
        if (fields.TryGetValue("P", out var name) &&
            fields.TryGetValue("V", out var version))
        {
            var dependencies = fields.TryGetValue("D", out var rawDependencies)
                ? ParseDependencies(rawDependencies)
                : Array.Empty<string>();
            var provides = fields.TryGetValue("p", out var rawProvides)
                ? ParseDependencies(rawProvides)
                : Array.Empty<string>();
            
            packages.Add(new PackageRecord(name, version, dependencies, provides));
        }

        fields.Clear();
    }

    private static IReadOnlyList<string> ParseDependencies(string rawDependencies)
    {
        return rawDependencies.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !token.StartsWith("!", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }


}
