using DependencyGraphApp.Models;
using DependencyGraphApp.Repositories;
using DependencyGraphApp.Services;
using DependencyGraphApp.Visualization;

namespace DependencyGraphApp;

internal static class Program
{
    private static int _passed;
    
    private static int Main()
    {
        Run("DFS строит транзитивный граф и отмечает цикл", TestDepthFirstSearch);
        Run("Ограничение глубины соблюдается", TestMaximumDepth);
        Run("Фильтр исключает совпадающие имена", TestFilter);
        Run("Поиск обратных зависимостей", TestReverseDependencies);
        Run("Парсер APKINDEX извлекает зависимости и provides", TestAlpineIndexParser);
        Run("D2 и SVG содержат граф", TestRenderers);
        
        Console.WriteLine($"Пройдено проверок: {_passed}/6");

        const int validPassedTestsCount = 6;
        
        return _passed == validPassedTestsCount ? 0 : 1;
    }

    private static void TestDepthFirstSearch()
    {
        var graph = BuildGraph("A", 10, string.Empty);

        const int validNodesCount = 7;
        
        Assert(graph.Nodes.Count == validNodesCount, "Ожидалось 7 узлов.");
        Assert(graph.Edges.Contains(new GraphEdge("D", "A")), "Ожидалось ребро цикла D -> A.");
        Assert(graph.CycleEdges.Contains(new GraphEdge("D", "A")), "Цикл не отмечен.");
    }

    private static void TestMaximumDepth()
    {
        var graph = BuildGraph("A", 1, string.Empty);

        const int validNodesCount = 3;
        
        Assert(graph.Nodes.Count == validNodesCount, "При глубине 1 должны быть A, B и C.");
        Assert(graph.GetDependencies("B").Count == 0, "Узел B не должен раскрываться.");
    }

    private static void TestFilter()
    {
        var graph = BuildGraph("A", 10, "E");
        
        Assert(!graph.Nodes.Contains("E"), "Пакет E должен быть отфильтрован.");
        Assert(!graph.Edges.Any(edge => edge.To == "E"), "Рёбер к E быть не должно.");
    }

    private static void TestReverseDependencies()
    {
        var catalog = new PackageCatalog(TestPackages());
        var finder = new ReverseDependencyFinder(catalog, 10, string.Empty);
        var result = finder.Find("D");
        
        Assert(result.Contains("B"), "Не найдена прямая обратная зависимость B.");
        Assert(result.Contains("A"), "Не найдена транзитивная зависимость A.");
        Assert(result.Contains("H"), "Не найдена транзитивная зависимость H.");
    }

    private static void TestAlpineIndexParser()
    {
        const string index = "P:demo\nV:1.0-r0\nD:foo>=1.2 so:libdemo.so.1\n" +
            "p:cmd:demo=1.0-r0\n\nP:foo\nV:1.2-r0\n";
        var packages = AlpineIndexParser.Parse(index);
        var demo = packages.Single(item => item.Name == "demo");
        
        const int validDependenciesCount = 2;
            
        Assert(demo.Dependencies.Count == validDependenciesCount, "Ожидались две исходные зависимости.");
        Assert(demo.Provides?.Single() == "cmd:demo=1.0-r0", "Не разобран provides.");
        
        var catalog = new PackageCatalog(packages);
        
        Assert(catalog.ResolveDependency("foo>=1.2")?.Name == "foo",
            "Не разрешена обычная зависимость с ограничением версии.");
        Assert(catalog.ResolveDependency("cmd:demo")?.Name == "demo",
            "Не разрешена виртуальная зависимость cmd:.");
    }

    private static void TestRenderers()
    {
        var graph = BuildGraph("A", 10, string.Empty);
        var d2 = D2Renderer.Render(graph);
        var svg = SvgRenderer.Render(graph);
        
        Assert(d2.Contains("direction: right", StringComparison.Ordinal), "Нет заголовка D2.");
        Assert(d2.Contains("->", StringComparison.Ordinal), "В D2 отсутствуют рёбра.");
        Assert(svg.Contains("<svg", StringComparison.Ordinal), "Нет корня SVG.");
        Assert(svg.Contains("marker-end", StringComparison.Ordinal), "Нет стрелок SVG.");
    }

    private static DependencyGraphApp.Models.DependencyGraph BuildGraph(string root, int depth, string filter)
    {
        var catalog = new PackageCatalog(TestPackages());
        
        return new DependencyGraphBuilder(catalog, depth, filter).Build(root, "1.0");
    }

    private static IReadOnlyList<PackageRecord> TestPackages()
    {
        return
        [
            new PackageRecord("A", "1.0", ["B", "C"]),
            new PackageRecord("B", "1.0", ["D", "E"]),
            new PackageRecord("C", "1.0", ["E", "F"]),
            new PackageRecord("D", "1.0", ["A"]),
            new PackageRecord("E", "1.0", ["G"]),
            new PackageRecord("F", "1.0", ["G"]),
            new PackageRecord("G", "1.0", []),
            new PackageRecord("H", "1.0", ["B"])
        ];
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            
            Console.WriteLine($"[OK] {name}");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[FAIL] {name}: {exception.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) 
            throw new InvalidOperationException(message);
    }
}
