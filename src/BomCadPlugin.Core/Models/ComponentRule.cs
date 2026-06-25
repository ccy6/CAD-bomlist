using System.Text.Json.Serialization;

namespace BomCadPlugin.Core.Models;

public sealed class ComponentRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SystemName { get; set; } = "默认体系";
    public string GroupName { get; set; } = "主体构件";
    public string BlockName { get; set; } = "";
    public string ComponentName { get; set; } = "";
    public string ReferenceCode { get; set; } = "";
    public string Unit { get; set; } = "个";
    public decimal BaseQtyPerBlock { get; set; } = 1;
    public string Formula { get; set; } = "1";
    public string Note { get; set; } = "";
    public string LibraryVersion { get; set; } = "";
    public string Source { get; set; } = "BuiltIn";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsModified { get; set; }

    // Legacy fields retained so old .bomconfig.json / rule exports can still load.
    public string CalculationMode { get; set; } = "Formula";
    public decimal SpacingMm { get; set; } = 600;
    public decimal VerticalRows { get; set; } = 1;

    [JsonIgnore]
    public string CalculationModeDisplay => "公式计算";
}
