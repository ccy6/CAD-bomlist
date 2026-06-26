using BomCadPlugin.Core.Models;

namespace BomCadPlugin.Core.Services;

public static class ComponentRuleOrderingService
{
    public static List<ComponentRule> OrderForDisplay(IEnumerable<ComponentRule> rules)
    {
        return rules
            .Select((rule, index) => new { Rule = rule, Index = index })
            .OrderBy(item => string.IsNullOrWhiteSpace(item.Rule.BlockName) ? 1 : 0)
            .ThenBy(item => item.Index)
            .Select(item => item.Rule)
            .ToList();
    }
}
