using System.Text.RegularExpressions;
using BomCadPlugin.Core.Models;

namespace BomCadPlugin.UI;

internal sealed class SystemParameterManagerForm : Form
{
    private readonly string _systemName;
    private readonly IReadOnlyList<ComponentRule> _rules;
    private readonly BindingSource _binding = new();
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false
    };

    public SystemParameterManagerForm(string systemName, IEnumerable<SystemParameterDefinition> parameters, IEnumerable<ComponentRule> rules)
    {
        _systemName = systemName;
        _rules = rules.Select(CloneRule).ToList();
        Parameters = parameters.Select(CloneParameter).ToList();

        Text = $"参数维护 - {_systemName}";
        Width = 720;
        Height = 420;
        MinimumSize = new Size(640, 360);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        AddColumns();
        _grid.DataSource = _binding;

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        shell.Controls.Add(BuildToolbar(), 0, 0);
        shell.Controls.Add(_grid, 0, 1);
        shell.Controls.Add(BuildBottomBar(), 0, 2);
        Controls.Add(shell);

        RefreshGrid();
    }

    public List<SystemParameterDefinition> Parameters { get; private set; }

    private FlowLayoutPanel BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            WrapContents = false
        };
        toolbar.Controls.Add(ToolButton("新增", AddParameter));
        toolbar.Controls.Add(ToolButton("编辑", EditParameter));
        toolbar.Controls.Add(ToolButton("删除", DeleteParameter));
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
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "参数代号", DataPropertyName = nameof(SystemParameterDefinition.Key), Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "参数名称", DataPropertyName = nameof(SystemParameterDefinition.Name), Width = 140 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "单位", DataPropertyName = nameof(SystemParameterDefinition.Unit), Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "默认值", DataPropertyName = nameof(SystemParameterDefinition.DefaultValue), Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = nameof(SystemParameterDefinition.Description), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
    }

    private void AddParameter()
    {
        using var form = new SystemParameterForm(_systemName, Parameters.Select(parameter => parameter.Key));
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        Parameters.Add(CloneParameter(form.Parameter));
        RefreshGrid();
    }

    private void EditParameter()
    {
        var parameter = SelectedParameter();
        if (parameter is null)
        {
            return;
        }

        using var form = new SystemParameterForm(_systemName, Parameters.Select(item => item.Key), parameter);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var index = Parameters.IndexOf(parameter);
        if (index >= 0)
        {
            Parameters[index] = CloneParameter(form.Parameter);
            RefreshGrid();
        }
    }

    private void DeleteParameter()
    {
        var parameter = SelectedParameter();
        if (parameter is null)
        {
            return;
        }

        var message = BuildDeleteMessage(parameter);
        if (MessageBox.Show(message, "删除参数", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        Parameters.Remove(parameter);
        RefreshGrid();
    }

    private string BuildDeleteMessage(SystemParameterDefinition parameter)
    {
        var referencedRules = _rules
            .Where(rule => FormulaContainsIdentifier(rule.Formula, parameter.Key))
            .Select(rule => string.IsNullOrWhiteSpace(rule.ComponentName) ? rule.BlockName : rule.ComponentName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        if (referencedRules.Count == 0)
        {
            return $"确定删除参数“{parameter.Key}”吗？";
        }

        return $"参数“{parameter.Key}”可能正在被构件公式引用，删除后这些公式会提示参数未定义或未赋值。\r\n\r\n相关构件：{string.Join("、", referencedRules)}\r\n\r\n确定删除吗？";
    }

    private static bool FormulaContainsIdentifier(string formula, string key)
    {
        if (string.IsNullOrWhiteSpace(formula) || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return Regex.IsMatch(formula, $@"(?<![A-Za-z0-9_]){Regex.Escape(key)}(?![A-Za-z0-9_])", RegexOptions.IgnoreCase);
    }

    private SystemParameterDefinition? SelectedParameter()
    {
        return _grid.CurrentRow?.DataBoundItem as SystemParameterDefinition;
    }

    private void RefreshGrid()
    {
        _binding.DataSource = null;
        _binding.DataSource = Parameters;
    }

    private static Button ToolButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 30, UseVisualStyleBackColor = true };
        button.Click += (_, _) => action();
        return button;
    }

    private static SystemParameterDefinition CloneParameter(SystemParameterDefinition parameter)
    {
        return new SystemParameterDefinition
        {
            Key = parameter.Key,
            Name = parameter.Name,
            Unit = parameter.Unit,
            DefaultValue = parameter.DefaultValue,
            Description = parameter.Description
        };
    }

    private static ComponentRule CloneRule(ComponentRule rule)
    {
        return new ComponentRule
        {
            BlockName = rule.BlockName,
            ComponentName = rule.ComponentName,
            ReferenceCode = rule.ReferenceCode,
            Formula = rule.Formula
        };
    }
}
