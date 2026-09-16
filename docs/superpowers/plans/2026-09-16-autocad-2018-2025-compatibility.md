# AutoCAD 2018–2025 Compatibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the BOM plugin from one source tree and publish validated bundles for AutoCAD 2018–2024 and AutoCAD 2025.

**Architecture:** Multi-target the shared core for `net48` and `net8.0`, and select the plugin target framework and Autodesk reference set through an `AutoCADVersion` MSBuild property. A PowerShell release script builds the four compatibility rows, stages each output, creates two bundles from source-controlled manifests, and runs a package validator before reporting success.

**Tech Stack:** C# 12, SDK-style MSBuild, .NET Framework 4.8, .NET 8, System.Text.Json, xUnit, PowerShell, AutoCAD managed assemblies.

---

### Task 1: Make the shared core compile for .NET Framework 4.8

**Files:**
- Modify: `src/BomCadPlugin.Core/BomCadPlugin.Core.csproj`
- Modify: `src/BomCadPlugin.Core/Services/StatisticsService.cs`
- Modify: `src/BomCadPlugin.Core/Services/SystemParameterKeySuggestionService.cs`
- Test: `tests/BomCadPlugin.Core.Tests/BomCadPlugin.Core.Tests.csproj`

- [ ] **Step 1: Change the core project to multi-target and run the `net48` build to expose incompatibilities**

```xml
<TargetFrameworks>net48;net8.0</TargetFrameworks>
<LangVersion>12.0</LangVersion>
```

Run: `dotnet build .\src\BomCadPlugin.Core\BomCadPlugin.Core.csproj -c Release -f net48`

Expected: FAIL on APIs or generated types unavailable to `net48` before compatibility changes are applied.

- [ ] **Step 2: Add the legacy-only dependencies**

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net48'">
  <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
  <PackageReference Include="System.Text.Json" Version="8.0.5" />
</ItemGroup>
```

- [ ] **Step 3: Replace APIs that require newer runtime types without changing behavior**

```csharp
// string.Contains(string, StringComparison) -> IndexOf(...) >= 0
// value[start..] -> value.Substring(start)
// private records -> small sealed classes
```

- [ ] **Step 4: Build both core targets and run the existing test suite**

Run: `dotnet build .\src\BomCadPlugin.Core\BomCadPlugin.Core.csproj -c Release -f net48`

Expected: PASS with zero errors.

Run: `dotnet test .\tests\BomCadPlugin.Core.Tests\BomCadPlugin.Core.Tests.csproj -c Release`

Expected: all 42 existing tests pass on `net8.0`.

### Task 2: Parameterize the AutoCAD host project

**Files:**
- Modify: `src/BomCadPlugin/BomCadPlugin.csproj`
- Modify: `src/BomCadPlugin/Commands/BomCommands.cs`
- Modify: `src/BomCadPlugin/UI/AddComponentRuleForm.cs`
- Modify: `src/BomCadPlugin/UI/ComponentRuleManagerForm.cs`

- [ ] **Step 1: Add explicit build matrix properties with validation**

```xml
<AutoCADVersion Condition="'$(AutoCADVersion)' == ''">2025</AutoCADVersion>
<TargetFramework Condition="'$(AutoCADVersion)' == '2025'">net8.0-windows</TargetFramework>
<TargetFramework Condition="'$(AutoCADVersion)' != '2025'">net48</TargetFramework>
<AutoCADReferencePath Condition="'$(AutoCADReferencePath)' == '' and '$(AutoCADVersion)' == '2025'">C:\1 Software\cad2025\AutoCAD 2025</AutoCADReferencePath>
```

Add an MSBuild target that rejects values other than `2018`, `2019`, `2021`, and `2025`, and reports a missing Autodesk DLL with its resolved path.

- [ ] **Step 2: Run the 2018 target and confirm the missing SDK failure is actionable**

Run: `dotnet build .\src\BomCadPlugin\BomCadPlugin.csproj -c Release -p:AutoCADVersion=2018 -p:AutoCADReferencePath=C:\missing`

Expected: FAIL with a message naming AutoCAD 2018 and the missing reference directory or DLL.

- [ ] **Step 3: Replace host-side collection expressions and spread expressions that rely on modern runtime support**

```csharp
_unit.Items.AddRange(new object[] { "个", "根", "套", "块", "件", "米", "m" });
TemplateHeightsM = new List<decimal>(project.TemplateHeightsM);
```

- [ ] **Step 4: Compile the 2025 row against the installed API**

Run: `dotnet build .\src\BomCadPlugin\BomCadPlugin.csproj -c Release -p:AutoCADVersion=2025 -p:AutoCADReferencePath="C:\1 Software\cad2025\AutoCAD 2025"`

Expected: PASS with zero errors and without copying `AcMgd.dll`, `AcDbMgd.dll`, `AcCoreMgd.dll`, or `AdWindows.dll`.

### Task 3: Define source-controlled bundle manifests

**Files:**
- Create: `packaging/BomCadPlugin-2018-2024.bundle/PackageContents.xml`
- Create: `packaging/BomCadPlugin-2025.bundle/PackageContents.xml`
- Create: `packaging/README.txt`

- [ ] **Step 1: Write the legacy bundle manifest with non-overlapping runtime rows**

```xml
<ComponentEntry ModuleName="./Contents/Windows/2018/BomCadPlugin.dll">
  <RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R22.0" SeriesMax="R22.0" />
