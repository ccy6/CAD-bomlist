using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;

namespace BomCadPlugin.UI;

internal sealed class AddComponentRuleForm : Form
{
    private readonly Func<string?> _selectBlockName;
    private readonly ProjectParams _projectParams;
    private readonly StatisticsService _statisticsService = new();
    private readonly ComboBox _systemName = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 220 };
    private readonly TextBox _groupName = new() { Text = "主体构件", Width = 220 };
    private readonly TextBox _componentName = new() { Width = 220 };
    private readonly TextBox _blockName = new() { Width = 220 };
    private readonly ComboBox _unit = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 220 };
    private readonly TextBox _formula = new() { Text = "1", Width = 260 };
    private readonly Label _preview = new() { AutoSize = true };
    private readonly TextBox _note = new() { Multiline = true, Height = 70, Width = 260 };

    public ComponentRule Rule { get; private set; } = new();

    public AddComponentRuleForm(Func<string?> selectBlockName, IEnumerable<string> systems, ProjectParams projectParams, ComponentRule? initialRule = null)
    {
        _selectBlockName = selectBlockName;
        _projectParams = projectParams;
        Text = initialRule is null ? "添加构件" : "编辑构件";
        Width = 580;
        Height = 560;
        MinimumSize = new Size(540, 500);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _systemName.Items.AddRange(systems.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        _systemName.Text = _systemName.Items.Count > 0 ? _systemName.Items[0]?.ToString() : "默认体系";
        _unit.Items.AddRange(["个", "根", "套", "块", "件", "米", "m"]);
        _unit.Text = "个";
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
        layout.Controls.Add(new Label { Text = "可用参数", AutoSize = true }, 0, 6);
        layout.Controls.Add(new Label { Text = ParameterHelpText(), AutoSize = true, MaximumSize = new Size(380, 0) }, 1, 6);
        layout.Controls.Add(new Label { Text = "公式预览", AutoSize = true }, 0, 7);
        layout.Controls.Add(_preview, 1, 7);
        layout.Controls.Add(new Label { Text = "备注", AutoSize = true }, 0, 8);
        layout.Controls.Add(_note, 1, 8);

        Controls.Add(ProjectParamsForm.FormShell(layout, Save));
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
        Rule.BlockName = _blockName.Text.Trim();
        Rule.Unit = _unit.Text.Trim();
        Rule.BaseQtyPerBlock = 1;
        Rule.CalculationMode = "Formula";
        Rule.Formula = _formula.Text.Trim().ToLowerInvariant();
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
            new FormulaExpressionEvaluator().Evaluate(_formula.Text, SampleVariables(_projectParams));
            if (!_statisticsService.TryCalculateFactor(new ComponentRule { Formula = _formula.Text }, _projectParams, out var coefficient, out var error))
            {
                message = error;
                return false;
            }

            message = coefficient > 0 || _formula.Text.Trim() == "0"
                ? coefficient.ToString("0.####")
                : "公式有效，项目参数未完整";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private static Dictionary<string, decimal> SampleVariables(ProjectParams projectParams)
    {
        var variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["h"] = 3000,
            ["n"] = 2,
            ["floorHeight"] = 3000,
            ["wallThickness"] = 200,
            ["count"] = 1
        };

        for (var i = 1; i <= 20; i++)
        {
            variables[$"h{i}"] = 1500;
        }

        foreach (var parameter in projectParams.CustomParameters)
        {
            variables[parameter.Key] = parameter.Value;
        }

        return variables;
    }

    private static string ParameterHelpText()
    {
        return "count=图块数量；L/H/S 等为当前体系自定义参数\r\nh=模板总高度mm，n=模板段数，h1/h2...=每段模板高度mm";
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
}
