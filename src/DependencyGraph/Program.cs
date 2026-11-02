using DependencyGraphApp.Configuration;
using DependencyGraphApp.Repositories;
using DependencyGraphApp.Services;

namespace DependencyGraphApp;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var config = await ConfigLoader.LoadAsync(GetConfigPath(args));
            PrintConfiguration(config);

            var packages = await LoadPackagesAsync(config);
            var catalog = new PackageCatalog(packages);
            var graph = BuildGraph(config, catalog);

            PrintGraph(graph);
            return 0;
        }
        catch (ConfigurationException exception)
        {
            Console.Error.WriteLine("Ошибка конфигурации:");
            foreach (var error in exception.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Ошибка: {exception.Message}");
            return 1;
        }
    }

    private static string GetConfigPath(string[] args)
    {
        if (args.Length == 0)
        {
            return Path.Combine("examples", "config-a.json");
        }

        if (args.Length == 2 &&
            (args[0] == "--config" || args[0] == "-c"))
        {
            return args[1];
        }

        throw new ArgumentException(
            "Использование: DependencyGraph [--config <путь-к-json-конфигурации>]");
    }

    private static Task<IReadOnlyList<Models.PackageRecord>> LoadPackagesAsync(
        ValidatedConfig config)
    {
        IPackageRepository repository = config.UseTestRepository
            ? new JsonTestRepository(config.TestRepositoryPath)
            : new AlpineRepository(config.RepositoryUrl);

        return repository.LoadAsync();
    }

    private static Models.DependencyGraph BuildGraph(
        ValidatedConfig config,
        PackageCatalog catalog)
    {
        var builder = new DependencyGraphBuilder(
            catalog, config.MaxDepth, config.FilterSubstring);

        return builder.Build(config.PackageName, config.Version);
    }

    private static void PrintGraph(Models.DependencyGraph graph)
    {
        Console.WriteLine($"Граф зависимостей пакета {graph.RootPackage}:");
        foreach (var node in graph.Nodes)
        {
            var dependencies = graph.GetDependencies(node);
            Console.WriteLine($"  {node} -> " +
                (dependencies.Count == 0 ? "(нет зависимостей)" :
                    string.Join(", ", dependencies)));
        }

        if (graph.CycleEdges.Count > 0)
        {
            Console.WriteLine("Обнаружены циклические рёбра: " +
                string.Join(", ", graph.CycleEdges.Select(edge =>
                    $"{edge.From} -> {edge.To}")));
        }
    }

    private static void PrintConfiguration(ValidatedConfig config)
    {
        Console.WriteLine("Параметры конфигурации:");
        Console.WriteLine($"packageName = {config.PackageName}");
        Console.WriteLine($"repositoryUrl = {DisplayValue(config.RepositoryUrl)}");
        Console.WriteLine($"useTestRepository = {config.UseTestRepository}");
        Console.WriteLine($"testRepositoryPath = {DisplayValue(config.TestRepositoryPath)}");
        Console.WriteLine($"version = {config.Version}");
        Console.WriteLine($"outputFileName = {config.OutputFileName}");
        Console.WriteLine($"maxDepth = {config.MaxDepth}");
        Console.WriteLine($"filterSubstring = {DisplayValue(config.FilterSubstring)}");
        Console.WriteLine($"showReverseDependencies = {config.ShowReverseDependencies}");
        Console.WriteLine();
    }

    private static string DisplayValue(string value)
    {
        return string.IsNullOrEmpty(value) ? "(не задано)" : value;
    }
}
