using DependencyGraphApp.Configuration;

namespace DependencyGraphApp;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var configPath = GetConfigPath(args);
            var config = await ConfigLoader.LoadAsync(configPath);

            PrintConfiguration(config);
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
    }

    private static string DisplayValue(string value)
    {
        return string.IsNullOrEmpty(value) ? "(не задано)" : value;
    }
}
