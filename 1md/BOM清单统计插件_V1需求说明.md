# BOM清单统计 CAD插件 V1 需求说明

> 用途：把这个 Markdown 文件交给 Codex，让它根据本文档生成第一版 CAD 插件。  
> 插件名称统一为：**BOM清单统计**。  
> 第一版目标：**先不做自动设计，只做项目参数设置 + 图块规则绑定 + 框选平面统计 + BOM清单输出。**

---

## 1. 项目背景

现在设计人员在 CAD 里完成平面方案后，需要人工统计图块数量，再根据项目高度、模板高度、构件竖向布置排数等规则，换算成 BOM 清单数量。

第一版插件的目标是：

```text
打开 CAD 文件
↓
设置项目参数：层高、模板高度、墙厚
↓
用户正常完成平面设计方案
↓
添加构件规则：把 CAD 图块绑定到构件，并定义竖向排数
↓
框选平面方案区域
↓
自动统计区域内已绑定图块的数量
↓
按照规则换算最终 BOM 数量
↓
显示 BOM 清单，可导出
```

第一版重点解决：

```text
BOM数量 = 框选区域内图块数量 × 竖向排数 × 单图块基础数量
```

第一版暂时不处理：

```text
1. 不做自动设计
2. 不做自动布置图块
3. 不做混凝土周长统计
4. 不做围绕混凝土四周一圈、两圈的构件计算
5. 不做复杂公式编辑器
6. 不做 AI 识图
```

---

## 2. 插件整体定位

插件形式：AutoCAD 顶部工具栏 / Ribbon 插件。

插件名称：

```text
BOM清单统计
```

建议顶部工具栏按钮：

```text
[BOM清单统计]
    [项目参数]
    [添加构件]
    [构件管理]
    [平面统计]
    [清除统计]
    [导出BOM]
    [关于]
```

其中第一版必须完成的核心按钮：

```text
1. 项目参数
2. 添加构件
3. 构件管理
4. 平面统计
5. 导出BOM
```

---

## 3. 第一版用户流程

### 3.1 第一步：设置项目参数

用户打开 CAD 文件后，点击：

```text
BOM清单统计 → 项目参数
```

弹出窗口：

```text
项目参数设置

项目名称：__________
层高 mm：__________
模板高度 mm：__________
墙厚 mm：__________
备注：__________

[保存] [取消]
```

字段说明：

| 字段 | 类型 | 是否必填 | 说明 |
|---|---:|---:|---|
| projectName | string | 否 | 项目名称 |
| floorHeightMm | number | 是 | 竖向层高，单位 mm |
| templateHeightMm | number | 是 | 模板高度，单位 mm |
| wallThicknessMm | number | 是 | 墙厚，单位 mm |
| note | string | 否 | 备注 |

保存后，项目参数要与当前 DWG 文件绑定。  
如果第一版来不及做 DWG 内部存储，可以先用当前 DWG 同目录下的 JSON 文件保存。

推荐 JSON 文件名：

```text
当前DWG文件名.bomconfig.json
```

例如：

```text
A项目结构平面图.dwg
A项目结构平面图.bomconfig.json
```

---

### 3.2 第二步：用户正常设计方案

这一部分插件不干预。

用户照常在 CAD 里画平面方案、插入图块、调整位置。

插件只需要在后续统计时读取图纸里的图块对象。

---

### 3.3 第三步：添加构件并绑定图块规则

用户点击：

```text
BOM清单统计 → 添加构件
```

弹出窗口：

```text
添加构件

1. 选择图块
图块名：__________    [从图中选择图块]

2. 构件信息
构件名称：__________
单位：下拉选择，默认「个」
单图块基础数量：默认 1
竖向排数：__________
备注：__________

[确定] [取消]
```

添加构件的核心含义：

```text
某个 CAD 图块 = 某个构件
这个构件在竖向需要布置几排
统计时最终数量 = 平面图块数量 × 竖向排数 × 单图块基础数量
```

