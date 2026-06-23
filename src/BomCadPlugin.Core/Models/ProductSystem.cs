namespace BomCadPlugin.Core.Models;

public sealed class ProductSystem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public List<SystemParameterDefinition> Parameters { get; set; } = [];
    public List<ComponentRule> Rules { get; set; } = [];
}
