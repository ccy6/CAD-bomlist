using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;
using Xunit;

namespace BomCadPlugin.Core.Tests.Services;

public sealed class StatisticsServiceTests
{
    [Theory]
    [InlineData("h/600", 14)]
    [InlineData("h1/600+h2/600", 14)]
    [InlineData("n*2", 4)]
    public void CalculatePreviewRows_WithFormula_UsesProjectTemplateVariables(string formula, decimal expected)
    {
        var service = new StatisticsService();
        var project = new ProjectParams { TemplateHeightsM = [5m, 3m] };
        var rule = new ComponentRule
        {
            CalculationMode = "Formula",
            Formula = formula
        };

        var rows = service.CalculatePreviewRows(rule, project);

        Assert.Equal(expected, rows);
    }

    [Fact]
    public void CalculatePreviewRows_WithFixedFormula_ReturnsCalculationFactor()
    {
        var service = new StatisticsService();
        var rule = new ComponentRule { Formula = "5" };

        var coefficient = service.CalculatePreviewRows(rule, new ProjectParams());

        Assert.Equal(5, coefficient);
    }

    [Fact]
    public void BuildResult_UsesFormulaAsCalculationFactor()
    {
        var service = new StatisticsService();
        var rule = new ComponentRule
        {
            BlockName = "BLK_A",
            Formula = "5",
            Unit = "个"
        };

        var result = service.BuildResult(["BLK_A", "BLK_A"], new ProjectParams(), [rule]);

        var item = Assert.Single(result.Items);
        Assert.Equal(5, item.CalculationFactor);
        Assert.Equal(10, item.TotalQty);
    }

    [Fact]
    public void BuildResult_WithInvalidFormula_KeepsItemAndReportsCalculationError()
    {
        var service = new StatisticsService();
        var rule = new ComponentRule
        {
            BlockName = "BLK_A",
            ComponentName = "构件A",
            Formula = "h/",
            Unit = "个"
        };

        var result = service.BuildResult(["BLK_A"], new ProjectParams { TemplateHeightsM = [3m] }, [rule]);

        var item = Assert.Single(result.Items);
        Assert.Equal(0, item.CalculationFactor);
        Assert.Equal(0, item.TotalQty);
        Assert.Contains("公式", item.CalculationError);
    }

    [Fact]
    public void BuildResult_IncludesParameterOnlyRuleWithoutBlockName()
    {
        var service = new StatisticsService();
        var project = new ProjectParams
        {
            CustomParameters = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["L"] = 120
            }
        };
        var rule = new ComponentRule
        {
            SystemName = "铝模体系",
            GroupName = "围护构件",
            ComponentName = "围护钢管",
            Unit = "m",
            Formula = "L * 2"
        };

        var result = service.BuildResult([], project, [rule]);

        var item = Assert.Single(result.Items);
        Assert.Equal("围护钢管", item.ComponentName);
        Assert.Equal("围护构件", item.GroupName);
        Assert.Equal(0, item.PlaneCount);
        Assert.Equal(240, item.TotalQty);
    }

    [Fact]
    public void BuildResult_WhenFormulaUsesCount_TreatsFormulaAsTotalQuantity()
    {
        var service = new StatisticsService();
        var rule = new ComponentRule
        {
            BlockName = "BLK_A",
            ComponentName = "连接件",
            Unit = "个",
            Formula = "count * 3"
        };

        var result = service.BuildResult(["BLK_A", "BLK_A"], new ProjectParams(), [rule]);

        var item = Assert.Single(result.Items);
        Assert.Equal(2, item.PlaneCount);
        Assert.Equal(3, item.CalculationFactor);
        Assert.Equal(6, item.TotalQty);
    }

    [Fact]
    public void CalculatePreviewRows_WithInvalidFormula_ReturnsZero()
    {
        var service = new StatisticsService();
        var project = new ProjectParams { TemplateHeightsM = [3m] };
        var rule = new ComponentRule
        {
            CalculationMode = "Formula",
            Formula = "h/"
        };

        var rows = service.CalculatePreviewRows(rule, project);

        Assert.Equal(0, rows);
    }

    [Fact]
    public void CalculatePreviewRows_WithMissingProjectHeights_ReturnsZero()
    {
        var service = new StatisticsService();
        var rule = new ComponentRule
        {
            CalculationMode = "Formula",
            Formula = "h/600"
        };

        var rows = service.CalculatePreviewRows(rule, new ProjectParams());

        Assert.Equal(0, rows);
    }
}