</ComponentEntry>
<ComponentEntry ModuleName="./Contents/Windows/2019-2020/BomCadPlugin.dll">
  <RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R23.0" SeriesMax="R23.1" />
</ComponentEntry>
<ComponentEntry ModuleName="./Contents/Windows/2021-2024/BomCadPlugin.dll">
  <RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R24.0" SeriesMax="R24.3" />
</ComponentEntry>
```

Each component repeats the seven `BOM_*` command declarations and points at exactly one versioned module.

- [ ] **Step 2: Write the AutoCAD 2025-only manifest**

```xml
<RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R25.0" SeriesMax="R25.0" />
```

- [ ] **Step 3: Document installation and prerequisites**

Document `.bundle` deployment under `%APPDATA%\Autodesk\ApplicationPlugins`, the `NETLOAD` fallback, the .NET Framework 4.8 prerequisite for AutoCAD 2018–2024, and the fact that users choose only the bundle matching their AutoCAD generation.

### Task 4: Add the package validation test first

**Files:**
- Create: `scripts/Test-Packages.ps1`

- [ ] **Step 1: Implement validation assertions against an expected `dist` layout**

The script parses both manifests and fails unless:

```text
legacy ranges = R22.0-R22.0, R23.0-R23.1, R24.0-R24.3
2025 range = R25.0-R25.0
every ModuleName resolves to an existing file
no Autodesk host DLL exists anywhere under dist
exactly two top-level *.bundle directories exist
```

- [ ] **Step 2: Run the validator before packaging and verify the red state**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Packages.ps1 -DistPath .\dist`

Expected: FAIL because `dist` has not been created.

### Task 5: Build and package all four compatibility rows

**Files:**
- Create: `scripts/Build-Release.ps1`
- Modify: `.gitignore`

- [ ] **Step 1: Accept or discover four matching Autodesk reference directories**

```powershell
param(
    [string] $AutoCAD2018ReferencePath,
    [string] $AutoCAD2019ReferencePath,
    [string] $AutoCAD2021ReferencePath,
    [string] $AutoCAD2025ReferencePath
)
```

Default discovery uses `C:\Program Files\Autodesk\AutoCAD <year>`; the existing custom AutoCAD 2025 directory is accepted only as the 2025 row. Missing rows terminate with an error naming the expected version and property.

- [ ] **Step 2: Build into isolated staging directories**

For each of `2018`, `2019`, `2021`, and `2025`, execute:

```powershell
dotnet build .\src\BomCadPlugin\BomCadPlugin.csproj -c Release --no-incremental `
  -p:AutoCADVersion=$version `
  -p:AutoCADReferencePath=$referencePath `
  -p:OutputPath=$outputPath
```

Fail immediately on a non-zero exit code or a missing `BomCadPlugin.dll` / `BomCadPlugin.Core.dll`.

- [ ] **Step 3: Stage two clean bundles and exclude Autodesk assemblies**

Copy each build output into its manifest-selected directory while excluding:

```text
AcMgd.dll
AcDbMgd.dll
AcCoreMgd.dll
AdWindows.dll
```

- [ ] **Step 4: Run the package validator as the final build gate**

Run: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Packages.ps1 -DistPath .\dist`

Expected: PASS after all four rows have been built and staged.

- [ ] **Step 5: Ignore generated staging and distribution output**

```gitignore
artifacts/
dist/
```

### Task 6: Update developer and user documentation

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Replace the 2025-only build section with the supported matrix**

Document the four SDK rows, both target frameworks, build-script invocation, output bundle names, and that Autodesk managed DLLs are not redistributed.

- [ ] **Step 2: Document verification limits**

State that successful compilation verifies managed compatibility, while final acceptance still requires loading and smoke-testing the commands in real AutoCAD 2018, 2019/2020, 2021–2024, and 2025 hosts.

### Task 7: Final verification

**Files:**
- Verify only

- [ ] **Step 1: Run all available automated checks**

Run:

```powershell
dotnet restore .\BOMlist.sln
dotnet test .\tests\BomCadPlugin.Core.Tests\BomCadPlugin.Core.Tests.csproj -c Release
dotnet build .\src\BomCadPlugin.Core\BomCadPlugin.Core.csproj -c Release -f net48
dotnet build .\src\BomCadPlugin.Core\BomCadPlugin.Core.csproj -c Release -f net8.0
dotnet build .\src\BomCadPlugin\BomCadPlugin.csproj -c Release -p:AutoCADVersion=2025 -p:AutoCADReferencePath="C:\1 Software\cad2025\AutoCAD 2025"
```

Expected: all available checks pass with zero errors.

- [ ] **Step 2: Exercise the missing-SDK and package-validator failure paths**

Run the 2018 build with `C:\missing` and validate a nonexistent distribution directory. Both commands must return non-zero with actionable messages.

- [ ] **Step 3: Review the final diff against the design**

Confirm no duplicated business source, no committed Autodesk binaries, exactly two generated bundle names, and no change to BOM formulas, configuration shape, commands, Ribbon layout, or UI behavior.
