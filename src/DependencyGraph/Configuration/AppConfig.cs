using System.Text.Json;
using System.Text.Json.Serialization;

namespace DependencyGraphApp.Configuration;

public sealed class AppConfig
{
    [JsonPropertyName("packageName")]
    public string? PackageName { get; init; }

    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; init; }

    [JsonPropertyName("useTestRepository")]
    public bool? UseTestRepository { get; init; }

    [JsonPropertyName("testRepositoryPath")]
    public string? TestRepositoryPath { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("outputFileName")]
    public string? OutputFileName { get; init; }

    [JsonPropertyName("maxDepth")]
    public int? MaxDepth { get; init; }

    [JsonPropertyName("filterSubstring")]
    public string? FilterSubstring { get; init; }

    [JsonPropertyName("showReverseDependencies")]
    public bool ShowReverseDependencies { get; init; }
    
    public ValidatedConfig Validate(string configDirectory)
    {
        var errors = new List<string>();
        
        ValidateRequiredValues(errors);
        ValidateRepositoryValues(errors);
        ValidateOutputValues(errors);
        
        if (errors.Count > 0)
        {
            throw new ConfigurationException(errors);
        }

        return CreateValidatedConfig(configDirectory);
    }

    private void ValidateRequiredValues(List<string> errors)
    {
        RequireText(PackageName, "packageName", errors);
        RequireText(Version, "version", errors);
        
        if (UseTestRepository is null)
        {
            errors.Add("useTestRepository: укажите true или false.");
        }

        if (MaxDepth is null || MaxDepth.Value < 0)
        {
            errors.Add("maxDepth: значение должно быть целым числом не меньше 0.");
        }
    }

    private void ValidateRepositoryValues(List<string> errors)
    {
        if (UseTestRepository == true)
        {
            RequireText(TestRepositoryPath, "testRepositoryPath", errors);
            return;
        }

        if (!Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("repositoryUrl: требуется абсолютный HTTP(S) URL репозитория.");
        }
    }

    private void ValidateOutputValues(List<string> errors)
    {
        RequireText(OutputFileName, "outputFileName", errors);
        
        if (!string.IsNullOrWhiteSpace(OutputFileName) &&
            !string.Equals(Path.GetExtension(OutputFileName), ".svg",
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("outputFileName: расширение файла должно быть .svg.");
        }
    }

    private ValidatedConfig CreateValidatedConfig(string configDirectory)
    {
        var testPath = string.IsNullOrWhiteSpace(TestRepositoryPath)
            ? string.Empty
            : ResolvePath(configDirectory, TestRepositoryPath);
        var outputPath = ResolvePath(Environment.CurrentDirectory, OutputFileName!);

        return new ValidatedConfig(
            PackageName!.Trim(),
            RepositoryUrl?.Trim() ?? string.Empty,
            UseTestRepository!.Value,
            testPath,
            Version!.Trim(),
            outputPath,
            MaxDepth!.Value,
            FilterSubstring?.Trim() ?? string.Empty,
            ShowReverseDependencies);
    }

    private static void RequireText(
        string? value,
        string fieldName,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName}: значение обязательно.");
        }
    }

    private static string ResolvePath(string basePath, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(basePath, path));
    }
}

public sealed record ValidatedConfig(
    string PackageName,
    string RepositoryUrl,
    bool UseTestRepository,
    string TestRepositoryPath,
    string Version,
    string OutputFileName,
    int MaxDepth,
    string FilterSubstring,
    bool ShowReverseDependencies)
{
    public string D2FileName => Path.ChangeExtension(OutputFileName, ".d2");
}

public sealed class ConfigurationException : Exception
{
    public ConfigurationException(IReadOnlyCollection<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }
    
    public IReadOnlyCollection<string> Errors { get; }
}

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    
    public static async Task<ValidatedConfig> LoadAsync(string configPath)
    {
        var fullPath = Path.GetFullPath(configPath);
        
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Файл конфигурации не найден.", fullPath);
        }

        var json = await File.ReadAllTextAsync(fullPath);
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
            ?? throw new JsonException("Файл конфигурации пуст или содержит null.");
        
        return config.Validate(Path.GetDirectoryName(fullPath)!);
    }
}
