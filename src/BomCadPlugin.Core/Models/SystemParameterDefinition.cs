namespace BomCadPlugin.Core.Models;

public sealed class SystemParameterDefinition
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal DefaultValue { get; set; }
    public string Description { get; set; } = "";
}
