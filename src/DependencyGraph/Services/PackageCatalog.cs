using DependencyGraphApp.Models;

namespace DependencyGraphApp.Services;

public sealed class PackageCatalog
{
    private readonly Dictionary<string, List<PackageRecord>> _byName;
    private readonly Dictionary<string, List<PackageRecord>> _providers =
        new(StringComparer.Ordinal);
    
    public PackageCatalog(IEnumerable<PackageRecord> packages)
    {
        _byName = packages.GroupBy(item => item.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(),
                StringComparer.Ordinal);
        
        BuildProviderIndex();
        
        if (_byName.Count == 0)
        {
            throw new InvalidDataException("Репозиторий не содержит пакетов.");
        }
    }
    
    public IReadOnlyCollection<PackageRecord> LatestPackages => _byName.Values
        .Select(SelectLatest)
        .OrderBy(item => item.Name, StringComparer.Ordinal)
        .ToArray();
    
    public PackageRecord GetRequired(string name, string version)
    {
        if (_byName.TryGetValue(name, out var versions))
        {
            var exact = versions.FirstOrDefault(item =>
                string.Equals(item.Version, version, StringComparison.Ordinal));
            
            if (exact is not null) 
                return exact;
        }

        throw new InvalidDataException(
            $"Пакет '{name}' версии '{version}' не найден в репозитории.");
    }
    
    public PackageRecord? ResolveDependency(string dependencySpec)
    {
        var spec = DependencySpec.Parse(dependencySpec);
        
        if (spec.Name.Length == 0) 
            return null;
        
        if (_byName.TryGetValue(spec.Name, out var direct))
        {
            return SelectMatching(direct, spec.Constraint);
        }

        return _providers.TryGetValue(spec.Name, out var providers)
            ? SelectMatching(providers, spec.Constraint)
            : null;
    }

    public string GetDependencyLabel(string dependencySpec)
    {
        return DependencySpec.Parse(dependencySpec).Name;
    }

    private void BuildProviderIndex()
    {
        foreach (var package in LatestPackagesUnsafe())
        {
            foreach (var provided in package.Provides ?? Array.Empty<string>())
            {
                var providerName = DependencySpec.Parse(provided).Name;
                
                if (providerName.Length == 0) 
                    continue;
                
                if (!_providers.TryGetValue(providerName, out var candidates))
                {
                    candidates = [];
                    _providers.Add(providerName, candidates);
                }
                
                candidates.Add(package);
            }
        }
    }

    private IEnumerable<PackageRecord> LatestPackagesUnsafe()
    {
        return _byName.Values.Select(SelectLatest);
    }

    private static PackageRecord? SelectMatching(
        IEnumerable<PackageRecord> candidates,
        VersionConstraint? constraint)
    {
        var matching = candidates.Where(item => constraint is null ||
            constraint.Value.IsSatisfied(item.Version));
        
        return matching.OrderByDescending(item => item.Version,
            AlpineVersionComparer.Instance).FirstOrDefault();
    }

    private static PackageRecord SelectLatest(List<PackageRecord> versions)
    {
        return versions.OrderByDescending(item => item.Version,
            AlpineVersionComparer.Instance).First();
    }
}

internal readonly record struct DependencySpec(string Name, VersionConstraint? Constraint)
{
    public static DependencySpec Parse(string raw)
    {
        var value = raw.Trim();
        
        if (value.StartsWith("!", StringComparison.Ordinal)) 
            return new("", null);
        
        var operatorIndex = value.IndexOfAny(['<', '>', '=', '~']);
        
        if (operatorIndex < 0) 
            return new(value, null);
        
        var name = value[..operatorIndex];
        var operatorLength = operatorIndex + 1 < value.Length &&
            (value[operatorIndex] == '>' && value[operatorIndex + 1] == '=' ||
             value[operatorIndex] == '<' && value[operatorIndex + 1] == '=') ? 2 : 1;
        var operation = value.Substring(operatorIndex, operatorLength);
        var targetVersion = value[(operatorIndex + operatorLength)..];
        
        return new DependencySpec(name, new VersionConstraint(operation, targetVersion));
    }
}

internal readonly record struct VersionConstraint(string Operator, string Version)
{
    public bool IsSatisfied(string actualVersion)
    {
        var comparison = AlpineVersionComparer.Instance.Compare(actualVersion, Version);
        
        return Operator switch
        {
            ">" => comparison > 0,
            ">=" => comparison >= 0,
            "<" => comparison < 0,
            "<=" => comparison <= 0,
            "=" => comparison == 0,
            "~" => comparison >= 0,
            _ => true
        };
    }
}

internal sealed class AlpineVersionComparer : IComparer<string>
{
    public static AlpineVersionComparer Instance { get; } = new();

    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right)) 
            return 0;
        
        if (left is null) 
            return -1;
        
        if (right is null) 
            return 1;
        
        var leftParts = SplitParts(left);
        var rightParts = SplitParts(right);
        
        for (var index = 0; index < Math.Min(leftParts.Count, rightParts.Count); index++)
        {
            var comparison = ComparePart(leftParts[index], rightParts[index]);
            
            if (comparison != 0) 
                return comparison;
        }
        
        return leftParts.Count.CompareTo(rightParts.Count);
    }

    private static List<string> SplitParts(string version)
    {
        var parts = new List<string>();
        var start = 0;
        
        for (var index = 1; index < version.Length; index++)
        {
            if (char.IsDigit(version[index]) == char.IsDigit(version[index - 1]))
                continue;
            
            parts.Add(version[start..index]);
            start = index;
        }
        
        parts.Add(version[start..]);
        return parts;
    }

    private static int ComparePart(string left, string right)
    {
        var leftNumeric = long.TryParse(left, out var leftValue);
        var rightNumeric = long.TryParse(right, out var rightValue);
        
        if (leftNumeric && rightNumeric) 
            return leftValue.CompareTo(rightValue);
        
        if (leftNumeric != rightNumeric) 
            return leftNumeric ? 1 : -1;
        
        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
