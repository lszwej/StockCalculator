namespace StockCalculator.Web.Logging;

public static class SerilogConfigurationExtensions
{
    private const string SerilogSectionName = "Serilog";
    private const string FilePathKeySuffix = ":path";

    public static IConfigurationManager ResolveRelativeLogPathsAgainst(this IConfigurationManager configuration, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        var resolvedPaths = configuration.GetSection(SerilogSectionName)
            .AsEnumerable()
            .Where(entry => entry.Key.EndsWith(FilePathKeySuffix, StringComparison.OrdinalIgnoreCase))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value) && !Path.IsPathRooted(entry.Value))
            .ToDictionary(entry => entry.Key, entry => (string?)Path.GetFullPath(entry.Value!, baseDirectory));

        configuration.AddInMemoryCollection(resolvedPaths);
        return configuration;
    }
}
