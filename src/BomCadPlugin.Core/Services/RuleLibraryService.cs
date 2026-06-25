using System.Text.Encodings.Web;
using System.Text.Json;
using BomCadPlugin.Core.Models;

namespace BomCadPlugin.Core.Services;

public sealed class RuleLibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _libraryPath;
    private readonly Func<IReadOnlyList<ComponentRule>> _builtInRulesFactory;

    public RuleLibraryService()
        : this(GetDefaultLibraryPath(), BuiltInRuleCatalog.CreateDefaultRules)
    {
    }

    public RuleLibraryService(string libraryPath, Func<IReadOnlyList<ComponentRule>> builtInRulesFactory)
    {
        _libraryPath = libraryPath;
        _builtInRulesFactory = builtInRulesFactory;
    }

    public string LibraryPath => _libraryPath;

    public RuleLibrary LoadOrCreate(IEnumerable<ComponentRule> legacyRules)
    {
        var library = File.Exists(_libraryPath)
            ? LoadExisting()
            : CreateInitialLibrary(legacyRules);

        MergeBuiltInRules(library);
        EnsureProductSystems(library);
        Save(library);
        return library;
    }

    public void Save(RuleLibrary library)
    {
        var directory = Path.GetDirectoryName(_libraryPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        library.Version = RuleLibraryServiceVersion.Current;
        library.UpdatedAt = DateTime.Now;
        EnsureProductSystems(library);

        var json = JsonSerializer.Serialize(library, JsonOptions);
        File.WriteAllText(_libraryPath, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private RuleLibrary LoadExisting()
    {
        var json = File.ReadAllText(_libraryPath);
        return JsonSerializer.Deserialize<RuleLibrary>(json, JsonOptions) ?? new RuleLibrary();
    }

    private RuleLibrary CreateInitialLibrary(IEnumerable<ComponentRule> legacyRules)
    {
        var library = new RuleLibrary
        {
            Rules = _builtInRulesFactory().Select(CloneBuiltInRule).ToList()
        };

        foreach (var legacyRule in legacyRules.Where(rule => !string.IsNullOrWhiteSpace(rule.BlockName)))
        {
            if (FindMatchingRule(library.Rules, legacyRule) is not null)
            {
                continue;
            }

            library.Rules.Add(CloneLegacyRule(legacyRule));
        }

        return library;
    }

    private void MergeBuiltInRules(RuleLibrary library)
    {
        foreach (var builtInRule in _builtInRulesFactory())
        {
            var existing = FindMatchingRule(library.Rules, builtInRule);
            if (existing is null)
            {
                library.Rules.Add(CloneBuiltInRule(builtInRule));
                continue;
            }

            if (existing.IsModified)
            {
                continue;
            }

            var id = existing.Id;
            CopyRuleValues(builtInRule, existing);
            existing.Id = string.IsNullOrWhiteSpace(id) ? builtInRule.Id : id;
            existing.Source = "BuiltIn";
            existing.LibraryVersion = RuleLibraryServiceVersion.Current;
            existing.IsModified = false;
        }
    }

    private static void EnsureProductSystems(RuleLibrary library)
    {
        if (library.ProductSystems.Count == 0)
        {
            library.ProductSystems = library.Rules
                .GroupBy(rule => NormalizeSystemName(rule.SystemName), StringComparer.OrdinalIgnoreCase)
                .Select(group => new ProductSystem
                {
                    Name = group.Key,
                    Rules = group.Select(Clone).ToList()
                })
                .ToList();
        }

        foreach (var system in library.ProductSystems)
        {
            NormalizeSystem(system);
            EnsureReferenceCodes(system.Rules);
        }

        foreach (var rule in library.Rules)
        {
            NormalizeRule(rule);
            var system = library.ProductSystems.FirstOrDefault(s => string.Equals(s.Name, rule.SystemName, StringComparison.OrdinalIgnoreCase));
            if (system is null)
            {
                system = new ProductSystem { Name = rule.SystemName };
                library.ProductSystems.Add(system);
            }

            var existingRule = FindMatchingRule(system.Rules, rule);
            if (existingRule is null)
            {
                system.Rules.Add(Clone(rule));
            }
            else if (rule.IsModified)
            {
                var index = system.Rules.IndexOf(existingRule);
                system.Rules[index] = Clone(rule);
            }
        }

        foreach (var system in library.ProductSystems)
        {
            EnsureReferenceCodes(system.Rules);
        }

        library.Rules = library.ProductSystems
            .SelectMany(system =>
            {
                foreach (var rule in system.Rules)
                {
                    rule.SystemName = system.Name;
                    NormalizeRule(rule);
                }

                return system.Rules;
            })
            .Select(Clone)
            .ToList();
    }

    private static void NormalizeSystem(ProductSystem system)
    {
        system.Id = string.IsNullOrWhiteSpace(system.Id) ? Guid.NewGuid().ToString("N") : system.Id;
        system.Name = NormalizeSystemName(system.Name);
        var parameterKeyMap = system.Parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Key))
            .Select(parameter => new
            {
                Original = parameter.Key.Trim(),
                Normalized = NormalizeParameterKey(parameter.Key)
            })
            .Where(parameter => !string.Equals(parameter.Original, parameter.Normalized, StringComparison.Ordinal))
            .GroupBy(parameter => parameter.Original, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Normalized, StringComparer.Ordinal);

        system.Parameters = system.Parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Key))
            .Select(NormalizeParameter)
            .GroupBy(parameter => parameter.Key, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToList();

        foreach (var rule in system.Rules)
        {
            rule.SystemName = system.Name;
            NormalizeRule(rule);
            rule.Formula = RewriteFormulaIdentifiers(rule.Formula, parameterKeyMap);
        }
    }

    private static SystemParameterDefinition NormalizeParameter(SystemParameterDefinition parameter)
    {
        parameter.Key = NormalizeParameterKey(parameter.Key);
        parameter.Name = string.IsNullOrWhiteSpace(parameter.Name) ? parameter.Key : parameter.Name.Trim();
        parameter.Unit = parameter.Unit?.Trim() ?? "";
        parameter.Description = parameter.Description?.Trim() ?? "";
        return parameter;
    }

    private static string NormalizeParameterKey(string key) => key.Trim().ToLowerInvariant();

    private static string RewriteFormulaIdentifiers(string formula, IReadOnlyDictionary<string, string> replacements)
    {
        if (string.IsNullOrWhiteSpace(formula) || replacements.Count == 0)
        {
            return formula;
        }

        var result = new System.Text.StringBuilder(formula.Length);
        for (var i = 0; i < formula.Length;)
        {
            if (!char.IsLetter(formula[i]) && formula[i] != '_')
            {
                result.Append(formula[i]);
                i++;
                continue;
            }

            var start = i;
            while (i < formula.Length && (char.IsLetterOrDigit(formula[i]) || formula[i] == '_'))
            {
                i++;
            }

            var identifier = formula[start..i];
            result.Append(replacements.TryGetValue(identifier, out var replacement) ? replacement : identifier);
        }

        return result.ToString();
    }

    private static ComponentRule? FindMatchingRule(IEnumerable<ComponentRule> rules, ComponentRule target)
    {
        return rules.FirstOrDefault(rule =>
            (!string.IsNullOrWhiteSpace(rule.Id) && string.Equals(rule.Id, target.Id, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(rule.BlockName) &&
             !string.IsNullOrWhiteSpace(target.BlockName) &&
             string.Equals(rule.BlockName, target.BlockName, StringComparison.OrdinalIgnoreCase)) ||
            (string.IsNullOrWhiteSpace(rule.BlockName) &&
             string.IsNullOrWhiteSpace(target.BlockName) &&
             string.Equals(rule.SystemName, target.SystemName, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(rule.ComponentName, target.ComponentName, StringComparison.OrdinalIgnoreCase)));
    }

    private static ComponentRule CloneBuiltInRule(ComponentRule rule)
    {
        var clone = Clone(rule);
        clone.Source = "BuiltIn";
        clone.LibraryVersion = RuleLibraryServiceVersion.Current;
        clone.UpdatedAt = DateTime.Now;
        clone.IsModified = false;
        NormalizeRule(clone);
        return clone;
    }

    private static ComponentRule CloneLegacyRule(ComponentRule rule)
    {
        var clone = Clone(rule);
        clone.Source = "LegacyProject";
        clone.LibraryVersion = RuleLibraryServiceVersion.Current;
        clone.UpdatedAt = DateTime.Now;
        clone.IsModified = true;
        NormalizeRule(clone);
        return clone;
    }

    private static ComponentRule Clone(ComponentRule rule)
    {
        return new ComponentRule
        {
            Id = string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("N") : rule.Id,
            SystemName = rule.SystemName,
            GroupName = rule.GroupName,
            BlockName = rule.BlockName,
            ComponentName = rule.ComponentName,
            ReferenceCode = rule.ReferenceCode,
            Unit = rule.Unit,
            BaseQtyPerBlock = rule.BaseQtyPerBlock,
            CalculationMode = rule.CalculationMode,
            SpacingMm = rule.SpacingMm,
            Formula = rule.Formula,
            VerticalRows = rule.VerticalRows,
            Note = rule.Note,
            LibraryVersion = rule.LibraryVersion,
            Source = rule.Source,
            UpdatedAt = rule.UpdatedAt,
            IsModified = rule.IsModified
        };
    }

    private static void CopyRuleValues(ComponentRule source, ComponentRule target)
    {
        target.SystemName = source.SystemName;
        target.GroupName = source.GroupName;
        target.BlockName = source.BlockName;
        target.ComponentName = source.ComponentName;
        target.ReferenceCode = source.ReferenceCode;
        target.Unit = source.Unit;
        target.BaseQtyPerBlock = source.BaseQtyPerBlock;
        target.CalculationMode = source.CalculationMode;
        target.SpacingMm = source.SpacingMm;
        target.Formula = source.Formula;
        target.VerticalRows = source.VerticalRows;
        target.Note = source.Note;
        target.UpdatedAt = DateTime.Now;
    }

    private static void NormalizeRule(ComponentRule rule)
    {
        rule.SystemName = NormalizeSystemName(rule.SystemName);
        rule.GroupName = string.IsNullOrWhiteSpace(rule.GroupName) ? "主体构件" : rule.GroupName.Trim();
        rule.BlockName = rule.BlockName?.Trim() ?? "";
        rule.ComponentName = string.IsNullOrWhiteSpace(rule.ComponentName)
            ? (string.IsNullOrWhiteSpace(rule.BlockName) ? "未命名构件" : rule.BlockName)
            : rule.ComponentName.Trim();
        rule.ReferenceCode = rule.ReferenceCode?.Trim() ?? "";
        rule.Unit = string.IsNullOrWhiteSpace(rule.Unit) ? "个" : rule.Unit.Trim();
        rule.Formula = ResolveFormula(rule).Trim();
        rule.CalculationMode = "Formula";
        rule.BaseQtyPerBlock = rule.BaseQtyPerBlock <= 0 ? 1 : rule.BaseQtyPerBlock;
        rule.SpacingMm = rule.SpacingMm <= 0 ? 600 : rule.SpacingMm;
        rule.LibraryVersion = RuleLibraryServiceVersion.Current;
    }

    private static void EnsureReferenceCodes(List<ComponentRule> rules)
    {
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.ReferenceCode) && usedCodes.Add(rule.ReferenceCode.Trim()))
            {
                rule.ReferenceCode = rule.ReferenceCode.Trim();
                continue;
            }

            rule.ReferenceCode = NextReferenceCode(usedCodes);
            usedCodes.Add(rule.ReferenceCode);
        }
    }

    private static string NextReferenceCode(IReadOnlySet<string> usedCodes)
    {
        for (var index = 0; ; index++)
        {
            var code = ToSpreadsheetColumnName(index);
            if (!usedCodes.Contains(code))
            {
                return code;
            }
        }
    }

    private static string ToSpreadsheetColumnName(int zeroBasedIndex)
    {
        var value = zeroBasedIndex + 1;
        var chars = new Stack<char>();
        while (value > 0)
        {
            value--;
            chars.Push((char)('A' + value % 26));
            value /= 26;
        }

        return new string(chars.ToArray());
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

        return rule.VerticalRows > 0 ? rule.VerticalRows.ToString("0.####") : "1";
    }

    private static string NormalizeSystemName(string systemName)
    {
        return string.IsNullOrWhiteSpace(systemName) ? "默认体系" : systemName.Trim();
    }

    private static string GetDefaultLibraryPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "BomCadPlugin", "component-rules.json");
    }
}
