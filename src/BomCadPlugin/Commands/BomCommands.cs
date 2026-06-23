using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;
using BomCadPlugin.Services;
using BomCadPlugin.UI;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BomCadPlugin.Commands;

public sealed class BomCommands : IExtensionApplication
{
    private readonly ConfigService _configService = new();
    private readonly RuleLibraryService _ruleLibraryService = new();
    private readonly StatisticsService _statisticsService = new();
    private readonly ExportService _exportService = new();
    private readonly CadTableWriter _cadTableWriter = new();

    private static readonly Dictionary<string, BomStatResult> LastResults = new(StringComparer.OrdinalIgnoreCase);

    public void Initialize()
    {
        Ribbon.BomRibbon.Create();
        if (CadDocumentService.TryGetActiveDocument(out var document))
        {
            document.Editor.WriteMessage("\nBOM清单统计 已加载。");
        }
    }

    public void Terminate()
    {
    }

    [CommandMethod("BOM_PARAMS")]
    public void ProjectParams()
    {
        var configPath = CadDocumentService.GetConfigPathOrWarn();
        if (configPath is null)
        {
            return;
        }

        var config = _configService.Load(configPath);
        var library = _ruleLibraryService.LoadOrCreate(config.Rules);
        using var form = new ProjectParamsForm(config.Project, library.ProductSystems);
        if (CadApplication.ShowModalDialog(form) != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        config.Project = form.ProjectParams;
        _configService.Save(configPath, config);
        CadDocumentService.Editor.WriteMessage("\n项目参数已保存。");
    }

    [CommandMethod("BOM_ADD_RULE")]
    public void AddRule()
    {
        var configPath = CadDocumentService.GetConfigPathOrWarn();
        if (configPath is null)
        {
            return;
        }

        var config = _configService.Load(configPath);
        var library = _ruleLibraryService.LoadOrCreate(config.Rules);
        using var form = new AddComponentRuleForm(SelectBlockName, NormalizeSystems(library.ProductSystems.Select(s => s.Name)), BuildPreviewProjectParams(config.Project, library.ProductSystems));
        if (CadApplication.ShowModalDialog(form) != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        UpsertRule(library, form.Rule);
        _ruleLibraryService.Save(library);
        CadDocumentService.Editor.WriteMessage($"\n构件规则已保存到本机规则库：{_ruleLibraryService.LibraryPath}");
    }

    [CommandMethod("BOM_RULES")]
    public void ManageRules()
    {
        var configPath = CadDocumentService.GetConfigPathOrWarn();
        if (configPath is null)
        {
            return;
        }

        var config = _configService.Load(configPath);
        var library = _ruleLibraryService.LoadOrCreate(config.Rules);
        using var form = new ComponentRuleManagerForm(
            library.Rules,
            library.ProductSystems,
            config.Project,
            SelectBlockName);
        if (CadApplication.ShowModalDialog(form) != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        library.Rules = form.Rules;
        library.ProductSystems = form.ProductSystems;
        _ruleLibraryService.Save(library);
        CadDocumentService.Editor.WriteMessage($"\n构件规则库已保存：{_ruleLibraryService.LibraryPath}");
    }

    [CommandMethod("BOM_STAT")]
    public void PlanStatistic()
    {
        var configPath = CadDocumentService.GetConfigPathOrWarn();
        if (configPath is null)
        {
            return;
        }

        var config = _configService.Load(configPath);
        while (true)
        {
            LastResults.TryGetValue(configPath, out var lastResult);
            if (lastResult is null || lastResult.Items.Count == 0)
            {
                var result = BuildStatisticResult(config);
                if (result is null)
                {
                    return;
                }

                LastResults[configPath] = result;
                lastResult = result;
            }

            var action = ShowStatisticResult(lastResult);
            if (action == StatisticResultAction.Recalculate)
            {
                LastResults.Remove(configPath);
                continue;
            }

            if (action == StatisticResultAction.ExportCsv)
            {
                ExportLastResult(lastResult);
            }
            else if (action == StatisticResultAction.WriteCad)
            {
                _cadTableWriter.InsertBomTable(lastResult, config.Project);
            }

            return;
        }
    }

    [CommandMethod("BOM_EXPORT")]
    public void ExportBom()
    {
        var configPath = CadDocumentService.GetConfigPathOrWarn();
        if (configPath is null || !LastResults.TryGetValue(configPath, out var lastResult) || lastResult.Items.Count == 0)
        {
            CadDocumentService.Editor.WriteMessage("\n当前没有可导出的 BOM 统计结果，请先执行平面统计。");
            return;
        }

        ExportLastResult(lastResult);
    }

    [CommandMethod("BOM_CLEAR")]
    public void ClearStatistic()
    {
        var configPath = CadDocumentService.GetConfigPathOrWarn();
        if (configPath is not null)
        {
            LastResults.Remove(configPath);
        }

        CadDocumentService.Editor.WriteMessage("\n已清除当前统计结果。");
    }

    [CommandMethod("BOM_ABOUT")]
    public void About()
    {
        System.Windows.Forms.MessageBox.Show(
            "BOM清单统计 V1\n\n项目参数 + 本机构件规则库 + 平面统计 + CSV导出 + CAD表格留档",
            "关于 BOM清单统计",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Information);
    }

    private BomStatResult? BuildStatisticResult(BomConfig config)
    {
        var library = _ruleLibraryService.LoadOrCreate(config.Rules);
        if (library.Rules.Count == 0)
        {
            CadDocumentService.Editor.WriteMessage("\n构件规则库为空，请先在构件管理中维护规则。");
            return null;
        }

        var blockNames = SelectBlockNamesInArea();
        if (blockNames is null)
        {
            return null;
        }

        var rules = ResolveProjectRules(config.Project, library);
        var result = _statisticsService.BuildResult(blockNames, config.Project, rules);
        if (result.Items.Count == 0)
        {
            CadDocumentService.Editor.WriteMessage("\n当前区域内没有找到已绑定规则的图块，请先检查构件规则库。");
            return null;
        }

        return result;
    }

    private static StatisticResultAction ShowStatisticResult(BomStatResult result)
    {
        using var form = new StatisticResultForm(result);
        CadApplication.ShowModalDialog(form);
        return form.RequestedAction;
    }

    private static void UpsertRule(RuleLibrary library, ComponentRule rule)
    {
        rule.SystemName = string.IsNullOrWhiteSpace(rule.SystemName) ? "默认体系" : rule.SystemName.Trim();
        rule.ComponentName = string.IsNullOrWhiteSpace(rule.ComponentName) ? rule.BlockName : rule.ComponentName.Trim();
        rule.IsModified = true;
        rule.Source = string.IsNullOrWhiteSpace(rule.Source) ? "User" : rule.Source;
        rule.UpdatedAt = DateTime.Now;

        var existing = library.Rules.FirstOrDefault(r => SameRuleIdentity(r, rule));
        if (existing is null)
        {
            library.Rules.Add(rule);
            return;
        }

        var replace = System.Windows.Forms.MessageBox.Show(
            $"图块“{rule.BlockName}”已在体系“{existing.SystemName}”中存在规则，是否覆盖？",
            "确认覆盖",
            System.Windows.Forms.MessageBoxButtons.YesNo,
            System.Windows.Forms.MessageBoxIcon.Question);

        if (replace != System.Windows.Forms.DialogResult.Yes)
        {
            return;
        }

        var index = library.Rules.IndexOf(existing);
        rule.Id = existing.Id;
        library.Rules[index] = rule;
    }

    private static bool SameRuleIdentity(ComponentRule left, ComponentRule right)
    {
        if (!string.IsNullOrWhiteSpace(left.BlockName) && !string.IsNullOrWhiteSpace(right.BlockName))
        {
            return string.Equals(left.BlockName, right.BlockName, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(left.SystemName, right.SystemName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.ComponentName, right.ComponentName, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> NormalizeSystems(IEnumerable<string> systems)
    {
        var normalized = systems
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            normalized.Add("默认体系");
        }

        return normalized;
    }

    private static IReadOnlyList<ComponentRule> ResolveProjectRules(ProjectParams project, RuleLibrary library)
    {
        if (!string.IsNullOrWhiteSpace(project.SelectedSystemName))
        {
            var system = library.ProductSystems.FirstOrDefault(s => string.Equals(s.Name, project.SelectedSystemName, StringComparison.OrdinalIgnoreCase));
            if (system is not null)
            {
                return system.Rules;
            }
        }

        return library.Rules;
    }

    private static ProjectParams BuildPreviewProjectParams(ProjectParams project, IEnumerable<ProductSystem> productSystems)
    {
        var preview = new ProjectParams
        {
            ProjectName = project.ProjectName,
            SelectedSystemName = project.SelectedSystemName,
            FloorHeightM = project.FloorHeightM,
            TemplateHeightsM = [.. project.TemplateHeightsM],
            WallThicknessMm = project.WallThicknessMm,
            Note = project.Note,
            UpdatedAt = project.UpdatedAt,
            CustomParameters = new Dictionary<string, decimal>(project.CustomParameters, StringComparer.OrdinalIgnoreCase)
        };

        foreach (var parameter in productSystems.SelectMany(system => system.Parameters))
        {
            if (!string.IsNullOrWhiteSpace(parameter.Key) && !preview.CustomParameters.ContainsKey(parameter.Key))
            {
                preview.CustomParameters[parameter.Key] = parameter.DefaultValue;
            }
        }

        return preview;
    }

    private static string? SelectBlockName()
    {
        var editor = CadDocumentService.Editor;
        var database = CadDocumentService.Database;
        var options = new PromptEntityOptions("\n请选择一个图块：");
        options.SetRejectMessage("\n请选择图块对象。");
        options.AddAllowedClass(typeof(BlockReference), exactMatch: true);

        var result = editor.GetEntity(options);
        if (result.Status != PromptStatus.OK)
        {
            return null;
        }

        using var transaction = database.TransactionManager.StartTransaction();
        var blockReference = transaction.GetObject(result.ObjectId, OpenMode.ForRead) as BlockReference;
        if (blockReference is null)
        {
            return null;
        }

        var name = CadBlockService.GetEffectiveBlockName(blockReference, transaction);
        transaction.Commit();
        return name;
    }

    private static List<string>? SelectBlockNamesInArea()
    {
        var editor = CadDocumentService.Editor;
        var database = CadDocumentService.Database;
        var filter = new SelectionFilter([new TypedValue((int)DxfCode.Start, "INSERT")]);
        var options = new PromptSelectionOptions
        {
            MessageForAdding = "\n请选择需要统计的平面方案区域："
        };

        var result = editor.GetSelection(options, filter);
        if (result.Status != PromptStatus.OK)
        {
            return null;
        }

        var names = new List<string>();
        using var transaction = database.TransactionManager.StartTransaction();
        foreach (SelectedObject selected in result.Value)
        {
            if (selected is null)
            {
                continue;
            }

            var blockReference = transaction.GetObject(selected.ObjectId, OpenMode.ForRead) as BlockReference;
            if (blockReference is null)
            {
                continue;
            }

            names.Add(CadBlockService.GetEffectiveBlockName(blockReference, transaction));
        }

        transaction.Commit();
        return names;
    }

    private void ExportLastResult(BomStatResult result)
    {
        using var dialog = new System.Windows.Forms.SaveFileDialog
        {
            Title = "导出 BOM CSV",
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"BOM清单_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        _exportService.ExportCsv(dialog.FileName, result);
        CadDocumentService.Editor.WriteMessage($"\nBOM CSV 已导出：{dialog.FileName}");
    }
}
