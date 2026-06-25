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

        var pendingItems = new List<PendingStatItem>();
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

            pendingItems.Add(new PendingStatItem(rule, planeCount, pendingItems.Count));
        }

        var result = new BomStatResult();
        var variables = BuildFormulaVariables(project);
        var unresolved = pendingItems.ToList();
        while (unresolved.Count > 0)
        {
            var resolvedThisPass = new List<PendingStatItem>();
            foreach (var item in unresolved)
            {
                variables["count"] = item.PlaneCount;
                if (!TryEvaluateFormula(item, variables, out var calculation, out _))
                {
                    continue;
                }

                result.Items.Add(BuildStatItem(item, calculation, ""));
                AddReferenceVariables(variables, item, calculation);
                resolvedThisPass.Add(item);
            }

            if (resolvedThisPass.Count == 0)
            {
                break;
            }

            foreach (var item in resolvedThisPass)
            {
                unresolved.Remove(item);
            }
        }

        foreach (var item in unresolved)
        {
            variables["count"] = item.PlaneCount;
            _ = TryEvaluateFormula(item, variables, out var calculation, out var error);
            result.Items.Add(BuildStatItem(item, calculation, error));
        }

        result.Items = result.Items
            .OrderBy(item => pendingItems.FindIndex(pending => SameStatItem(pending, item)))
            .ToList();
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

    private static bool TryEvaluateFormula(PendingStatItem item, Dictionary<string, decimal> variables, out FormulaCalculation calculation, out string error)
    {
        try
        {
            var rule = item.Rule;
            var formula = ResolveFormula(rule);
            var formulaValue = new FormulaExpressionEvaluator().Evaluate(formula, variables);
            var formulaUsesCount = FormulaUsesCount(formula);
            var hasBlock = !string.IsNullOrWhiteSpace(rule.BlockName);
            var rawTotal = hasBlock && !formulaUsesCount
                ? item.PlaneCount * formulaValue
                : formulaValue;
            calculation = new FormulaCalculation(rawTotal, Math.Ceiling(rawTotal), Math.Ceiling(formulaValue));
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            calculation = new FormulaCalculation(0, 0, 0);
            error = ex.Message;
            return false;
        }
    }

    private static BomStatItem BuildStatItem(PendingStatItem pendingItem, FormulaCalculation calculation, string calculationError)
    {
        var rule = pendingItem.Rule;
        return new BomStatItem
        {
            ComponentName = string.IsNullOrWhiteSpace(rule.ComponentName) ? rule.BlockName : rule.ComponentName,
            SystemName = string.IsNullOrWhiteSpace(rule.SystemName) ? "默认体系" : rule.SystemName,
            GroupName = string.IsNullOrWhiteSpace(rule.GroupName) ? "主体构件" : rule.GroupName,
            BlockName = rule.BlockName,
            Unit = rule.Unit,
            PlaneCount = pendingItem.PlaneCount,
            BaseQtyPerBlock = 1,
            CalculationFactor = calculation.CalculationFactor,
            TotalQty = calculation.RoundedValue,
            Note = rule.Note,
            CalculationError = calculationError
        };
    }

    private static void AddReferenceVariables(Dictionary<string, decimal> variables, PendingStatItem item, FormulaCalculation calculation)
    {
        var rule = item.Rule;
        if (!string.IsNullOrWhiteSpace(rule.ReferenceCode))
        {
            var referenceCode = rule.ReferenceCode.Trim();
            variables.TryAdd(referenceCode, calculation.RawValue);
            variables[$"{referenceCode}_raw"] = calculation.RawValue;
            variables[$"{referenceCode}_count"] = item.PlaneCount;
            variables[$"{referenceCode}_qty"] = calculation.RoundedValue;
        }
    }

    private static bool SameStatItem(PendingStatItem pendingItem, BomStatItem item)
    {
        return string.Equals(pendingItem.Rule.ComponentName, item.ComponentName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(pendingItem.Rule.BlockName, item.BlockName, StringComparison.OrdinalIgnoreCase);
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
        var variables = new Dictionary<string, decimal>
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
                variables[NormalizeParameterKey(parameter.Key)] = parameter.Value;
            }
        }

        return variables;
    }

    private static string NormalizeParameterKey(string key) => key.Trim().ToLowerInvariant();

    private static bool FormulaUsesCount(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return false;
        }

        for (var i = 0; i < formula.Length;)
        {
            if (!char.IsLetter(formula[i]) && formula[i] != '_')
            {
                i++;
                continue;
            }

            var start = i;
            while (i < formula.Length && (char.IsLetterOrDigit(formula[i]) || formula[i] == '_'))
            {
                i++;
            }

            var identifier = formula[start..i];
            if (string.Equals(identifier, "count", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeBlockName(string blockName) => blockName.Trim();

    private sealed record PendingStatItem(ComponentRule Rule, int PlaneCount, int Index);

    private sealed record FormulaCalculation(decimal RawValue, decimal RoundedValue, decimal CalculationFactor);
}
