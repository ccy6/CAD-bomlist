using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using BomCadPlugin.Core.Models;
using BomCadPlugin.Core.Services;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using CadColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;

namespace BomCadPlugin.Services;

internal sealed class CadTableWriter
{
    private static readonly double[] ColumnWidths = [12, 42, 52, 12, 22, 22, 24, 48];

    public bool InsertBomTable(BomStatResult result, ProjectParams project)
    {
        var editor = CadDocumentService.Editor;
        var pointResult = editor.GetPoint("\n请选择 BOM 统计表插入点：");
        if (pointResult.Status != PromptStatus.OK)
        {
            return false;
        }

        var tableData = BomTableDataBuilder.Build(result, project);
        InsertTable(pointResult.Value, tableData);
        editor.WriteMessage("\nBOM 统计表已写入 CAD。");
        return true;
    }

    private static void InsertTable(Point3d position, BomTableData tableData)
    {
        var database = CadDocumentService.Database;
        using var transaction = database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);

        var table = new Table
        {
            TableStyle = database.Tablestyle,
            Position = position
        };

        var columnCount = tableData.Headers.Count;
        var rowCount = tableData.Rows.Count + 3;
        table.SetSize(rowCount, columnCount);
        table.SetRowHeight(8);
        table.SetColumnWidth(24);

        for (var column = 0; column < columnCount && column < ColumnWidths.Length; column++)
        {
            table.Columns[column].Width = ColumnWidths[column];
        }

        table.Cells[0, 0].TextString = tableData.Title;
        table.Cells[1, 0].TextString = tableData.Subtitle;
        table.MergeCells(CellRange.Create(table, 0, 0, 0, columnCount - 1));
        table.MergeCells(CellRange.Create(table, 1, 0, 1, columnCount - 1));

        FormatMergedHeader(table, 0, 3.5);
        FormatMergedHeader(table, 1, 2.4);

        for (var column = 0; column < columnCount; column++)
        {
            var cell = table.Cells[2, column];
            cell.TextString = tableData.Headers[column];
            cell.Alignment = CellAlignment.MiddleCenter;
            cell.TextHeight = 2.5;
            cell.ContentColor = CadColor.FromColorIndex(CadColorMethod.ByAci, 7);
        }

        for (var row = 0; row < tableData.Rows.Count; row++)
        {
            var values = tableData.Rows[row];
            for (var column = 0; column < columnCount; column++)
            {
                var cell = table.Cells[row + 3, column];
                cell.TextString = column < values.Count ? values[column] : "";
                cell.Alignment = column is 1 or 2 or 7 ? CellAlignment.MiddleLeft : CellAlignment.MiddleCenter;
                cell.TextHeight = 2.3;
                cell.ContentColor = CadColor.FromColorIndex(CadColorMethod.ByAci, 7);
            }
        }

        table.GenerateLayout();
        space.AppendEntity(table);
        transaction.AddNewlyCreatedDBObject(table, true);
        transaction.Commit();
    }

    private static void FormatMergedHeader(Table table, int row, double textHeight)
    {
        var cell = table.Cells[row, 0];
        cell.Alignment = CellAlignment.MiddleCenter;
        cell.TextHeight = textHeight;
        cell.ContentColor = CadColor.FromColorIndex(CadColorMethod.ByAci, 7);
        table.Rows[row].Height = row == 0 ? 10 : 8;
    }
}
