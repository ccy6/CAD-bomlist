# AutoCAD 2018–2025 Compatibility Design

## Goal

Build the existing BOM plugin from one shared source tree and publish two user-facing AutoCAD Application Plug-in bundles:

- `BomCadPlugin-2018-2024.bundle`
- `BomCadPlugin-2025.bundle`

The legacy bundle contains version-specific managed assemblies selected automatically by AutoCAD. Users copy only the bundle that matches their AutoCAD generation; they do not work with `src`, `bin`, or `obj`.

## Supported Build Matrix

| Published range | Target framework | AutoCAD managed references | Bundle runtime series |
| --- | --- | --- | --- |
| AutoCAD 2018 | .NET Framework 4.8 | AutoCAD 2018 | R22.0 |
| AutoCAD 2019–2020 | .NET Framework 4.8 | AutoCAD 2019 | R23.0–R23.1 |
| AutoCAD 2021–2024 | .NET Framework 4.8 | AutoCAD 2021 | R24.0–R24.3 |
| AutoCAD 2025 | .NET 8 (`net8.0-windows`) | AutoCAD 2025 | R25.0 |

The SDK grouping follows Autodesk's published managed compatibility matrix: 2020 supports the 2019 SDK, and 2021–2024 support the 2021 SDK. AutoCAD 2025 is isolated because it changed from .NET Framework to .NET 8.

AutoCAD 2018 originally shipped against .NET Framework 4.6. The legacy package will require .NET Framework 4.8 to be installed. .NET Framework 4.8 is an in-place CLR 4 update and lets the shared core retain its current JSON behavior without introducing a second serializer. This prerequisite must be stated in the release README.

## Project Structure

No business source files are duplicated. The existing structure remains authoritative:

```text
src/
  BomCadPlugin.Core/
  BomCadPlugin/
tests/
  BomCadPlugin.Core.Tests/
```

The core project multi-targets `net48` and `net8.0`. The AutoCAD host project selects `net48` for releases 2018–2024 and `net8.0-windows` for release 2025. AutoCAD reference paths are supplied as build properties, so no Autodesk DLL is committed to Git.

For `net48`, `System.Text.Json` is supplied as an explicit NuGet dependency and all required runtime dependencies are copied into the bundle. The .NET 8 target continues to use the framework-provided implementation. Configuration file names and JSON shape remain unchanged across all AutoCAD versions.

## Build and Packaging

A repository PowerShell build script accepts the four managed-reference directories or discovers standard AutoCAD installation paths. It performs four Release builds from the same source and stages their outputs under `artifacts` before creating the bundles under `dist`.

```text
dist/
  BomCadPlugin-2018-2024.bundle/
    PackageContents.xml
    Contents/Windows/2018/
    Contents/Windows/2019-2020/
    Contents/Windows/2021-2024/
  BomCadPlugin-2025.bundle/
    PackageContents.xml
    Contents/Windows/2025/
```

Each legacy `PackageContents.xml` component entry has non-overlapping `SeriesMin` and `SeriesMax` values, so AutoCAD loads exactly one `BomCadPlugin.dll`. The 2025 bundle contains only the .NET 8 build. Autodesk reference assemblies (`AcMgd.dll`, `AcDbMgd.dll`, `AcCoreMgd.dll`, and `AdWindows.dll`) remain `Private=false` and are never copied into a bundle.

Build output directories (`bin`, `obj`, `artifacts`, and `dist`) are generated files and remain outside source control. A short deployment document explains copying a `.bundle` directory into `%APPDATA%\Autodesk\ApplicationPlugins` and the `NETLOAD` fallback for development.

## Compatibility Changes

The implementation will first compile the core and plugin against `net48` to expose incompatible BCL or C# constructs. Only compatibility-required edits will be made. Expected changes include replacing syntax or APIs that require newer runtime types while preserving current behavior. Conditional compilation is limited to genuine runtime/API differences; business rules and UI behavior must stay shared.

The AutoCAD API surface currently used—commands, document/editor/database access, block references, tables, WinForms, and `Autodesk.Windows` Ribbon types—exists in the selected SDK generations. Any signature difference found during the four builds will be isolated behind the smallest possible adapter or conditional block.

## Error Handling

The packaging script fails with an actionable message when a required AutoCAD reference directory or DLL is missing. It must not silently reuse the wrong SDK. It also fails if a build output or required dependency is absent, or if generated runtime ranges overlap.

At runtime, existing command-level behavior remains unchanged. Bundle selection is handled by AutoCAD before plugin initialization, so no runtime version switch is added to application code.

## Verification

Automated verification consists of:

1. Existing core unit tests running against the normal .NET 8 test target.
2. Successful Release compilation of the core for both `net48` and `net8.0`.
3. Successful plugin compilation for all four build matrix rows using the matching Autodesk references.
4. A packaging validation test that parses both `PackageContents.xml` files, verifies the expected runtime ranges, ensures every referenced module exists, and ensures Autodesk host DLLs are not packaged.
5. Inspection of `dist` to confirm only the two user-facing bundles are produced.

Final acceptance requires smoke testing each output in real AutoCAD 2018, 2019 or 2020, 2021 or newer through 2024, and 2025: load the bundle, confirm the Ribbon appears, run `BOM_PARAMS`, select blocks with `BOM_STAT`, write a CAD table, and export CSV. Local compilation alone cannot prove host compatibility when those AutoCAD versions are not installed.

## Non-Goals

- Supporting AutoCAD 2017 or earlier, AutoCAD 2026 or later, AutoCAD LT, or non-Windows hosts.
- Maintaining separate copies of business source code for each AutoCAD version.
- Committing Autodesk SDK binaries or generated bundles to Git.
- Changing BOM formulas, configuration semantics, commands, Ribbon layout, or UI behavior.

## References

- [Autodesk AutoCAD 2018 managed .NET compatibility](https://help.autodesk.com/cloudhelp/2018/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm)
- [Autodesk AutoCAD 2024 application compatibility matrix](https://help.autodesk.com/cloudhelp/2024/ENU/AutoCAD-Customization/files/GUID-D54B0935-1638-4F97-8B37-1EC3635A1E71.htm)
