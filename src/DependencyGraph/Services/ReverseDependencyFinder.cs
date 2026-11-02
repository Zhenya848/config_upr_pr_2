using DependencyGraphApp.Models;

namespace DependencyGraphApp.Services;

public sealed class ReverseDependencyFinder
{
    private readonly Dictionary<string, SortedSet<string>> _reverseEdges =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _expandedDepth = new(StringComparer.Ordinal);
    private readonly List<string> _result = [];
    private readonly int _maxDepth;
    private readonly string _filterSubstring;
    
    public ReverseDependencyFinder(
        PackageCatalog catalog,
        int maxDepth,
        string filterSubstring)
    {
        _maxDepth = maxDepth;
        _filterSubstring = filterSubstring;
        
        BuildReverseIndex(catalog);
    }
    
    public IReadOnlyList<string> Find(string packageName)
    {
        _visited.Clear();
        _expandedDepth.Clear();
        _result.Clear();
        _visited.Add(packageName);
        
        Visit(packageName, 0);
        
        return _result.ToArray();
    }

    private void BuildReverseIndex(PackageCatalog catalog)
    {
        foreach (var package in catalog.LatestPackages)
        {
            foreach (var dependencySpec in package.Dependencies)
            {
                var dependency = catalog.ResolveDependency(dependencySpec);
                var dependencyName = dependency?.Name ??
                    catalog.GetDependencyLabel(dependencySpec);
                
                if (dependencyName.Length == 0 || IsFiltered(package.Name) || IsFiltered(dependencyName))
                    continue;
                
                AddReverseEdge(dependencyName, package.Name);
            }
        }
    }

    private void AddReverseEdge(string dependency, string dependentPackage)
    {
        if (!_reverseEdges.TryGetValue(dependency, out var dependents))
        {
            dependents = new SortedSet<string>(StringComparer.Ordinal);
            _reverseEdges.Add(dependency, dependents);
        }

        dependents.Add(dependentPackage);
    }

    private void Visit(string packageName, int depth)
    {
        if (depth >= _maxDepth || WasExpandedAtShallowerDepth(packageName, depth))
        {
            return;
        }

        _expandedDepth[packageName] = depth;
        
        if (!_reverseEdges.TryGetValue(packageName, out var dependents)) 
            return;
        
        VisitDependents(dependents, depth);
    }

    private void VisitDependents(IEnumerable<string> dependents, int depth)
    {
        foreach (var dependent in dependents)
        {
            var nextDepth = depth + 1;
            
            if (_visited.Add(dependent)) 
                _result.Add(dependent);
            
            Visit(dependent, nextDepth);
        }
    }

    private bool WasExpandedAtShallowerDepth(string packageName, int depth)
    {
        return _expandedDepth.TryGetValue(packageName, out var oldDepth) &&
            oldDepth <= depth;
    }

    private bool IsFiltered(string packageName)
    {
        return _filterSubstring.Length > 0 && packageName.Contains(
            _filterSubstring, StringComparison.OrdinalIgnoreCase);
    }
}
