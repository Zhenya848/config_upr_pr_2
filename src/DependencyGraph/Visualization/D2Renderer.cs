using System.Text;
using DependencyGraphApp.Models;

namespace DependencyGraphApp.Visualization;

public static class D2Renderer
{
    public static string Render(DependencyGraph graph)
    {
        var builder = new StringBuilder();
        
        builder.AppendLine("direction: right");
        builder.AppendLine();
        
        var ids = BuildNodeIds(graph.Nodes);
        
        foreach (var node in graph.Nodes.Order(StringComparer.Ordinal))
        {
            builder.Append(ids[node]).Append(" : ")
                .AppendLine(Quote(node));
        }

        builder.AppendLine();
        
        foreach (var edge in graph.Edges)
        {
            builder.Append(ids[edge.From]).Append(" -> ")
                .AppendLine(ids[edge.To]);
        }

        return builder.ToString();
    }

    private static Dictionary<string, string> BuildNodeIds(
        IReadOnlyCollection<string> nodes)
    {
        return nodes.Order(StringComparer.Ordinal)
            .Select((name, index) => new { name, id = $"node{index}" })
            .ToDictionary(item => item.name, item => item.id, StringComparer.Ordinal);
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}
