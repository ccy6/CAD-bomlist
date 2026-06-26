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
    public void BuildResult_WithBlockRule_UsesFormulaAsPerBlockCoefficient()
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
        Assert.NotEmpty(item.CalculationError);
    }

    [Fact]
    public void BuildResult_IncludesParameterOnlyRuleWithoutBlockName()
    {
        var service = new StatisticsService();
        var project = new ProjectParams
        {
            CustomParameters = new Dictionary<string, decimal>
            {
                ["l"] = 120
            }
        };
        var rule = new ComponentRule
        {
            SystemName = "铝模体系",
            GroupName = "围护构件",
            ComponentName = "围护钢管",
            Unit = "m",
            Formula = "l * 2"
        };

        var result = service.BuildResult([], project, [rule]);

        var item = Assert.Single(result.Items);
        Assert.Equal("围护钢管", item.ComponentName);
        Assert.Equal("围护构件", item.GroupName);
        Assert.Equal(0, item.PlaneCount);
        Assert.Equal(240, item.TotalQty);
    }

    [Fact]
    public void CalculateFactor_WithLowercaseFormula_UsesLowercaseCustomParameter()
    {
        var service = new StatisticsService();
        var project = new ProjectParams
        {
            CustomParameters = new Dictionary<string, decimal>
            {
                ["l"] = 12
            }
        };
        var rule = new ComponentRule { Formula = "l * 2" };

        var factor = service.CalculateFactor(rule, project);

        Assert.Equal(24, factor);
    }

    [Fact]
    public void CalculateFactor_WithBuiltInParameterNames_UsesProjectValuesBeforeCustomParameters()
    {
        var service = new StatisticsService();
        var project = new ProjectParams
        {
            TemplateHeightsM = [3m, 2m],
            WallThicknessMm = 100,
            CustomParameters = new Dictionary<string, decimal>
            {
                ["n"] = 99,
                ["t"] = 888,
                ["wallthickness"] = 999
            }
        };
        var rule = new ComponentRule { Formula = "n + t + wallthickness" };

        var factor = service.CalculateFactor(rule, project);

        Assert.Equal(202, factor);
    }

    [Fact]
    public void BuildResult_WhenReferenceCodeDiffersFromLowercaseParameterByCase_KeepsBothVariables()
    {
        var service = new StatisticsService();
        var project = new ProjectParams
        {
            CustomParameters = new Dictionary<string, decimal>
            {
                ["l"] = 100
            }
        };
        var rules = new[]
        {
            new ComponentRule { BlockName = "BRACE", ComponentName = "Brace", ReferenceCode = "L", Formula = "count*1.5" },
            new ComponentRule { ComponentName = "Pipe", Formula = "l*2" },
            new ComponentRule { ComponentName = "Bare Brace Consumer", Formula = "L*2" },
            new ComponentRule { ComponentName = "Brace Consumer", Formula = "L_raw + L_count + L_qty" }
        };

        var result = service.BuildResult(["BRACE"], project, rules);

        var pipe = result.Items.Single(item => item.ComponentName == "Pipe");
        var bareBraceConsumer = result.Items.Single(item => item.ComponentName == "Bare Brace Consumer");
        var braceConsumer = result.Items.Single(item => item.ComponentName == "Brace Consumer");
        Assert.Equal(200, pipe.TotalQty);
        Assert.Equal(3, bareBraceConsumer.TotalQty);
        Assert.Equal(5, braceConsumer.TotalQty);
    }

    [Fact]
    public void TryCalculateFactor_WithMissingParameter_PreservesInputCasingInError()
    {
        var service = new StatisticsService();
        var rule = new ComponentRule { Formula = "Q1 * 3" };

        var success = service.TryCalculateFactor(rule, new ProjectParams(), out _, out var error);

        Assert.False(success);
        Assert.Contains("Q1", error);
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
        Assert.Equal(6, item.CalculationFactor);
        Assert.Equal(6, item.TotalQty);
    }

    [Fact]
    public void BuildResult_WhenFormulaReferencesComponentCodes_UsesReferencedTotals()
    {
        var service = new StatisticsService();
        var rules = new[]
        {
            new ComponentRule { BlockName = "PANEL_1000", ComponentName = "Panel 1000", ReferenceCode = "P1000", Formula = "count" },
            new ComponentRule { BlockName = "PANEL_750", ComponentName = "Panel 750", ReferenceCode = "P750", Formula = "count" },
            new ComponentRule { BlockName = "PANEL_500", ComponentName = "Panel 500", ReferenceCode = "P500", Formula = "count" },
            new ComponentRule { BlockName = "PANEL_250", ComponentName = "Panel 250", ReferenceCode = "P250", Formula = "count" },
            new ComponentRule { ComponentName = "Panel Connector", Formula = "P1000*2 + P750*2 + P500*2 + P250" }
        };

        var result = service.BuildResult(
            ["PANEL_1000", "PANEL_1000", "PANEL_750", "PANEL_500", "PANEL_250", "PANEL_250"],
            new ProjectParams(),
            rules);

        var connector = result.Items.Single(item => item.ComponentName == "Panel Connector");
        Assert.Equal(10, connector.TotalQty);
        Assert.Equal("", connector.CalculationError);
    }

    [Fact]
    public void BuildResult_WithPanelBlock_CreatesHeightWidthRowsInNote()
    {
        var service = new StatisticsService();
        var rule = new ComponentRule
        {
            BlockName = "PANEL_1000",
            ComponentName = "Panel",
            Unit = "pcs",
            Formula = "count"
        };
        var project = new ProjectParams { TemplateHeightsM = [3m, 2m] };

        var result = service.BuildResult(["PANEL_1000", "PANEL_1000"], project, [rule]);

        Assert.Collection(
            result.Items,
            first =>
            {
                Assert.Equal("Panel", first.ComponentName);
                Assert.Equal("3000x1000", first.Note);
                Assert.Equal(2, first.PlaneCount);
                Assert.Equal(2, first.TotalQty);
            },
            second =>
            {
                Assert.Equal("Panel", second.ComponentName);
                Assert.Equal("2000x1000", second.Note);
                Assert.Equal(2, second.PlaneCount);
                Assert.Equal(2, second.TotalQty);
            });
    }

    [Fact]
    public void BuildResult_WithMeterPanelBlockName_UsesNumberBeforeMeterAsWidth()
    {
        var service = new StatisticsService();
        var rule = new ComponentRule
        {
            BlockName = "LG-SF-65-Panel 1.2m(H)",
            ComponentName = "LG-SF-65-Panel 1.2m(H)",
            Unit = "pcs",
            Formula = "count"
        };
        var project = new ProjectParams { TemplateHeightsM = [3m, 1.2m] };

        var result = service.BuildResult(["LG-SF-65-Panel 1.2m(H)"], project, [rule]);

        Assert.Collection(
            result.Items,
            first => Assert.Equal("3000x1200", first.Note),
            second => Assert.Equal("1200x1200", second.Note));
    }

    [Fact]
    public void BuildResult_WithRepeatedPanelHeight_MergesSamePanelNote()
    {
        var service = new StatisticsService();
        var rule = new ComponentRule
        {
            BlockName = "PANEL_250",
            ComponentName = "Panel",
            Unit = "pcs",
            Formula = "count"
        };
        var project = new ProjectParams { TemplateHeightsM = [1.5m, 1.5m] };

        var result = service.BuildResult(["PANEL_250", "PANEL_250"], project, [rule]);

        var item = Assert.Single(result.Items);
        Assert.Equal("1500x250", item.Note);
        Assert.Equal(2, item.PlaneCount);
        Assert.Equal(4, item.TotalQty);
    }

    [Fact]
    public void BuildResult_WithPanelBlock_KeepsReferenceCountForFormulaRules()
    {
        var service = new StatisticsService();
        var rules = new[]
        {
            new ComponentRule
            {
                BlockName = "PANEL_1000",
                ComponentName = "Panel",
                ReferenceCode = "A",
                Unit = "pcs",
                Formula = "count"
            },
            new ComponentRule
            {
                ComponentName = "Vertical Connector",
                Unit = "pcs",
                Formula = "A_count * 2 * (n - 1)"
            }
        };
        var project = new ProjectParams { TemplateHeightsM = [3m, 2m] };

        var result = service.BuildResult(["PANEL_1000", "PANEL_1000"], project, rules);

        var connector = result.Items.Single(item => item.ComponentName == "Vertical Connector");
        Assert.Equal(4, connector.TotalQty);
        Assert.Equal("", connector.CalculationError);
    }

    [Fact]
    public void BuildResult_WithNonPanelBlock_DoesNotCreatePanelRows()
    {
        var service = new StatisticsService();
        var rule = new ComponentRule
        {
            BlockName = "LG-SF-65-Clamp",
            ComponentName = "Standard Clamp",
            Unit = "pcs",
            Formula = "1"
        };
        var project = new ProjectParams { TemplateHeightsM = [3m, 2m] };

        var result = service.BuildResult(["LG-SF-65-Clamp", "LG-SF-65-Clamp"], project, [rule]);

        var item = Assert.Single(result.Items);
        Assert.Equal("Standard Clamp", item.ComponentName);
        Assert.Equal("", item.Note);
        Assert.Equal(2, item.TotalQty);
    }

    [Fact]
    public void BuildResult_WhenFormulaReferencesComponentCode_UsesUnroundedReferencedCalculation()
    {
        var service = new StatisticsService();
        var rules = new[]
        {
            new ComponentRule { BlockName = "ROD", ComponentName = "Tie Rod", ReferenceCode = "J", Formula = "count*2.5" },
            new ComponentRule { ComponentName = "Pipe", Formula = "J*2" }
        };

        var result = service.BuildResult(["ROD"], new ProjectParams(), rules);

        var pipe = result.Items.Single(item => item.ComponentName == "Pipe");
        Assert.Equal(5, pipe.TotalQty);
        Assert.Equal("", pipe.CalculationError);
    }

    [Fact]
    public void BuildResult_WhenFormulaReferencesComponentCodeCountAndQty_UsesPlaneCountAndRoundedQuantity()
    {
        var service = new StatisticsService();
        var rules = new[]
        {
            new ComponentRule { BlockName = "ROD", ComponentName = "Tie Rod", ReferenceCode = "J", Formula = "count*2.5" },
            new ComponentRule { ComponentName = "Auxiliary", Formula = "J_count + J_qty" }
        };

        var result = service.BuildResult(["ROD"], new ProjectParams(), rules);

        var auxiliary = result.Items.Single(item => item.ComponentName == "Auxiliary");
        Assert.Equal(4, auxiliary.TotalQty);
        Assert.Equal("", auxiliary.CalculationError);
    }

    [Fact]
    public void BuildResult_WhenOldUppercaseCustomParameterIsProvided_UsesLowercaseIdentifierForParameter()
    {
        var service = new StatisticsService();
        var project = new ProjectParams
        {
            CustomParameters = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["L"] = 100
            }
        };
        var rules = new[]
        {
            new ComponentRule { BlockName = "BRACE", ComponentName = "Brace", ReferenceCode = "L", Formula = "count*1.5" },
            new ComponentRule { ComponentName = "Pipe", Formula = "l*2" },
            new ComponentRule { ComponentName = "Bare Brace Consumer", Formula = "L*2" },
            new ComponentRule { ComponentName = "Brace Count Consumer", Formula = "L_raw + L_count + L_qty" }
        };

        var result = service.BuildResult(["BRACE"], project, rules);

        var pipe = result.Items.Single(item => item.ComponentName == "Pipe");
        var bareBraceConsumer = result.Items.Single(item => item.ComponentName == "Bare Brace Consumer");
        var braceCountConsumer = result.Items.Single(item => item.ComponentName == "Brace Count Consumer");
        Assert.Equal(200, pipe.TotalQty);
        Assert.Equal(3, bareBraceConsumer.TotalQty);
        Assert.Equal(5, braceCountConsumer.TotalQty);
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