例如：

| 图块名 | 构件名称 | 单位 | 单图块基础数量 | 竖向排数 |
|---|---|---:|---:|---:|
| BLK_JG01 | 竖向背楞 | 根 | 1 | 5 |
| BLK_JG02 | 斜撑 | 根 | 1 | 3 |
| BLK_JG03 | U型卡 | 个 | 1 | 2 |

---

### 3.4 第四步：构件管理

用户点击：

```text
BOM清单统计 → 构件管理
```

弹出窗口，显示已添加的构件规则：

```text
构件管理

[添加构件] [编辑] [删除] [导入规则] [导出规则]

| 序号 | 图块名 | 构件名称 | 单位 | 单图块基础数量 | 竖向排数 | 备注 |
|---|---|---|---|---|---|---|
| 1 | BLK_JG01 | 竖向背楞 | 根 | 1 | 5 | |
| 2 | BLK_JG02 | 斜撑 | 根 | 1 | 3 | |
| 3 | BLK_JG03 | U型卡 | 个 | 1 | 2 | |
```

第一版必须支持：

```text
1. 新增规则
2. 编辑规则
3. 删除规则
4. 保存规则
5. 重新打开文件后能读取规则
```

导入规则、导出规则可以作为第一版可选功能。

---

### 3.5 第五步：框选平面方案并统计

用户点击：

```text
BOM清单统计 → 平面统计
```

CAD 命令行提示：

```text
请选择需要统计的平面方案区域：
```

用户框选一块区域后，插件执行：

```text
1. 获取框选范围内的所有对象
2. 过滤出图块对象 BlockReference
3. 读取每个图块的图块名
4. 判断该图块是否在构件规则表中
5. 如果已绑定规则，则计入统计
6. 如果未绑定规则，则忽略，不报错
7. 计算每个构件的平面数量
8. 根据竖向排数换算 BOM 数量
9. 显示统计结果窗口
```

统计公式：

```text
平面数量 = 框选区域内该图块出现次数
最终数量 = 平面数量 × 竖向排数 × 单图块基础数量
```

例如：

| 构件名称 | 图块名 | 单位 | 平面数量 | 单图块基础数量 | 竖向排数 | 最终数量 |
|---|---|---:|---:|---:|---:|---:|
| 竖向背楞 | BLK_JG01 | 根 | 28 | 1 | 5 | 140 |
| 斜撑 | BLK_JG02 | 根 | 15 | 1 | 3 | 45 |
| U型卡 | BLK_JG03 | 个 | 32 | 1 | 2 | 64 |

---

### 3.6 第六步：查看和导出 BOM 清单

统计完成后，弹出窗口：

```text
平面统计结果 / BOM清单

统计区域：已选择区域
统计时间：2026-xx-xx xx:xx:xx

| 序号 | 构件名称 | 图块名 | 单位 | 平面数量 | 单图块基础数量 | 竖向排数 | 总数量 | 备注 |
|---|---|---|---|---|---|---|---|---|

合计：x 项构件

[生成CAD表格] [导出Excel/CSV] [关闭]
```

第一版建议先实现：

```text
1. 在窗口中显示统计结果
2. 导出 CSV 文件
```

如果时间够，再实现：

```text
1. 在 CAD 模型空间插入表格
2. 导出 xlsx 文件
```

---

## 4. 数据结构设计

### 4.1 项目参数数据结构

```json
{
  "projectName": "XX项目",
  "floorHeightMm": 3000,
  "templateHeightMm": 1500,
  "wallThicknessMm": 200,
  "note": "",
  "updatedAt": "2026-06-12T10:30:00"
}
```

---

### 4.2 构件规则数据结构

