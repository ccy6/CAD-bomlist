namespace BomCadPlugin.Core.Models;

public sealed class BomStatItem
{
    public string ComponentName { get; set; } = "";
    public string SystemName { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string BlockName { get; set; } = "";
    public string Unit { get; set; } = "";
    public int PlaneCount { get; set; }
    public decimal BaseQtyPerBlock { get; set; }
    public decimal CalculationFactor { get; set; }
    public decimal TotalQty { get; set; }
    public string Note { get; set; } = "";
    public string CalculationError { get; set; } = "";

    public decimal VerticalRows
    {
        get => CalculationFactor;
        set => CalculationFactor = value;
    }
}
