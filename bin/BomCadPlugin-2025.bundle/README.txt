BOM 清单统计插件安装说明（AutoCAD 2018–2025）
================================================

一、选择安装包

- AutoCAD 2018–2024：使用 BomCadPlugin-2018-2024.bundle。
- AutoCAD 2025：使用 BomCadPlugin-2025.bundle。
- 不支持 AutoCAD LT。
- AutoCAD 2018–2024 电脑需要安装 .NET Framework 4.8。

二、自动加载安装（推荐）

1. 只复制与 CAD 版本匹配的整个 .bundle 文件夹。
2. 将该文件夹放到：
   %APPDATA%\Autodesk\ApplicationPlugins\
3. 重启 AutoCAD。加载成功后，顶部会出现“BOM清单统计”功能区。

如需同一台电脑的所有 Windows 用户都能使用，也可放到：
C:\ProgramData\Autodesk\ApplicationPlugins\

三、开发时手动加载

在 AutoCAD 命令行执行 NETLOAD，并选择对应版本目录中的：
Contents\Windows\<版本>\BomCadPlugin.dll

手动 NETLOAD 只适合开发验证，重启 AutoCAD 后需要重新加载。

四、可用命令

BOM_PARAMS    设置/查看项目参数
BOM_ADD_RULE  添加构件规则
BOM_RULES     管理构件规则
BOM_STAT      统计选中的图块并计算 BOM
BOM_EXPORT    导出 CSV
BOM_CLEAR     清除统计结果
BOM_ABOUT     关于

五、卸载

删除对应的 .bundle 文件夹并重启 AutoCAD。