```json
{
  "rules": [
    {
      "id": "rule-001",
      "blockName": "BLK_JG01",
      "componentName": "竖向背楞",
      "unit": "根",
      "baseQtyPerBlock": 1,
      "verticalRows": 5,
      "note": ""
    },
    {
      "id": "rule-002",
      "blockName": "BLK_JG02",
      "componentName": "斜撑",
      "unit": "根",
      "baseQtyPerBlock": 1,
      "verticalRows": 3,
      "note": ""
    }
  ]
}
```

---

### 4.3 完整配置文件结构

```json
{
  "version": "1.0.0",
  "project": {
    "projectName": "XX项目",
    "floorHeightMm": 3000,
    "templateHeightMm": 1500,
    "wallThicknessMm": 200,
    "note": "",
    "updatedAt": "2026-06-12T10:30:00"
  },
  "rules": [
    {
      "id": "rule-001",
      "blockName": "BLK_JG01",
      "componentName": "竖向背楞",
      "unit": "根",
      "baseQtyPerBlock": 1,
      "verticalRows": 5,
      "note": ""
    }
  ]
}
```

---

### 4.4 统计结果数据结构

```json
{
  "statTime": "2026-06-12T10:35:00",
  "items": [
    {
      "componentName": "竖向背楞",
      "blockName": "BLK_JG01",
      "unit": "根",
      "planeCount": 28,
      "baseQtyPerBlock": 1,
      "verticalRows": 5,
      "totalQty": 140,
      "note": ""
    }
  ]
}
```

---

## 5. 第一版计算逻辑

### 5.1 基础公式

```text
totalQty = planeCount × baseQtyPerBlock × verticalRows
```

含义：

```text
planeCount：框选区域内该图块出现次数
baseQtyPerBlock：一个图块代表几个构件，默认 1
verticalRows：竖向排数，由用户在构件规则里定义
```

---

### 5.2 高度参数的作用

第一版先把高度参数作为项目基础信息保存。

后续升级时，可以支持自动计算竖向排数：

```text
verticalRows = ceil(floorHeightMm / templateHeightMm)
```

但是第一版为了稳定落地，建议先让用户在构件规则里手动填写竖向排数。

原因：

```text
1. 不同构件不一定都按层高 ÷ 模板高度计算
2. 有些构件可能固定 2 排或 3 排
3. 有些构件可能有特殊布置逻辑
4. 第一版先保证统计准确、流程跑通
```

---

## 6. 推荐技术方案

### 6.1 开发语言和插件形式

推荐使用：

```text
C# + AutoCAD .NET API
```

原因：

```text
1. 方便做 AutoCAD 插件命令
2. 方便做 Ribbon 顶部工具栏
3. 方便做 WinForms / WPF 参数窗口
4. 后续方便扩展导出 Excel、写入 CAD 表格、保存 DWG 内部数据
```

---

### 6.2 项目结构建议

```text
BomCadPlugin/
├─ BomCadPlugin.csproj
├─ Commands/
│  ├─ BomCommands.cs
│  ├─ ProjectParamCommand.cs
│  ├─ ComponentRuleCommand.cs
│  └─ PlanStatisticCommand.cs
├─ Models/
│  ├─ ProjectParams.cs
│  ├─ ComponentRule.cs
│  ├─ BomConfig.cs
│  └─ BomStatItem.cs
├─ Services/
│  ├─ ConfigService.cs
│  ├─ BlockSelectionService.cs
│  ├─ StatisticsService.cs
│  └─ ExportService.cs
├─ UI/
│  ├─ ProjectParamsForm.cs
│  ├─ AddComponentRuleForm.cs
│  ├─ ComponentRuleManagerForm.cs
│  └─ StatisticResultForm.cs
├─ Ribbon/
│  └─ BomRibbon.cs
└─ Utils/
   └─ CadUtils.cs
```

---

### 6.3 AutoCAD 命令建议

每个按钮对应一个 CAD 命令：

