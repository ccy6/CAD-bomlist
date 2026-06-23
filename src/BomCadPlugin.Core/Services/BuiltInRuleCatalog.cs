using BomCadPlugin.Core.Models;

namespace BomCadPlugin.Core.Services;

public static class BuiltInRuleCatalog
{
    public static List<ComponentRule> CreateDefaultRules()
    {
        return
        [
            CreateFormulaRule("built-in-vertical-back-rib", "BLK_JG01", "竖向背楞", "根", "h/600"),
            CreateFormulaRule("built-in-brace", "BLK_JG02", "斜撑", "根", "3"),
            CreateFormulaRule("built-in-u-clip", "BLK_JG03", "U型卡", "个", "4")
        ];
    }

    private static ComponentRule CreateFormulaRule(string id, string blockName, string componentName, string unit, string formula)
    {
        return new ComponentRule
        {
            Id = id,
            SystemName = "默认体系",
            BlockName = blockName,
            ComponentName = componentName,
            Unit = unit,
            BaseQtyPerBlock = 1,
            CalculationMode = "Formula",
            Formula = formula,
            VerticalRows = 1,
            LibraryVersion = RuleLibraryServiceVersion.Current,
            Source = "BuiltIn",
            UpdatedAt = DateTime.Now,
            IsModified = false
        };
    }

}
