# BOM 清单统计 CAD 插件 V1

基于 [`1md/BOM清单统计插件_V1需求说明.md`](1md/BOM清单统计插件_V1需求说明.md) 实现的 AutoCAD C# 插件。插件从图纸中的块参照读取构件信息，根据配置规则计算 BOM，并支持写入 CAD 表格和导出 CSV。

## 支持版本

发布脚本从同一套业务源码生成两个安装包：

| 安装包 | AutoCAD 版本 | 目标框架 | 编译引用 |
| --- | --- | --- | --- |
| `BomCadPlugin-2018-2024.bundle` | 2018 | .NET Framework 4.8 | AutoCAD 2018 |
| `BomCadPlugin-2018-2024.bundle` | 2019–2020 | .NET Framework 4.8 | AutoCAD 2019 |
| `BomCadPlugin-2018-2024.bundle` | 2021–2024 | .NET Framework 4.8 | AutoCAD 2021 |
| `BomCadPlugin-2025.bundle` | 2025 | .NET 8 | AutoCAD 2025 |

AutoCAD 2018–2024 的目标电脑需要安装 .NET Framework 4.8。插件不支持 AutoCAD LT。

## 已实现功能

- 设置项目名称、产品体系、层高、模板高度、墙厚和自定义参数。
- 维护产品体系、构件规则、构件引用代号和计算公式。
- 点选或框选 `BlockReference` 图块并统计数量。
- 根据参数与构件引用公式计算 BOM。
- 在 AutoCAD 图纸中写入 BOM 表格。
- 导出带 UTF-8 BOM 的 CSV 文件。
- 将图纸配置保存为 DWG 同目录、同名的 `.bomconfig.json`。
- 创建“BOM清单统计”Ribbon 功能区。

## 项目结构

```text
src/
  BomCadPlugin.Core/       共享模型、配置、公式、统计和导出；目标为 net48 与 net8.0
  BomCadPlugin/            AutoCAD 命令、Ribbon 和 WinForms；按 CAD 版本选择目标框架
tests/
  BomCadPlugin.Core.Tests/ 核心业务测试
packaging/                 两个 bundle 的源清单和安装说明
scripts/
  Build-Release.ps1        四版本编译和打包
  Test-Packages.ps1        bundle 结构与运行时范围校验
```

## 开发编译

核心库不依赖 AutoCAD，可以直接编译和测试：

```powershell
dotnet build .\src\BomCadPlugin.Core\BomCadPlugin.Core.csproj -c Release -f net48
dotnet build .\src\BomCadPlugin.Core\BomCadPlugin.Core.csproj -c Release -f net8.0
dotnet test .\tests\BomCadPlugin.Core.Tests\BomCadPlugin.Core.Tests.csproj -c Release
```

编译某个 AutoCAD 版本时，需要提供与该构建行匹配的安装目录。目录内必须包含 `AcMgd.dll`、`AcDbMgd.dll`、`AcCoreMgd.dll` 和 `AdWindows.dll`：

```powershell
dotnet build .\src\BomCadPlugin\BomCadPlugin.csproj `
  -c Release `
  -p:AutoCADVersion=2025 `
  -p:AutoCADReferencePath="C:\Program Files\Autodesk\AutoCAD 2025"
```

`AutoCADVersion` 只接受 `2018`、`2019`、`2021` 或 `2025`。2019 SDK 用于 2019–2020，2021 SDK 用于 2021–2024。

## 生成发布包

如果四套 AutoCAD 引用位于标准安装目录，运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Release.ps1
```

也可以显式传入安装目录：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Release.ps1 `
  -AutoCAD2018ReferencePath "D:\Autodesk\AutoCAD 2018" `
  -AutoCAD2019ReferencePath "D:\Autodesk\AutoCAD 2019" `
  -AutoCAD2021ReferencePath "D:\Autodesk\AutoCAD 2021" `
  -AutoCAD2025ReferencePath "D:\Autodesk\AutoCAD 2025"
```

脚本会执行四次 Release 编译，在 `artifacts` 暂存，然后生成：

```text
dist/
  BomCadPlugin-2018-2024.bundle/
  BomCadPlugin-2025.bundle/
```

打包结束前会自动运行 `scripts/Test-Packages.ps1`，检查运行时范围、模块路径和 bundle 数量，并确保没有分发 Autodesk 的宿主 DLL。

## 安装

只复制与 AutoCAD 版本匹配的 `.bundle` 文件夹到：

```text
%APPDATA%\Autodesk\ApplicationPlugins\
```

重启 AutoCAD 后插件会自动加载。开发时也可执行 `NETLOAD`，选择 bundle 对应版本目录中的 `BomCadPlugin.dll`。

## CAD 命令

```text
BOM_PARAMS     设置或查看项目参数
BOM_ADD_RULE   添加构件规则
BOM_RULES      管理构件规则
BOM_STAT       统计选中的图块并计算 BOM
BOM_EXPORT     导出 CSV
BOM_CLEAR      清除当前统计结果
BOM_ABOUT      关于
```

## 验收说明

本地编译和自动化测试只能验证托管代码兼容性及包结构。最终发布前仍需在真实 AutoCAD 2018、2019/2020、2021–2024 和 2025 环境分别进行冒烟测试：加载 bundle、确认 Ribbon、运行 `BOM_PARAMS`、使用 `BOM_STAT` 统计图块、写入 CAD 表格并导出 CSV。

Autodesk 的托管 DLL 只用于编译，始终设置为不复制，也不会进入发布包。