| 命令 | 功能 |
|---|---|
| BOM_PARAMS | 打开项目参数设置窗口 |
| BOM_ADD_RULE | 添加构件规则 |
| BOM_RULES | 打开构件管理窗口 |
| BOM_STAT | 框选平面并统计 |
| BOM_EXPORT | 导出 BOM 清单 |
| BOM_CLEAR | 清除当前统计结果，可选 |
| BOM_ABOUT | 关于插件，可选 |

---

## 7. 关键开发逻辑

### 7.1 读取当前 DWG 文件路径

用途：决定配置文件保存在哪里。

伪代码：

```csharp
var doc = Application.DocumentManager.MdiActiveDocument;
var db = doc.Database;
var dwgPath = db.Filename;
var configPath = Path.ChangeExtension(dwgPath, ".bomconfig.json");
```

注意：

```text
如果当前图纸还没保存，dwgPath 可能为空。
这种情况下需要提示用户：请先保存当前 DWG 文件，再使用 BOM清单统计。
```

---

### 7.2 从图中选择一个图块并读取图块名

添加构件时，用户点击：

```text
[从图中选择图块]
```

插件让用户点选一个对象。

要求：

```text
1. 如果选中的是 BlockReference，则读取图块名
2. 如果不是图块，则提示“请选择图块对象”
3. 动态块要尽量读取真实有效名称
```

伪代码：

```csharp
PromptEntityOptions options = new PromptEntityOptions("\n请选择一个图块：");
options.SetRejectMessage("\n请选择图块对象。");
options.AddAllowedClass(typeof(BlockReference), true);

PromptEntityResult result = editor.GetEntity(options);
if (result.Status != PromptStatus.OK) return;

using (Transaction tr = db.TransactionManager.StartTransaction())
{
    var br = tr.GetObject(result.ObjectId, OpenMode.ForRead) as BlockReference;
    string blockName = GetEffectiveBlockName(br, tr);
    tr.Commit();
}
```

---

### 7.3 框选区域内的图块

用户点击平面统计后，插件提示用户选择统计区域。

建议使用选择过滤器，只选图块：

```text
DXF 0 = INSERT
```

伪代码：

```csharp
TypedValue[] filterValues = new TypedValue[]
{
    new TypedValue((int)DxfCode.Start, "INSERT")
};
SelectionFilter filter = new SelectionFilter(filterValues);

PromptSelectionOptions options = new PromptSelectionOptions();
options.MessageForAdding = "\n请选择需要统计的平面方案区域：";

PromptSelectionResult result = editor.GetSelection(options, filter);
if (result.Status != PromptStatus.OK) return;
```

---

### 7.4 统计已绑定规则的图块数量

伪代码：

```csharp
Dictionary<string, int> blockCounts = new Dictionary<string, int>();

foreach (SelectedObject selected in result.Value)
{
    var br = tr.GetObject(selected.ObjectId, OpenMode.ForRead) as BlockReference;
    if (br == null) continue;

    string blockName = GetEffectiveBlockName(br, tr);

    if (!rules.Any(r => r.BlockName == blockName))
    {
        continue;
    }

    if (!blockCounts.ContainsKey(blockName))
    {
        blockCounts[blockName] = 0;
    }

    blockCounts[blockName]++;
}
```

---

### 7.5 生成 BOM 统计结果

伪代码：

```csharp
List<BomStatItem> items = new List<BomStatItem>();

foreach (var rule in rules)
{
    int planeCount = blockCounts.ContainsKey(rule.BlockName)
        ? blockCounts[rule.BlockName]
        : 0;

    if (planeCount == 0) continue;

    decimal totalQty = planeCount * rule.BaseQtyPerBlock * rule.VerticalRows;

    items.Add(new BomStatItem
    {
        ComponentName = rule.ComponentName,
        BlockName = rule.BlockName,
        Unit = rule.Unit,
        PlaneCount = planeCount,
        BaseQtyPerBlock = rule.BaseQtyPerBlock,
        VerticalRows = rule.VerticalRows,
        TotalQty = totalQty,
        Note = rule.Note
    });
}
```

---

