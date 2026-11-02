using System.Net;
using System.Text;
using DependencyGraphApp.Models;

namespace DependencyGraphApp.Visualization;

public static class SvgRenderer
{
    private const int NodeWidth = 150;
    private const int NodeHeight = 58;
    private const int ColumnGap = 130;
    private const int RowGap = 32;
    private const int Margin = 42;
    
    public static string Render(DependencyGraph graph)
    {
        var positions = CalculatePositions(graph);
        var dimensions = CalculateDimensions(positions);
        var builder = new StringBuilder();
        
        WriteHeader(builder, dimensions.Width, dimensions.Height, graph.RootPackage);
        WriteEdges(builder, graph, positions);
        WriteNodes(builder, graph, positions);
        
        builder.AppendLine("</svg>");
        
        return builder.ToString();
    }

    private static Dictionary<string, NodePosition> CalculatePositions(
        DependencyGraph graph)
    {
        var levels = CalculateLevels(graph);
        var positions = new Dictionary<string, NodePosition>(StringComparer.Ordinal);
        
        foreach (var group in levels.GroupBy(pair => pair.Value).OrderBy(group => group.Key))
        {
            var ordered = group.Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray();
            
            for (var index = 0; index < ordered.Length; index++)
            {
                var x = Margin + group.Key * (NodeWidth + ColumnGap);
                var y = Margin + index * (NodeHeight + RowGap);
                
                positions[ordered[index]] = new NodePosition(x, y);
            }
        }

        return positions;
    }

    private static Dictionary<string, int> CalculateLevels(DependencyGraph graph)
    {
        var levels = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [graph.RootPackage] = 0
        };
        
        var queue = new Queue<string>();
        queue.Enqueue(graph.RootPackage);
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            
            foreach (var dependency in graph.GetDependencies(current))
            {
                if (levels.ContainsKey(dependency)) 
                    continue;
                
                levels[dependency] = levels[current] + 1;
                queue.Enqueue(dependency);
            }
        }

        return levels;
    }

    private static SvgDimensions CalculateDimensions(IReadOnlyDictionary<string, NodePosition> positions)
    {
        var maxX = positions.Values.Max(position => position.X);
        var maxY = positions.Values.Max(position => position.Y);
        
        return new SvgDimensions(maxX + NodeWidth + Margin, maxY + NodeHeight + Margin);
    }

    private static void WriteHeader(StringBuilder builder, int width, int height, string rootPackage)
    {
        builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" " +
            $"width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        builder.AppendLine("<defs><marker id=\"arrow\" markerWidth=\"10\" " +
            "markerHeight=\"10\" refX=\"8\" refY=\"3\" orient=\"auto\">");
        builder.AppendLine("<path d=\"M0,0 L0,6 L9,3 z\" fill=\"#475569\"/></marker></defs>");
        builder.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#f8fafc\"/>");
        builder.AppendLine($"<text x=\"{Margin}\" y=\"24\" " +
            "font-family=\"Arial, sans-serif\" font-size=\"16\" " +
            $"font-weight=\"bold\">Dependency graph: {Escape(rootPackage)}</text>");
    }

    private static void WriteEdges(
        StringBuilder builder,
        DependencyGraph graph,
        IReadOnlyDictionary<string, NodePosition> positions)
    {
        foreach (var edge in graph.Edges)
        {
            var from = positions[edge.From];
            var to = positions[edge.To];
            var startX = from.X + NodeWidth;
            var startY = from.Y + NodeHeight / 2;
            var endX = to.X;
            var endY = to.Y + NodeHeight / 2;
            var curve = Math.Max(32, Math.Abs(endX - startX) / 2);
            var path = $"M {startX} {startY} C {startX + curve} {startY}, " +
                $"{endX - curve} {endY}, {endX} {endY}";
            builder.AppendLine($"<path d=\"{path}\" fill=\"none\" " +
                "stroke=\"#64748b\" stroke-width=\"1.8\" " +
                "marker-end=\"url(#arrow)\"/>");
        }
    }

    private static void WriteNodes(
        StringBuilder builder,
        DependencyGraph graph,
        IReadOnlyDictionary<string, NodePosition> positions)
    {
        foreach (var node in graph.Nodes.Order(StringComparer.Ordinal))
        {
            var position = positions[node];
            var isRoot = string.Equals(node, graph.RootPackage, StringComparison.Ordinal);
            var fill = isRoot ? "#dbeafe" : "#ffffff";
            var stroke = isRoot ? "#2563eb" : "#94a3b8";
            builder.AppendLine($"<rect x=\"{position.X}\" y=\"{position.Y}\" " +
                $"width=\"{NodeWidth}\" height=\"{NodeHeight}\" rx=\"8\" " +
                $"fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"1.5\"/>");
            builder.AppendLine($"<text x=\"{position.X + NodeWidth / 2}\" " +
                $"y=\"{position.Y + NodeHeight / 2 + 5}\" text-anchor=\"middle\" " +
                "font-family=\"Arial, sans-serif\" font-size=\"15\" " +
                $"fill=\"#0f172a\">{Escape(node)}</text>");
        }
    }

    private static string Escape(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private sealed record NodePosition(int X, int Y);
    private sealed record SvgDimensions(int Width, int Height);
}
