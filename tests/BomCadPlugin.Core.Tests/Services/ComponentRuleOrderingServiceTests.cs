using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;
using Xunit;

namespace BomCadPlugin.Core.Tests.Services;

public sealed class ComponentRuleOrderingServiceTests
{
    [Fact]
    public void OrderForDisplay_MovesFormulaOnlyRulesAfterBlockRules()
    {
        var formulaOnlyRule = new ComponentRule { ComponentName = "Tie rod 大垫片", Formula = "J_count*2" };
        var blockRule = new ComponentRule { ComponentName = "LG-SF-65-Panel 1.2m(H)", BlockName = "LG-SF-65-Panel 1.2m(H)" };
        var secondBlockRule = new ComponentRule { ComponentName = "LG-SF-65-Panel 1m(H)", BlockName = "LG-SF-65-Panel 1m(H)" };

        var ordered = ComponentRuleOrderingService.OrderForDisplay([formulaOnlyRule, blockRule, secondBlockRule]);

        Assert.Equal([blockRule, secondBlockRule, formulaOnlyRule], ordered);
    }

    [Fact]
    public void OrderForDisplay_PreservesOriginalOrderWithinEachRuleKind()
    {
        var firstFormulaOnlyRule = new ComponentRule { ComponentName = "连接件 A", Formula = "A_count" };
        var firstBlockRule = new ComponentRule { ComponentName = "面板 1000", BlockName = "PANEL_1000" };
        var secondFormulaOnlyRule = new ComponentRule { ComponentName = "连接件 B", Formula = "B_count" };
        var secondBlockRule = new ComponentRule { ComponentName = "面板 250", BlockName = "PANEL_250" };

        var ordered = ComponentRuleOrderingService.OrderForDisplay([firstFormulaOnlyRule, firstBlockRule, secondFormulaOnlyRule, secondBlockRule]);

        Assert.Equal([firstBlockRule, secondBlockRule, firstFormulaOnlyRule, secondFormulaOnlyRule], ordered);
    }
}
