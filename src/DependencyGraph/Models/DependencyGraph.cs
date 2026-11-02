namespace DependencyGraphApp.Models;

public sealed class DependencyGraph
{
    private readonly Dictionary<string, SortedSet<string>> _edges =
        new(StringComparer.Ordinal);
    private readonly HashSet<GraphEdge> _cycleEdges = [];
    
    public DependencyGraph(string rootPackage)
    {
        RootPackage = rootPackage;
        AddNode(rootPackage);
    }
    
    public string RootPackage { get; }
    
    public IReadOnlyCollection<string> Nodes => _edges.Keys.Order().ToArray();
    
    public IReadOnlyCollection<GraphEdge> Edges => _edges
        .SelectMany(pair => pair.Value.Select(target =>
            new GraphEdge(pair.Key, target)))
        .OrderBy(edge => edge.From, StringComparer.Ordinal)
        .ThenBy(edge => edge.To, StringComparer.Ordinal)
        .ToArray();
    
    public IReadOnlyCollection<GraphEdge> CycleEdges => _cycleEdges;
    
    public void AddNode(string name)
    {
        _edges.TryAdd(name, new SortedSet<string>(StringComparer.Ordinal));
    }
    
    public void AddEdge(string from, string to, bool isCycle = false)
    {
        AddNode(from);
        AddNode(to);
        _edges[from].Add(to);
        
        if (isCycle)
        {
            _cycleEdges.Add(new GraphEdge(from, to));
        }
    }
    
    public IReadOnlyCollection<string> GetDependencies(string name)
    {
        return _edges.TryGetValue(name, out var dependencies)
            ? dependencies.ToArray()
            : Array.Empty<string>();
    }
}

public sealed record GraphEdge(string From, string To);
