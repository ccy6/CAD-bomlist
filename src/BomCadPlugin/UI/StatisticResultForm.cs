using BomCadPlugin.Core.Models;

namespace BomCadPlugin.UI;

internal enum StatisticResultAction
{
    None,
    ExportCsv,
    WriteCad,
    Recalculate
}

internal sealed class StatisticResultForm : Form
{
    public StatisticResultForm(BomStatResult result)
    {
        Text = "平面统计结果 / BOM清单";
        Width = 980;
        Height = 520;
        MinimumSize = new Size(760, 420);
        StartPosition = FormStartPosition.CenterParent;

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(12, 11, 12, 0),
            Text = $"统计区域：当前结果    统计时间：{result.StatTime:yyyy-MM-dd HH:mm:ss}"
        };

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoGenerateColumns = false,
            DataSource = result.Items,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "构件名称", DataPropertyName = nameof(BomStatItem.ComponentName), Width = 150 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "图块名", DataPropertyName = nameof(BomStatItem.BlockName), Width = 160 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "单位", DataPropertyName = nameof(BomStatItem.Unit), Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "平面数量", DataPropertyName = nameof(BomStatItem.PlaneCount), Width = 90 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "计算系数", DataPropertyName = nameof(BomStatItem.CalculationFactor), Width = 90 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "总数量", DataPropertyName = nameof(BomStatItem.TotalQty), Width = 90 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "计算提示", DataPropertyName = nameof(BomStatItem.CalculationError), Width = 180 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = nameof(BomStatItem.Note), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            Padding = new Padding(12),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        footer.Controls.Add(ActionButton("关闭", StatisticResultAction.None));
        footer.Controls.Add(ActionButton("重新统计", StatisticResultAction.Recalculate));
        footer.Controls.Add(ActionButton("写入CAD", StatisticResultAction.WriteCad));
        footer.Controls.Add(ActionButton("导出CSV", StatisticResultAction.ExportCsv));
        footer.Controls.Add(new Label
        {
            Text = $"合计：{result.Items.Count} 项构件",
            AutoSize = true,
            Padding = new Padding(0, 8, 24, 0)
        });

        Controls.Add(grid);
        Controls.Add(header);
        Controls.Add(footer);
    }

    public StatisticResultAction RequestedAction { get; private set; }

    private Button ActionButton(string text, StatisticResultAction action)
    {
        var button = new Button { Text = text, Width = 96, Height = 32, UseVisualStyleBackColor = true };
        button.Click += (_, _) =>
        {
            RequestedAction = action;
            Close();
        };
        return button;
    }
}
