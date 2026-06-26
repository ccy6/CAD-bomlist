using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;

namespace BomCadPlugin.UI;

internal sealed class AddComponentRuleForm : Form
{
    private readonly Func<string?> _selectBlockName;
    private readonly IReadOnlyList<ProductSystem> _productSystems;
    private readonly ProjectParams _projectParams;
    private readonly ComboBox _systemName = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 220 };
    private readonly TextBox _groupName = new() { Text = "主体构件", Width = 220 };
    private readonly TextBox _componentName = new() { Width = 220 };
    private readonly TextBox _blockName = new() { Width = 220 };
    private readonly ComboBox _unit = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 220 };
    private readonly TextBox _formula = new() { Text = "1", Width = 260 };
    private readonly Label _parameterHelp = new() { AutoSize = true, MaximumSize = new Size(380, 0) };
    private readonly Label _preview = new() { AutoSize = true };
    private readonly TextBox _note = new() { Multiline = true, Height = 70, Width = 260 };

    public ComponentRule Rule { get; private set; } = new();

    public AddComponentRuleForm(Func<string?> selectBlockName, IEnumerable<string> systems, ProjectParams projectParams, ComponentRule? initialRule = null)
        : this(selectBlockName, systems.Select(system => new ProductSystem { Name = system }), projectParams, initialRule)
    {
    }

    public AddComponentRuleForm(Func<string?> selectBlockName, IEnumerable<ProductSystem> productSystems, ProjectParams projectParams, ComponentRule? initialRule = null)
    {
        _selectBlockName = selectBlockName;
        _productSystems = productSystems.Select(CloneSystem).ToList();
        _projectParams = projectParams;
        Text = initialRule is null ? "添加构件" : "编辑构件";
        Width = 580;
        Height = 560;
        MinimumSize = new Size(540, 500);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _systemName.Items.AddRange(_productSystems.Select(system => system.Name).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        _systemName.Text = _systemName.Items.Count > 0 ? _systemName.Items[0]?.ToString() : "默认体系";
        _unit.Items.AddRange(["个", "根", "套", "块", "件", "米", "m"]);
        _unit.Text = "个";
        _systemName.TextChanged += (_, _) => UpdateParameterHelp();
        _formula.TextChanged += (_, _) => UpdatePreview();

        if (initialRule is not null)
        {
            Rule = Clone(initialRule);
            _systemName.Text = Rule.SystemName;
            _groupName.Text = Rule.GroupName;
            _componentName.Text = Rule.ComponentName;
            _blockName.Text = Rule.BlockName;
            _unit.Text = Rule.Unit;
            _formula.Text = string.IsNullOrWhiteSpace(Rule.Formula) ? "1" : Rule.Formula;
            _note.Text = Rule.Note;
        }

        var pick = new Button { Text = "从图中选择图块", Width = 128, Height = 28 };
        pick.Click += (_, _) => PickBlockName();

        var clearBlock = new Button { Text = "清空", Width = 56, Height = 28 };
        clearBlock.Click += (_, _) => _blockName.Clear();

        var blockPanel = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Fill, AutoSize = true };
        blockPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        blockPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        blockPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        blockPanel.Controls.Add(_blockName, 0, 0);
        blockPanel.Controls.Add(pick, 1, 0);
        blockPanel.Controls.Add(clearBlock, 2, 0);

        var layout = ProjectParamsForm.FormLayout();
        layout.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, 96);
        layout.Controls.Add(new Label { Text = "所属体系", AutoSize = true }, 0, 0);
        layout.Controls.Add(_systemName, 1, 0);
        layout.Controls.Add(new Label { Text = "构件分组", AutoSize = true }, 0, 1);
        layout.Controls.Add(_groupName, 1, 1);
        layout.Controls.Add(new Label { Text = "构件名称", AutoSize = true }, 0, 2);
        layout.Controls.Add(_componentName, 1, 2);
        layout.Controls.Add(new Label { Text = "图块名", AutoSize = true }, 0, 3);
        layout.Controls.Add(blockPanel, 1, 3);
        layout.Controls.Add(new Label { Text = "单位", AutoSize = true }, 0, 4);
        layout.Controls.Add(_unit, 1, 4);
        layout.Controls.Add(new Label { Text = "计算公式", AutoSize = true }, 0, 5);
        layout.Controls.Add(_formula, 1, 5);
        layout.Controls.Add(new Label { Text = "可用变量", AutoSize = true }, 0, 6);
        layout.Controls.Add(_parameterHelp, 1, 6);
        layout.Controls.Add(new Label { Text = "公式预览", AutoSize = true }, 0, 7);
        layout.Controls.Add(_preview, 1, 7);
        layout.Controls.Add(new Label { Text = "备注", AutoSize = true }, 0, 8);
        layout.Controls.Add(_note, 1, 8);

        Controls.Add(ProjectParamsForm.FormShell(layout, Save));
        UpdateParameterHelp();
        UpdatePreview();
    }

    private void PickBlockName()
    {
        Hide();
        try
        {
            var name = _selectBlockName();
            if (!string.IsNullOrWhiteSpace(name))
            {
                _blockName.Text = name;
                if (string.IsNullOrWhiteSpace(_componentName.Text))
                {
                    _componentName.Text = name;
                }
            }
        }
        finally
        {
            Show();
            Activate();
        }
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_systemName.Text) ||
            string.IsNullOrWhiteSpace(_componentName.Text) ||
            string.IsNullOrWhiteSpace(_formula.Text))
        {
            MessageBox.Show("体系、构件名称和计算公式不能为空。图块名可以为空，用于参数构件。", "规则不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!TryEvaluateFormula(out var error))
        {
            MessageBox.Show(error, "公式不正确", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Rule.SystemName = _systemName.Text.Trim();
        Rule.GroupName = string.IsNullOrWhiteSpace(_groupName.Text) ? "主体构件" : _groupName.Text.Trim();
        Rule.ComponentName = _componentName.Text.Trim();
        Rule.ReferenceCode = ResolveReferenceCode();
        Rule.BlockName = _blockName.Text.Trim();
        Rule.Unit = _unit.Text.Trim();
        Rule.BaseQtyPerBlock = 1;
        Rule.CalculationMode = "Formula";
        Rule.Formula = _formula.Text.Trim();
        Rule.Note = _note.Text.Trim();
        Rule.IsModified = true;
        Rule.UpdatedAt = DateTime.Now;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdatePreview()
    {
        _preview.Text = TryEvaluateFormula(out var message) ? message : message;
    }

    private bool TryEvaluateFormula(out string message)
    {
        try
        {
            var coefficient = Math.Ceiling(new FormulaExpressionEvaluator().Evaluate(_formula.Text, SampleVariables(_projectParams)));
            message = coefficient.ToString("0.####");
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private Dictionary<string, decimal> SampleVariables(ProjectParams projectParams)
    {
        var variables = new Dictionary<string, decimal>
        {
            ["h"] = 3000,
            ["n"] = 2,
            ["floorheight"] = 3000,
            ["t"] = 200,
            ["wallthickness"] = 200,
            ["count"] = 1
        };

        for (var i = 1; i <= 20; i++)
        {
            variables[$"h{i}"] = 1500;
        }

        foreach (var parameter in projectParams.CustomParameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Key))
            {
                variables[NormalizeParameterKey(parameter.Key)] = parameter.Value;
            }
        }

        var system = _productSystems
            .FirstOrDefault(system => string.Equals(system.Name, _systemName.Text, StringComparison.OrdinalIgnoreCase));
        if (system is not null)
        {
            foreach (var parameter in system.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(parameter.Key) && !variables.ContainsKey(NormalizeParameterKey(parameter.Key)))
                {
                    variables[NormalizeParameterKey(parameter.Key)] = parameter.DefaultValue == 0 ? 1 : parameter.DefaultValue;
                }
            }

            foreach (var rule in system.Rules)
            {
                if (string.IsNullOrWhiteSpace(rule.ReferenceCode))
                {
                    continue;
                }

                var referenceCode = rule.ReferenceCode.Trim();
                variables.TryAdd(referenceCode, 1);
                variables.TryAdd($"{referenceCode}_raw", 1);
                variables.TryAdd($"{referenceCode}_count", 1);
                variables.TryAdd($"{referenceCode}_qty", 1);
            }
        }

        return variables;
    }

    private static string NormalizeParameterKey(string key) => key.Trim().ToLowerInvariant();

    private void UpdateParameterHelp()
    {
        var system = _productSystems
            .FirstOrDefault(system => string.Equals(system.Name, _systemName.Text, StringComparison.OrdinalIgnoreCase));
        var parameters = system?.Parameters ?? [];
        var componentReferences = system?.Rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.ReferenceCode))
            .Select(rule => $"{rule.ReferenceCode.Trim()}={rule.ComponentName}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        var help = SystemParameterDisplayFormatter.FormatFormulaHelp(parameters);
        if (componentReferences.Count > 0)
        {
            help += "\r\n" + string.Join("\r\n", componentReferences);
        }

        _parameterHelp.Text = help;
    }

    private string ResolveReferenceCode()
    {
        if (!string.IsNullOrWhiteSpace(Rule.ReferenceCode))
        {
            return Rule.ReferenceCode.Trim();
        }

        var system = _productSystems
            .FirstOrDefault(system => string.Equals(system.Name, _systemName.Text, StringComparison.OrdinalIgnoreCase));
        var usedCodes = system?.Rules
            .Where(rule => !string.Equals(rule.Id, Rule.Id, StringComparison.OrdinalIgnoreCase))
            .Select(rule => rule.ReferenceCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

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

    private static ComponentRule Clone(ComponentRule rule)
    {
        return new ComponentRule
        {
            Id = rule.Id,
            SystemName = string.IsNullOrWhiteSpace(rule.SystemName) ? "默认体系" : rule.SystemName,
            GroupName = string.IsNullOrWhiteSpace(rule.GroupName) ? "主体构件" : rule.GroupName,
            BlockName = rule.BlockName,
            ComponentName = string.IsNullOrWhiteSpace(rule.ComponentName) ? rule.BlockName : rule.ComponentName,
            ReferenceCode = rule.ReferenceCode,
            Unit = rule.Unit,
            BaseQtyPerBlock = 1,
            CalculationMode = "Formula",
            Formula = string.IsNullOrWhiteSpace(rule.Formula) ? "1" : rule.Formula,
            VerticalRows = rule.VerticalRows,
            Note = rule.Note,
            LibraryVersion = rule.LibraryVersion,
            Source = rule.Source,
            UpdatedAt = rule.UpdatedAt,
            IsModified = rule.IsModified
        };
    }

    private static ProductSystem CloneSystem(ProductSystem system)
    {
        return new ProductSystem
        {
            Id = system.Id,
            Name = string.IsNullOrWhiteSpace(system.Name) ? "默认体系" : system.Name,
            Parameters = system.Parameters.Select(parameter => new SystemParameterDefinition
            {
                Key = parameter.Key,
                Name = parameter.Name,
                Unit = parameter.Unit,
                DefaultValue = parameter.DefaultValue,
                Description = parameter.Description
            }).ToList(),
            Rules = system.Rules.Select(Clone).ToList()
        };
    }
}
