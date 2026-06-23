namespace BomCadPlugin.Core.Models;

public sealed class BomTableData
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public IReadOnlyList<string> Headers { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; set; } = [];
}
