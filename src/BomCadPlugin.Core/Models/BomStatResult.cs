namespace BomCadPlugin.Core.Models;

public sealed class BomStatResult
{
    public DateTime StatTime { get; set; } = DateTime.Now;
    public List<BomStatItem> Items { get; set; } = [];
}
