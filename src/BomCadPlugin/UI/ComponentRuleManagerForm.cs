using System.IO;
using System.Text.Json;
using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;

namespace BomCadPlugin.UI;

internal sealed class ComponentRuleManagerForm : Form
{
    private const string AllSystemsText = "全部体系";
    private const string CalculationFactorColumnName = "CalculationFactorPreview";
    private const string ReferenceCodeColumnName = "ReferenceCode";

    private readonly Func<string?> _selectBlockName;
    private readonly ProjectParams _projectParams;
    private readonly StatisticsService _statisticsService = new();
    private readonly ComboBox _systemFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = false,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AllowUserToAddRows = false
    };
    private readonly BindingSource _binding = new();

    public List<ComponentRule> Rules { get; private set; }
    public List<string> Systems { get; private set; } = [];
    public List<ProductSystem> ProductSystems { get; private set; }

    public ComponentRuleManagerForm(IEnumerable<ComponentRule> rules, IEnumerable<string> systems, ProjectParams projectParams, Func<string?> selectBlockName)
        : this(rules, systems.Select(system => new ProductSystem { Name = system }), projectParams, selectBlockName)
    {
    }

    public ComponentRuleManagerForm(IEnumerable<ComponentRule> rules, IEnumerable<ProductSystem> productSystems, ProjectParams projectParams, Func<string?> selectBlockName)
    {
        _selectBlockName = selectBlockName;
        _projectParams = projectParams;
        Rules = rules.Select(CloneRule).ToList();
        ProductSystems = productSystems.Select(CloneSystem).ToList();
        EnsureSystemsFromRules();
        EnsureReferenceCodes();

        Text = "构件产品库维护";
        Width = 1060;
        Height = 580;
        MinimumSize = new Size(900, 480);
        StartPosition = FormStartPosition.CenterParent;

        AddColumns();
        _grid.DataSource = _binding;
        _grid.CellFormatting += GridCellFormatting;
        _grid.CellEndEdit += GridCellEndEdit;
        _grid.CellDoubleClick += GridCellDoubleClick;
        _grid.CurrentCellDirtyStateChanged += GridCurrentCellDirtyStateChanged;
        _grid.DataError += (_, _) => { };

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        shell.Controls.Add(BuildToolbar(), 0, 0);
        shell.Controls.Add(_grid, 0, 1);
        shell.Controls.Add(BuildBottomBar(), 0, 2);
        Controls.Add(shell);

        RefreshSystemFilter(AllSystemsText);
        RefreshGrid();
    }

    private FlowLayoutPanel BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 54,
            Padding = new Padding(8),
            WrapContents = false,
            AutoScroll = true
        };
        toolbar.Controls.Add(new Label { Text = "体系", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        toolbar.Controls.Add(_systemFilter);
        toolbar.Controls.Add(ToolButton("新增体系", AddSystem));
        toolbar.Controls.Add(ToolButton("参数维护", ManageParameters));
        toolbar.Controls.Add(ToolButton("添加构件", AddRule));
        toolbar.Controls.Add(ToolButton("从图中更新图块", PickBlockForSelectedRule));
        toolbar.Controls.Add(ToolButton("删除构件", DeleteRule));
        toolbar.Controls.Add(ToolButton("导入规则", ImportRules));
        toolbar.Controls.Add(ToolButton("导出规则", ExportRules));
        return toolbar;
    }

    private FlowLayoutPanel BuildBottomBar()
    {
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 12, 16, 8),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        bottom.Controls.Add(ToolButton("保存", () =>
        {
            EndGridEdit();
            DialogResult = DialogResult.OK;
            Close();
        }));
        bottom.Controls.Add(ToolButton("取消", () =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }));
        return bottom;
    }

    private void AddColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "体系", DataPropertyName = nameof(ComponentRule.SystemName), Width = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "分组", DataPropertyName = nameof(ComponentRule.GroupName), Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "构件名称", DataPropertyName = nameof(ComponentRule.ComponentName), Width = 140 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = ReferenceCodeColumnName, HeaderText = "引用代号", DataPropertyName = nameof(ComponentRule.ReferenceCode), Width = 90, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "图块名", DataPropertyName = nameof(ComponentRule.BlockName), Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "单位", DataPropertyName = nameof(ComponentRule.Unit), Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "计算公式", DataPropertyName = nameof(ComponentRule.Formula), Width = 170 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = CalculationFactorColumnName, HeaderText = "公式预览", Width = 140, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = nameof(ComponentRule.Note), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
    }

    private void GridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_grid.Columns[e.ColumnIndex].Name != CalculationFactorColumnName)
        {
            return;
        }

        if (_grid.Rows[e.RowIndex].DataBoundItem is not ComponentRule rule)
        {
            return;
        }

        e.Value = _statisticsService.TryCalculateFactor(rule, _projectParams, out var factor, out var error)
            ? factor.ToString("0.####")
            : error;
        e.FormattingApplied = true;
    }

    private void GridCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_grid.Rows[e.RowIndex].DataBoundItem is not ComponentRule rule)
        {
            return;
        }

        NormalizeRule(rule);
        MarkModified(rule);
        AddSystemIfMissing(rule.SystemName);
        EnsureReferenceCodes();
        RefreshSystemFilter(rule.SystemName);
        RefreshGrid();
    }

    private void GridCurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (_grid.IsCurrentCellDirty)
        {
            _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void GridCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        if (_grid.Rows[e.RowIndex].DataBoundItem is ComponentRule rule)
        {
            EditRule(rule);
        }
    }

    private void AddRule()
    {
        using var form = new AddComponentRuleForm(_selectBlockName, BuildProductSystemsForRuleForm(), BuildPreviewProjectParams());
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        MarkModified(form.Rule);
        Upsert(form.Rule);
    }

    private void EditRule(ComponentRule rule)
    {
        using var form = new AddComponentRuleForm(_selectBlockName, BuildProductSystemsForRuleForm(), BuildPreviewProjectParams(), rule);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var index = Rules.IndexOf(rule);
        if (index < 0)
        {
            return;
        }

        form.Rule.Id = rule.Id;
        MarkModified(form.Rule);
        NormalizeRule(form.Rule);
        Rules[index] = form.Rule;
        AddSystemIfMissing(form.Rule.SystemName);
        EnsureReferenceCodes();
        RefreshSystemFilter(form.Rule.SystemName);
        RefreshGrid();
    }

    private ProjectParams BuildPreviewProjectParams(string? systemName = null)
    {
        var project = new ProjectParams
        {
            ProjectName = _projectParams.ProjectName,
            SelectedSystemName = string.IsNullOrWhiteSpace(systemName) ? _projectParams.SelectedSystemName : systemName,
            FloorHeightM = _projectParams.FloorHeightM,
            TemplateHeightsM = [.. _projectParams.TemplateHeightsM],
            WallThicknessMm = _projectParams.WallThicknessMm,
            Note = _projectParams.Note,
            UpdatedAt = _projectParams.UpdatedAt,
            CustomParameters = NormalizeCustomParameters(_projectParams.CustomParameters)
        };

        var systems = string.IsNullOrWhiteSpace(systemName)
            ? ProductSystems
            : ProductSystems.Where(system => string.Equals(system.Name, systemName, StringComparison.OrdinalIgnoreCase));

        foreach (var parameter in systems.SelectMany(system => system.Parameters))
        {
            if (!string.IsNullOrWhiteSpace(parameter.Key) && !project.CustomParameters.ContainsKey(NormalizeParameterKey(parameter.Key)))
            {
                project.CustomParameters[NormalizeParameterKey(parameter.Key)] = parameter.DefaultValue;
            }
        }

        return project;
    }

    private static Dictionary<string, decimal> NormalizeCustomParameters(Dictionary<string, decimal> parameters)
    {
        var normalized = new Dictionary<string, decimal>();
        foreach (var parameter in parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Key))
            {
                normalized[NormalizeParameterKey(parameter.Key)] = parameter.Value;
            }
        }

        return normalized;
    }

    private static string NormalizeParameterKey(string key) => key.Trim().ToLowerInvariant();

    private void AddSystem()
    {
        var name = PromptText("新增体系", "体系名称");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        name = name.Trim();
        if (Systems.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("该体系已存在。", "新增体系", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshSystemFilter(name);
            RefreshGrid();
            return;
        }

        ProductSystems.Add(new ProductSystem { Name = name });
        EnsureSystemsFromRules();
        RefreshSystemFilter(name);
        RefreshGrid();
    }

    private void ManageParameters()
    {
        var system = SelectedProductSystem();
        if (system is null)
        {
            MessageBox.Show("请先选择或新增一个体系。", "参数维护", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var rules = Rules.Where(rule => string.Equals(rule.SystemName, system.Name, StringComparison.OrdinalIgnoreCase));
        using var form = new SystemParameterManagerForm(system.Name, system.Parameters, rules);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        system.Parameters = form.Parameters;
    }

    private void PickBlockForSelectedRule()
    {
        var rule = SelectedRule();
        if (rule is null)
        {
            return;
        }

        Hide();
        try
        {
            var blockName = _selectBlockName();
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return;
            }

            rule.BlockName = blockName.Trim();
            if (string.IsNullOrWhiteSpace(rule.ComponentName))
            {
                rule.ComponentName = rule.BlockName;
            }

            MarkModified(rule);
            RefreshGrid();
        }
        finally
        {
            Show();
            Activate();
        }
    }

    private void DeleteRule()
    {
        var rule = SelectedRule();
        if (rule is null)
        {
            return;
        }

        if (MessageBox.Show($"确定删除“{rule.ComponentName}”？", "删除构件", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        Rules.Remove(rule);
        EnsureReferenceCodes();
        RefreshGrid();
    }

    private void ImportRules()
    {
        using var dialog = new OpenFileDialog { Filter = "JSON 文件 (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var json = File.ReadAllText(dialog.FileName);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var importedLibrary = JsonSerializer.Deserialize<RuleLibrary>(json, options);
        if (importedLibrary?.ProductSystems.Count > 0)
        {
            ProductSystems = importedLibrary.ProductSystems.Select(CloneSystem).ToList();
            Rules = importedLibrary.Rules.Count > 0
                ? importedLibrary.Rules.Select(CloneRule).ToList()
                : ProductSystems.SelectMany(system => system.Rules).Select(CloneRule).ToList();
            EnsureSystemsFromRules();
            EnsureReferenceCodes();
            RefreshSystemFilter(AllSystemsText);
            RefreshGrid();
            return;
        }

        var importedRules = JsonSerializer.Deserialize<List<ComponentRule>>(json, options);
        if (importedRules is null)
        {
            return;
        }

        foreach (var rule in importedRules)
        {
            MarkModified(rule);
            Upsert(rule);
        }
    }

    private void ExportRules()
    {
        EndGridEdit();
        using var dialog = new SaveFileDialog { Filter = "JSON 文件 (*.json)|*.json", FileName = "bom-rules.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var library = new RuleLibrary
        {
            ProductSystems = ProductSystems.Select(CloneSystem).ToList(),
            Rules = Rules.Select(CloneRule).ToList()
        };
        var json = JsonSerializer.Serialize(library, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dialog.FileName, json, new System.Text.UTF8Encoding(false));
    }

    private void Upsert(ComponentRule rule)
    {
        NormalizeRule(rule);
        AddSystemIfMissing(rule.SystemName);
        EnsureReferenceCode(rule);
        var existing = Rules.FirstOrDefault(existingRule => SameRuleIdentity(existingRule, rule));
        if (existing is not null)
        {
            if (MessageBox.Show($"构件“{rule.ComponentName}”已存在，是否覆盖？", "确认覆盖", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            rule.Id = existing.Id;
            Rules[Rules.IndexOf(existing)] = rule;
        }
        else
        {
            Rules.Add(rule);
        }

        RefreshSystemFilter(rule.SystemName);
        EnsureReferenceCodes();
        RefreshGrid();
    }

    private ComponentRule? SelectedRule()
    {
        return _grid.CurrentRow?.DataBoundItem as ComponentRule;
    }

    private ProductSystem? SelectedProductSystem()
    {
        var selectedSystem = _systemFilter.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selectedSystem) || selectedSystem == AllSystemsText)
        {
            selectedSystem = Systems.FirstOrDefault();
        }

        return ProductSystems.FirstOrDefault(system => string.Equals(system.Name, selectedSystem, StringComparison.OrdinalIgnoreCase));
    }

    private List<ProductSystem> BuildProductSystemsForRuleForm()
    {
        return ProductSystems.Select(system =>
        {
            var clone = CloneSystem(system);
            clone.Rules = Rules
                .Where(rule => string.Equals(rule.SystemName, clone.Name, StringComparison.OrdinalIgnoreCase))
                .Select(CloneRule)
                .ToList();
            return clone;
        }).ToList();
    }

    private void RefreshGrid()
    {
        var selectedSystem = _systemFilter.SelectedItem?.ToString();
        var data = string.IsNullOrWhiteSpace(selectedSystem) || selectedSystem == AllSystemsText
            ? Rules
            : Rules.Where(r => string.Equals(r.SystemName, selectedSystem, StringComparison.OrdinalIgnoreCase)).ToList();

        _binding.DataSource = null;
        _binding.DataSource = data;
    }

    private void RefreshSystemFilter(string? preferredSystem)
    {
        EnsureSystemsFromRules();
        var selected = string.IsNullOrWhiteSpace(preferredSystem) ? _systemFilter.SelectedItem?.ToString() ?? AllSystemsText : preferredSystem;
        _systemFilter.SelectedIndexChanged -= SystemFilterChanged;
        _systemFilter.Items.Clear();
        _systemFilter.Items.Add(AllSystemsText);
        foreach (var system in Systems.OrderBy(s => s))
        {
            _systemFilter.Items.Add(system);
        }

        _systemFilter.SelectedItem = _systemFilter.Items.Contains(selected) ? selected : AllSystemsText;
        _systemFilter.SelectedIndexChanged += SystemFilterChanged;
    }

    private void SystemFilterChanged(object? sender, EventArgs e)
    {
        RefreshGrid();
    }

    private void AddSystemIfMissing(string systemName)
    {
        if (Systems.Any(s => string.Equals(s, systemName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        ProductSystems.Add(new ProductSystem { Name = systemName });
        EnsureSystemsFromRules();
    }

    private void EnsureSystemsFromRules()
    {
        foreach (var rule in Rules)
        {
            NormalizeRule(rule);
            if (!ProductSystems.Any(system => string.Equals(system.Name, rule.SystemName, StringComparison.OrdinalIgnoreCase)))
            {
                ProductSystems.Add(new ProductSystem { Name = rule.SystemName });
            }
        }

        Systems = ProductSystems
            .Select(system => string.IsNullOrWhiteSpace(system.Name) ? "默认体系" : system.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (Systems.Count == 0)
        {
            Systems.Add("默认体系");
            ProductSystems.Add(new ProductSystem { Name = "默认体系" });
        }
    }

    private void EndGridEdit()
    {
        _grid.EndEdit();
        _binding.EndEdit();
        foreach (var rule in Rules)
        {
            NormalizeRule(rule);
        }
        EnsureReferenceCodes();

        foreach (var system in ProductSystems)
        {
            system.Rules = Rules.Where(rule => string.Equals(rule.SystemName, system.Name, StringComparison.OrdinalIgnoreCase)).Select(CloneRule).ToList();
        }
    }

    private static void NormalizeRule(ComponentRule rule)
    {
        rule.SystemName = string.IsNullOrWhiteSpace(rule.SystemName) ? "默认体系" : rule.SystemName.Trim();
        rule.GroupName = string.IsNullOrWhiteSpace(rule.GroupName) ? "主体构件" : rule.GroupName.Trim();
        rule.BlockName = rule.BlockName?.Trim() ?? "";
        rule.ComponentName = string.IsNullOrWhiteSpace(rule.ComponentName)
            ? (string.IsNullOrWhiteSpace(rule.BlockName) ? "未命名构件" : rule.BlockName)
            : rule.ComponentName.Trim();
        rule.ReferenceCode = rule.ReferenceCode?.Trim() ?? "";
        rule.Unit = string.IsNullOrWhiteSpace(rule.Unit) ? "个" : rule.Unit.Trim();
        rule.CalculationMode = "Formula";
        rule.Formula = string.IsNullOrWhiteSpace(rule.Formula) ? "1" : rule.Formula.Trim();
        rule.BaseQtyPerBlock = rule.BaseQtyPerBlock <= 0 ? 1 : rule.BaseQtyPerBlock;
        rule.SpacingMm = rule.SpacingMm <= 0 ? 600 : rule.SpacingMm;
    }

    private void EnsureReferenceCodes()
    {
        foreach (var systemName in Systems)
        {
            var systemRules = Rules
                .Where(rule => string.Equals(rule.SystemName, systemName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in systemRules)
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
    }

    private void EnsureReferenceCode(ComponentRule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.ReferenceCode))
        {
            rule.ReferenceCode = rule.ReferenceCode.Trim();
            return;
        }

        var usedCodes = Rules
            .Where(existing => string.Equals(existing.SystemName, rule.SystemName, StringComparison.OrdinalIgnoreCase))
            .Select(existing => existing.ReferenceCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        rule.ReferenceCode = NextReferenceCode(usedCodes);
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

    private static bool SameRuleIdentity(ComponentRule left, ComponentRule right)
    {
        if (!string.IsNullOrWhiteSpace(left.BlockName) && !string.IsNullOrWhiteSpace(right.BlockName))
        {
            return string.Equals(left.BlockName, right.BlockName, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(left.SystemName, right.SystemName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.ComponentName, right.ComponentName, StringComparison.OrdinalIgnoreCase);
    }

    private static void MarkModified(ComponentRule rule)
    {
        rule.IsModified = true;
        rule.Source = string.IsNullOrWhiteSpace(rule.Source) ? "User" : rule.Source;
        rule.UpdatedAt = DateTime.Now;
    }

    private static Button ToolButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30, UseVisualStyleBackColor = true };
        button.Click += (_, _) => action();
        return button;
    }

    private static string? PromptText(string title, string label)
    {
        using var form = new Form
        {
            Text = title,
            Width = 380,
            Height = 160,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var textBox = new TextBox { Left = 100, Top = 22, Width = 230 };
        var ok = new Button { Text = "确定", Left = 164, Top = 72, Width = 76, Height = 32, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "取消", Left = 252, Top = 72, Width = 76, Height = 32, DialogResult = DialogResult.Cancel };

        form.Controls.Add(new Label { Text = label, Left = 20, Top = 25, AutoSize = true });
        form.Controls.Add(textBox);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }

    private static ProductSystem CloneSystem(ProductSystem system)
    {
        return new ProductSystem
        {
            Id = string.IsNullOrWhiteSpace(system.Id) ? Guid.NewGuid().ToString("N") : system.Id,
            Name = string.IsNullOrWhiteSpace(system.Name) ? "默认体系" : system.Name,
            Parameters = system.Parameters.Select(parameter => new SystemParameterDefinition
            {
                Key = parameter.Key,
                Name = parameter.Name,
                Unit = parameter.Unit,
                DefaultValue = parameter.DefaultValue,
                Description = parameter.Description
            }).ToList(),
            Rules = system.Rules.Select(CloneRule).ToList()
        };
    }

    private static ComponentRule CloneRule(ComponentRule rule)
    {
        return new ComponentRule
        {
            Id = rule.Id,
            SystemName = string.IsNullOrWhiteSpace(rule.SystemName) ? "默认体系" : rule.SystemName,
            GroupName = string.IsNullOrWhiteSpace(rule.GroupName) ? "主体构件" : rule.GroupName,
            BlockName = rule.BlockName,
            ComponentName = rule.ComponentName,
            ReferenceCode = rule.ReferenceCode,
            Unit = rule.Unit,
            BaseQtyPerBlock = rule.BaseQtyPerBlock,
            CalculationMode = "Formula",
            SpacingMm = rule.SpacingMm > 0 ? rule.SpacingMm : 600,
            Formula = string.IsNullOrWhiteSpace(rule.Formula) ? "1" : rule.Formula,
            VerticalRows = rule.VerticalRows,
            Note = rule.Note,
            LibraryVersion = rule.LibraryVersion,
            Source = rule.Source,
            UpdatedAt = rule.UpdatedAt,
            IsModified = rule.IsModified
        };
    }
}
