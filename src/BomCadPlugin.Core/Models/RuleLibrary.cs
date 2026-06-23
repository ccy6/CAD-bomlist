namespace BomCadPlugin.Core.Models;

public sealed class RuleLibrary
{
    public string Version { get; set; } = RuleLibraryServiceVersion.Current;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<ProductSystem> ProductSystems { get; set; } = [];
    public List<ComponentRule> Rules { get; set; } = [];
}

public static class RuleLibraryServiceVersion
{
    public const string Current = "1.0.0";
}
