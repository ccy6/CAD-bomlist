using BomCadPlugin.Core.Models;

namespace BomCadPlugin.Core.Services;

public sealed class StatisticsService
{
    public BomStatResult BuildResult(IEnumerable<string> selectedBlockNames, ProjectParams project, IEnumerable<ComponentRule> rules)
    {
        var ruleList = rules.ToList();
        var normalizedRules = ruleList
            .Where(r => !string.IsNullOrWhiteSpace(r.BlockName))
            .GroupBy(r => NormalizeBlockName(r.BlockName))
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var blockCounts = selectedBlockNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizeBlockName)
            .Where(normalizedRules.ContainsKey)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var result = new BomStatResult();
        foreach (var rule in ruleList)
        {
            var hasBlock = !string.IsNullOrWhiteSpace(rule.BlockName);
            var planeCount = 0;
            if (hasBlock)
            {
                var key = NormalizeBlockName(rule.BlockName);
                if (!blockCounts.TryGetValue(key, out planeCount) || planeCount == 0)
                {
                    continue;
                }
            }

            var variables = BuildFormulaVariables(project);
            variables["count"] = planeCount;

            var calculationError = "";
            if (!TryCalculateFactor(rule, variables, out var rawCalculation, out var error))
            {
                calculationError = error;
            }

            var formulaUsesCount = FormulaUsesCount(rule.Formula);
            var calculationFactor = hasBlock && formulaUsesCount && planeCount > 0
                ? rawCalculation / planeCount
                : rawCalculation;
            var totalQty = hasBlock && !formulaUsesCount
                ? planeCount * rawCalculation
                : rawCalculation;

            result.Items.Add(new BomStatItem
            {
                ComponentName = string.IsNullOrWhiteSpace(rule.ComponentName) ? rule.BlockName : rule.ComponentName,
                SystemName = string.IsNullOrWhiteSpace(rule.SystemName) ? "默认体系" : rule.SystemName,
                GroupName = string.IsNullOrWhiteSpace(rule.GroupName) ? "主体构件" : rule.GroupName,
                BlockName = rule.BlockName,
                Unit = rule.Unit,
                PlaneCount = planeCount,
                BaseQtyPerBlock = 1,
                CalculationFactor = calculationFactor,
                TotalQty = totalQty,
                Note = rule.Note,
                CalculationError = calculationError
            });
        }

        return result;
    }

    public decimal CalculatePreviewRows(ComponentRule rule, ProjectParams project)
    {
        return CalculateFactor(rule, project);
    }

    public decimal CalculateFactor(ComponentRule rule, ProjectParams project)
    {
        return TryCalculateFactor(rule, project, out var factor, out _) ? factor : 0;
    }

    public bool TryCalculateFactor(ComponentRule rule, ProjectParams project, out decimal factor, out string error)
    {
        return TryCalculateFactor(rule, BuildFormulaVariables(project), out factor, out error);
    }

    private static bool TryCalculateFactor(ComponentRule rule, Dictionary<string, decimal> variables, out decimal factor, out string error)
    {
        try
        {
            var formula = ResolveFormula(rule);
            var rawValue = new FormulaExpressionEvaluator().Evaluate(formula, variables);
            factor = Math.Ceiling(rawValue);
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            factor = 0;
            error = $"公式计算失败：{ex.Message}";
            return false;
        }
    }

    private static string ResolveFormula(ComponentRule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.Formula))
        {
            return rule.Formula;
        }

        if (string.Equals(rule.CalculationMode, "SpacingByTemplateHeight", StringComparison.OrdinalIgnoreCase))
        {
            return rule.SpacingMm > 0 ? $"h/{rule.SpacingMm:0.####}" : "0";
        }

        return rule.VerticalRows > 0 ? rule.VerticalRows.ToString("0.####") : "0";
    }

    private static List<decimal> ResolveTemplateHeights(ProjectParams project)
    {
        if (project.TemplateHeightsM.Count > 0)
        {
            return project.TemplateHeightsM.Where(h => h > 0).ToList();
        }

        if (project.TemplateHeightMm > 0)
        {
            return [project.TemplateHeightMm / 1000];
        }

        if (project.FloorHeightM > 0)
        {
            return [project.FloorHeightM];
        }

        if (project.FloorHeightMm > 0)
        {
            return [project.FloorHeightMm / 1000];
        }

        return [];
    }

    private static Dictionary<string, decimal> BuildFormulaVariables(ProjectParams project)
    {
        var heightsM = ResolveTemplateHeights(project);
        var variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["n"] = heightsM.Count,
            ["h"] = heightsM.Sum(h => h * 1000),
            ["floorheight"] = project.FloorHeightM > 0 ? project.FloorHeightM * 1000 : project.FloorHeightMm,
            ["wallthickness"] = project.WallThicknessMm
        };

        for (var i = 0; i < heightsM.Count; i++)
        {
            variables[$"h{i + 1}"] = heightsM[i] * 1000;
        }

        foreach (var parameter in project.CustomParameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Key))
            {
                variables[parameter.Key.Trim()] = parameter.Value;
            }
        }

        return variables;
    }

    private static bool FormulaUsesCount(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return false;
        }

        for (var i = 0; i < formula.Length; i++)
        {
            if (!char.IsLetter(formula[i]) && formula[i] != '_')
            {
                continue;
            }

            var start = i;
            while (i < formula.Length && (char.IsLetterOrDigit(formula[i]) || formula[i] == '_'))
            {
                i++;
            }

            var identifier = formula[start..i];
            if (string.Equals(identifier, "count", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeBlockName(string blockName) => blockName.Trim();
}
