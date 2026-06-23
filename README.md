# BOM清单统计 CAD 插件 V1

基于 `1md/BOM清单统计插件_V1需求说明.md` 搭建的 AutoCAD C# .NET 插件第一版。

## 已实现范围

- 项目参数设置：项目名称、层高、模板高度、墙厚、备注。
- 构件规则：图块名绑定单位、竖向排数、备注。
- 从 CAD 图中点选图块并读取有效图块名，动态块优先读取动态块定义名。
- 框选区域统计 `BlockReference` / `INSERT` 图块。
- 只统计已绑定规则的图块，未绑定图块自动忽略。
- 统计公式：`总数量 = 平面数量 × 竖向排数`。
- 统计结果窗口。
- CSV 导出，使用 UTF-8 with BOM，避免中文乱码。
- 配置保存为当前 DWG 同目录同名 `.bomconfig.json`。
- Ribbon 标签页：`BOM清单统计`。

## 项目结构

```text
src/
  BomCadPlugin.Core/       核心模型、JSON 配置、统计、CSV 导出，可独立编译
  BomCadPlugin/            AutoCAD 命令、Ribbon、WinForms 窗口
```

## 编译说明

本机没有安装 AutoCAD .NET API，因此当前环境只能验证核心库：

```powershell
dotnet build .\src\BomCadPlugin.Core\BomCadPlugin.Core.csproj
```

在安装了 AutoCAD 的机器上，先确认目录中存在这些 DLL：

- `AcMgd.dll`
- `AcDbMgd.dll`
- `AcCoreMgd.dll`
- `AdWindows.dll`

如果 AutoCAD 安装路径不是默认的 `C:\Program Files\Autodesk\AutoCAD 2025`，编译时传入引用路径：

```powershell
dotnet build .\src\BomCadPlugin\BomCadPlugin.csproj -p:AutoCADReferencePath="C:\Program Files\Autodesk\AutoCAD 2025"
```

生成后在 AutoCAD 命令行执行：

```text
NETLOAD
```

选择生成目录下的：

```text
src\BomCadPlugin\bin\Debug\net8.0-windows\BomCadPlugin.dll
```

> 当前项目目标框架为 `net8.0-windows`，适合 AutoCAD 2025+ 的 .NET 8 插件环境。若你的 AutoCAD 是 2024 或更早版本，通常需要改为 .NET Framework 4.8 项目。

## CAD 命令

```text
BOM_PARAMS     打开项目参数设置
BOM_ADD_RULE   添加构件规则
BOM_RULES      构件管理
BOM_STAT       框选平面区域并统计 BOM
BOM_EXPORT     导出最近一次 BOM 统计结果
BOM_CLEAR      清除当前统计结果
BOM_ABOUT      关于
```

## 配置文件

假设当前图纸是：

```text
D:\Project\A项目结构平面图.dwg
```

插件会读写：

```text
D:\Project\A项目结构平面图.bomconfig.json
```

配置示例：

```json
{
  "version": "1.0.0",
  "project": {
    "projectName": "XX项目",
    "floorHeightM": 3,
    "templateHeightsM": [3, 2],
    "wallThicknessMm": 200,
    "note": "",
    "updatedAt": "2026-06-12T10:30:00"
  },
  "rules": [
    {
      "id": "rule-001",
      "blockName": "BLK_JG01",
      "unit": "根",
      "verticalRows": 5,
      "note": ""
    }
  ]
}
```

## 后续建议

- 在真实 AutoCAD 环境做一次 `NETLOAD` 验证，重点确认 Ribbon 显示、点选图块、框选统计。
- 如果目标 AutoCAD 版本低于 2025，先把插件项目迁移到 .NET Framework 4.8。
- 下一版可加“生成 CAD 表格”和 xlsx 导出。