## 8. UI 原型说明

### 8.1 顶部 Ribbon

```text
┌──────────────────────────────────────────────────────────────┐
│ BOM清单统计                                                   │
├──────────┬──────────┬──────────┬──────────┬──────────┬──────┤
│ 项目参数 │ 添加构件 │ 构件管理 │ 平面统计 │ 导出BOM  │ 关于 │
└──────────┴──────────┴──────────┴──────────┴──────────┴──────┘
```

---

### 8.2 项目参数窗口

```text
┌──────────────────────────────┐
│ 项目参数设置                  │
├──────────────────────────────┤
│ 项目名称： [              ]   │
│ 层高 mm： [              ]   │
│ 模板高度 mm：[           ]   │
│ 墙厚 mm： [              ]   │
│ 备注：                       │
│ [                         ]  │
│                              │
│        [保存]   [取消]       │
└──────────────────────────────┘
```

---

### 8.3 添加构件窗口

```text
┌──────────────────────────────┐
│ 添加构件                      │
├──────────────────────────────┤
│ 图块名： [              ] [选择图块] │
│ 构件名称：[              ]     │
│ 单位：   [个/根/套/块]         │
│ 单图块基础数量：[ 1 ]          │
│ 竖向排数：[        ]           │
│ 备注：                        │
│ [                          ]  │
│                               │
│        [确定]   [取消]        │
└──────────────────────────────┘
```

---

### 8.4 构件管理窗口

```text
┌──────────────────────────────────────────────────────────────┐
│ 构件管理                                                       │
├──────────────────────────────────────────────────────────────┤
│ [添加构件] [编辑] [删除] [导入规则] [导出规则]                  │
├────┬──────────┬──────────┬────┬────────────┬────────┬──────┤
│序号│ 图块名   │ 构件名称 │单位│单图块数量  │竖向排数│备注  │
├────┼──────────┼──────────┼────┼────────────┼────────┼──────┤
│ 1  │ BLK_JG01 │ 竖向背楞 │根  │ 1          │ 5      │      │
│ 2  │ BLK_JG02 │ 斜撑     │根  │ 1          │ 3      │      │
└────┴──────────┴──────────┴────┴────────────┴────────┴──────┘
```

---

### 8.5 统计结果窗口

```text
┌──────────────────────────────────────────────────────────────┐
│ 平面统计结果 / BOM清单                                        │
├──────────────────────────────────────────────────────────────┤
│ 统计区域：已选择区域       统计时间：2026-xx-xx xx:xx:xx       │
├────┬──────────┬──────────┬────┬────────┬────────┬──────┬────┤
│序号│ 构件名称 │ 图块名   │单位│平面数量│竖向排数│总数量│备注│
├────┼──────────┼──────────┼────┼────────┼────────┼──────┼────┤
│ 1  │ 竖向背楞 │ BLK_JG01 │根  │ 28     │ 5      │ 140  │    │
│ 2  │ 斜撑     │ BLK_JG02 │根  │ 15     │ 3      │ 45   │    │
└────┴──────────┴──────────┴────┴────────┴────────┴──────┴────┘
│ 合计：2 项构件                                                 │
│                              [导出CSV] [生成CAD表格] [关闭]    │
└──────────────────────────────────────────────────────────────┘
```

---

## 9. 第一版验收标准

### 9.1 项目参数

必须满足：

```text
1. 点击项目参数按钮能打开窗口
2. 能输入层高、模板高度、墙厚
3. 点击保存后能保存
4. 关闭窗口再打开，数据仍存在
5. 关闭 CAD 文件后重新打开，数据仍能读取
```

---

### 9.2 构件规则

必须满足：

```text
1. 能从 CAD 图中选择一个图块并读取图块名
2. 能输入构件名称、单位、竖向排数
3. 能保存规则
4. 能编辑规则
5. 能删除规则
6. 同一个图块名不允许重复绑定多个构件，除非用户确认覆盖
```

