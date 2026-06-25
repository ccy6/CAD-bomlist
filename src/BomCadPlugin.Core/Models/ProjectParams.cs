using System.Text.Json.Serialization;

namespace BomCadPlugin.Core.Models;

public sealed class ProjectParams
{
    public string ProjectName { get; set; } = "";
    public string SelectedSystemName { get; set; } = "";
    public decimal FloorHeightM { get; set; }
    public List<decimal> TemplateHeightsM { get; set; } = [];
    public decimal WallThicknessMm { get; set; }
    public Dictionary<string, decimal> CustomParameters { get; set; } = [];
    public string Note { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public decimal FloorHeightMm { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public decimal TemplateHeightMm { get; set; }
}
