namespace BomCadPlugin.Core.Models;

public sealed class BomConfig
{
    public string Version { get; set; } = "1.0.0";
    public ProjectParams Project { get; set; } = new();
    public List<string> Systems { get; set; } = ["默认体系"];
    public List<ComponentRule> Rules { get; set; } = [];
}
