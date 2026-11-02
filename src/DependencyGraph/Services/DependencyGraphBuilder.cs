using DependencyGraphApp.Models;

namespace DependencyGraphApp.Services;

public sealed class DependencyGraphBuilder
{
    private readonly PackageCatalog _catalog;
    private readonly int _maxDepth;
    private readonly string _filterSubstring;
    private readonly Dictionary<string, int> _expandedDepth = new(StringComparer.Ordinal);
    private readonly HashSet<string> _active = new(StringComparer.Ordinal);
    private DependencyGraph _graph = null!;
    
    public DependencyGraphBuilder(
        PackageCatalog catalog,
        int maxDepth,
        string filterSubstring)
    {
        _catalog = catalog;
        _maxDepth = maxDepth;
        _filterSubstring = filterSubstring;
    }
    
    public DependencyGraph Build(string packageName, string version)
    {
        _graph = new DependencyGraph(packageName);
        _expandedDepth.Clear();
        _active.Clear();
        
        var root = _catalog.GetRequired(packageName, version);
        
        Visit(root, 0);
        
        return _graph;
    }

    private void Visit(PackageRecord package, int depth)
    {
        _graph.AddNode(package.Name);
        
        if (depth >= _maxDepth || WasExpandedAtShallowerDepth(package.Name, depth))
        {
            return;
        }

        _expandedDepth[package.Name] = depth;
        _active.Add(package.Name);
        
        VisitDependencies(package, depth);
        
        _active.Remove(package.Name);
    }

    private bool WasExpandedAtShallowerDepth(string packageName, int depth)
    {
        return _expandedDepth.TryGetValue(packageName, out var oldDepth) &&
            oldDepth <= depth;
    }

    private void VisitDependencies(PackageRecord package, int depth)
    {
        foreach (var dependencySpec in package.Dependencies.Order(StringComparer.Ordinal))
        {
            var dependency = _catalog.ResolveDependency(dependencySpec);
            var dependencyName = dependency?.Name ??
                _catalog.GetDependencyLabel(dependencySpec);
            
            if (dependencyName.Length == 0 || IsFiltered(dependencyName)) 
                continue;
            
            var isCycle = _active.Contains(dependencyName);
            _graph.AddEdge(package.Name, dependencyName, isCycle);
            
            if (isCycle || dependency is null) 
                continue;
            
            Visit(dependency, depth + 1);
        }
    }

    private bool IsFiltered(string packageName)
    {
        return _filterSubstring.Length > 0 && packageName.Contains(
            _filterSubstring, StringComparison.OrdinalIgnoreCase);
    }
}
