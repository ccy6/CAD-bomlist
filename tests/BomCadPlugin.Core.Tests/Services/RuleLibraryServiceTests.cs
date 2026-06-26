using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;
using Xunit;

namespace BomCadPlugin.Core.Tests.Services;

public sealed class RuleLibraryServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadOrCreate_WhenLibraryMissing_SeedsBuiltInRules()
    {
        var service = CreateService([
            CreateRule("built-in-1", "BLK_A", "h/600")
        ]);

        var library = service.LoadOrCreate([]);

        Assert.Single(library.Rules);
        Assert.Equal("BLK_A", library.Rules[0].BlockName);
        Assert.False(library.Rules[0].IsModified);
    }

    [Fact]
    public void LoadOrCreate_WhenLibraryMissingAndLegacyRulesExist_ImportsLegacyRules()
    {
        var service = CreateService([]);
        var legacyRules = new[]
        {
            CreateRule("legacy-1", "BLK_LEGACY", "n*2")
        };

        var library = service.LoadOrCreate(legacyRules);

        Assert.Single(library.Rules);
        Assert.Equal("BLK_LEGACY", library.Rules[0].BlockName);
        Assert.True(library.Rules[0].IsModified);
    }

    [Fact]
    public void LoadOrCreate_WhenLibraryMissing_SeedsBuiltInsAndImportsUnmatchedLegacyRules()
    {
        var service = CreateService([
            CreateRule("built-in-1", "BLK_A", "h/600")
        ]);
        var legacyRules = new[]
        {
            CreateRule("legacy-1", "BLK_LEGACY", "n*2")
        };

        var library = service.LoadOrCreate(legacyRules);

        Assert.Equal(2, library.Rules.Count);
        Assert.Contains(library.Rules, rule => rule.BlockName == "BLK_A" && !rule.IsModified);
        Assert.Contains(library.Rules, rule => rule.BlockName == "BLK_LEGACY" && rule.IsModified);
    }

    [Fact]
    public void LoadOrCreate_WhenBuiltInAddsRule_MergesWithoutOverwritingModifiedRule()
    {
        var originalBuiltIn = CreateRule("rule-a", "BLK_A", "h/600");
        var service = CreateService([originalBuiltIn]);
        var library = service.LoadOrCreate([]);
        library.Rules[0].Formula = "n*2";
        library.Rules[0].IsModified = true;
        service.Save(library);

        var upgradedService = CreateService([
            CreateRule("rule-a", "BLK_A", "h/300"),
            CreateRule("rule-b", "BLK_B", "h1/600+h2/600")
        ]);
        var upgradedLibrary = upgradedService.LoadOrCreate([]);

        Assert.Equal(2, upgradedLibrary.Rules.Count);
        Assert.Equal("n*2", upgradedLibrary.Rules.Single(r => r.Id == "rule-a").Formula);
        Assert.Equal("h1/600+h2/600", upgradedLibrary.Rules.Single(r => r.Id == "rule-b").Formula);
    }

    [Fact]
    public void SaveAndLoad_PreservesProductSystemParameters()
    {
        var service = CreateService([]);
        var library = new RuleLibrary
        {
            ProductSystems =
            [
                new ProductSystem
                {
                    Name = "铝模体系",
                    Parameters =
                    [
                        new SystemParameterDefinition
                        {
                            Key = "L",
                            Name = "墙长",
                            Unit = "m",
                            DefaultValue = 0,
                            Description = "围护墙体总长度"
                        }
                    ],
                    Rules =
                    [
                        new ComponentRule
                        {
                            SystemName = "铝模体系",
                            GroupName = "围护构件",
                            ComponentName = "围护钢管",
                            ReferenceCode = "PIPE",
                            Unit = "m",
                            Formula = "L * 2"
                        }
                    ]
                }
            ]
        };

        service.Save(library);

        var loaded = service.LoadOrCreate([]);

        var system = Assert.Single(loaded.ProductSystems);
        Assert.Equal("铝模体系", system.Name);
        var parameter = Assert.Single(system.Parameters.Where(parameter => parameter.Key == "l"));
        Assert.Equal("l", parameter.Key);
        Assert.Equal("墙长", parameter.Name);
        Assert.Equal("m", parameter.Unit);
        Assert.Single(system.Rules);
        Assert.Equal("PIPE", system.Rules[0].ReferenceCode);
        Assert.Equal("l * 2", system.Rules[0].Formula);
    }

    [Fact]
    public void SaveAndLoad_AddsBuiltInParametersToEachProductSystem()
    {
        var service = CreateService([]);
        var library = new RuleLibrary
        {
            ProductSystems =
            [
                new ProductSystem
                {
                    Name = "钢框体系",
                    Parameters =
                    [
                        new SystemParameterDefinition
                        {
                            Key = "l",
                            Name = "墙长",
                            Unit = "m"
                        }
                    ]
                }
            ]
        };

        service.Save(library);

        var loaded = service.LoadOrCreate([]);

        var parameters = loaded.ProductSystems.Single().Parameters;
        Assert.Contains(parameters, parameter => parameter.Key == "l");
        Assert.Contains(parameters, parameter => parameter.Key == "t" && parameter.Name == "墙厚" && parameter.Unit == "mm");
        Assert.Contains(parameters, parameter => parameter.Key == "n" && parameter.Name == "模板块数" && parameter.Unit == "块");
        Assert.DoesNotContain(parameters, parameter => parameter.Key == "wallthickness");
    }

    [Fact]
    public void SaveAndLoad_ConvertsLegacyWallThicknessParameterToT()
    {
        var service = CreateService([]);
        var library = new RuleLibrary
        {
            ProductSystems =
            [
                new ProductSystem
                {
                    Name = "钢框体系",
                    Parameters =
                    [
                        new SystemParameterDefinition
                        {
                            Key = "wallthickness",
                            Name = "墙厚",
                            Unit = "mm"
                        }
                    ]
                }
            ]
        };

        service.Save(library);

        var parameters = service.LoadOrCreate([]).ProductSystems.Single().Parameters;
        Assert.Contains(parameters, parameter => parameter.Key == "t" && parameter.Name == "墙厚");
        Assert.DoesNotContain(parameters, parameter => parameter.Key == "wallthickness");
    }

    [Fact]
    public void SaveAndLoad_AssignsSpreadsheetReferenceCodesWhenMissing()
    {
        var service = CreateService([]);
        var library = new RuleLibrary
        {
            ProductSystems =
            [
                new ProductSystem
                {
                    Name = "铝模体系",
                    Rules =
                    [
                        new ComponentRule { ComponentName = "面板1000", Formula = "count" },
                        new ComponentRule { ComponentName = "面板750", Formula = "count" },
                        new ComponentRule { ComponentName = "连接件", Formula = "A*2+B*2" }
                    ]
                }
            ]
        };

        service.Save(library);

        var loaded = service.LoadOrCreate([]);

        var codes = loaded.ProductSystems.Single().Rules.Select(rule => rule.ReferenceCode).ToList();
        Assert.Equal(["A", "B", "C"], codes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private RuleLibraryService CreateService(IReadOnlyList<ComponentRule> builtInRules)
    {
        return new RuleLibraryService(
            Path.Combine(_tempDirectory, "rules.json"),
            () => builtInRules.Select(Clone).ToList());
    }

    private static ComponentRule CreateRule(string id, string blockName, string formula)
    {
        return new ComponentRule
        {
            Id = id,
            SystemName = "默认体系",
            BlockName = blockName,
            Unit = "个",
            ReferenceCode = "",
            CalculationMode = "Formula",
            Formula = formula
        };
    }

    private static ComponentRule Clone(ComponentRule rule)
    {
        return new ComponentRule
        {
            Id = rule.Id,
            SystemName = rule.SystemName,
            BlockName = rule.BlockName,
            ComponentName = rule.ComponentName,
            ReferenceCode = rule.ReferenceCode,
            GroupName = rule.GroupName,
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
}