---

### 9.3 平面统计

必须满足：

```text
1. 点击平面统计后能框选图纸区域
2. 只统计已绑定规则的图块
3. 未绑定规则的图块自动忽略
4. 同一种图块能正确累计数量
5. 能按公式计算总数量
6. 能显示统计结果表格
```

---

### 9.4 导出 BOM

第一版最低要求：

```text
1. 能导出 CSV
2. CSV 字段包含：序号、构件名称、图块名、单位、平面数量、单图块基础数量、竖向排数、总数量、备注
3. 中文不乱码，建议 UTF-8 with BOM
```

---

## 10. 测试用例

### 测试用例 1：基础统计

前置条件：

```text
项目参数：
层高 = 3000 mm
模板高度 = 1500 mm
墙厚 = 200 mm

构件规则：
BLK_JG01 → 竖向背楞，单位 根，单图块基础数量 1，竖向排数 5
BLK_JG02 → 斜撑，单位 根，单图块基础数量 1，竖向排数 3
```

CAD 图中框选区域内：

```text
BLK_JG01 出现 10 次
BLK_JG02 出现 8 次
其他未绑定图块出现 20 次
```

期望结果：

```text
竖向背楞：10 × 1 × 5 = 50 根
斜撑：8 × 1 × 3 = 24 根
未绑定图块不出现在 BOM 清单中
```

---

### 测试用例 2：单图块基础数量不为 1

构件规则：

```text
BLK_JG03 → U型卡，单位 个，单图块基础数量 2，竖向排数 4
```

框选区域内：

```text
BLK_JG03 出现 6 次
```

期望结果：

```text
U型卡：6 × 2 × 4 = 48 个
```

---

### 测试用例 3：没有设置规则

框选区域内有很多图块，但没有任何图块被添加到规则表。

期望结果：

```text
提示：当前区域内没有找到已绑定规则的图块，请先添加构件规则。
```

---

### 测试用例 4：图纸未保存

如果当前 DWG 文件没有保存，无法确定 JSON 配置路径。

期望结果：

```text
提示：请先保存当前 DWG 文件，再使用 BOM清单统计。
```

---

## 11. 后续版本规划，但第一版不要做

### V2：根据高度自动计算竖向排数

支持规则类型：

```text
1. 手动竖向排数
2. 按层高 / 模板高度自动计算
3. 自定义公式
```

---

### V3：混凝土周长构件

支持：

```text
1. 输入混凝土长宽
2. 识别闭合多段线
3. 计算周长
4. 按间距和圈数计算构件数量
```

---

### V4：与线上报价系统对接

支持：

```text
1. BOM 清单导出 JSON
2. 上传到报价系统
3. 自动生成报价单
4. 自动匹配库存和价格
```

---

## 12. 给 Codex 的开发任务说明

请基于本文档开发一个 AutoCAD C# .NET 插件，插件名称为 **BOM清单统计**。

第一版请优先完成：

```text
1. 创建 AutoCAD 插件项目
2. 添加 Ribbon 顶部工具栏，名称为 BOM清单统计
3. 实现项目参数设置窗口
4. 实现构件规则添加和管理窗口
5. 实现从图中选择图块并读取图块名
6. 实现框选区域统计已绑定图块数量
7. 实现 BOM 统计结果窗口
8. 实现导出 CSV
9. 配置数据先保存为 DWG 同目录 JSON 文件
```

请先不要实现：

```text
1. 混凝土周长统计
2. 自动设计
3. 自动布置图块
4. AI 识别
5. 复杂公式编辑器
```

开发时请保证代码结构清晰，核心统计逻辑与 UI 分离，方便后续扩展。

---

## 13. 一句话总结

第一版插件的本质是：

```text
用户先设置项目参数，再把 CAD 图块绑定成构件规则；平面设计完成后，框选方案区域，插件自动统计已绑定图块数量，并按竖向排数换算成 BOM 清单。
```
