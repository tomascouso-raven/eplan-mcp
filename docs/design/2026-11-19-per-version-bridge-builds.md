# Design note: one Bridge build per EPLAN version (2025 and 2026)

| Item | Content |
|---|---|
| Status | Proposed. Plan for branch `build/bridge-eplan-2025-2026` of the fork `tomascouso-raven/eplan-mcp`; not yet agreed with the maintainer |
| Origin | Issue [#664](https://github.com/asckye/eplan-mcp/issues/664): `MissingMethodException` for `PathMap.SubstitutePath(String, String)` on EPLAN 2025.0.3 |
| Base | `main` at `6d14f7ec` (fixes for both problems of #664) |
| Related | [ADR-0009](../adr/0009-supported-eplan-versions.md), [catalog README](../../catalog/README.md), [eng/coverage README](../../eng/coverage/README.md) |

## 1. Problem

ADR-0009 builds one net481 add-in against EPLAN 2026 and loads it in every version from 2024 to 2027, on the assumption that the API members the Bridge uses have the same signatures in all of them. Issue #664 showed that the assumption does not hold: a member that exists only in 2026 fails at run time on 2025, and because the CLR binds members when it compiles a method, the whole calling method fails, not just the line that uses the member.

Building `src/EplanMcp.Bridge.Eplan` from `6d14f7ec` against the EPLAN 2025.0.3 API assemblies (`-p:EplanVersion=2025 -p:EplanBinPath=...\Platform\2025.0.3\Bin`) fails with 2,781 distinct errors:

| Where | Distinct errors | What is missing in 2025 |
|---|---:|---|
| `Adapters/Api/ApiTryDispatch.g.cs` (generated) | 2,778 | 2,701 `Properties.*` identifiers; about 80 other members (`Unit`, `UnitGroup`, `PlanningSegment`, `BusBar`, `MDUserDefinedPropertyDefinition.UnitGroup`, `DesignSpace` with 3 arguments, ...) |
| `Adapters/Runtime/EplanPaths.cs:39` | 1 | `PathMap.SubstitutePath(String, String)`; already handled at run time by `c37eed1b` |
| `Adapters/Pages/EplanDrawingReader.Reading.cs:312` | 1 | `GetBoundingBox` with one argument; likely breaks `eplan_page_get` with `include_drawings` on 2025 |
| `Adapters/MasterData/EplanPartsDatabaseWriter.cs:69` | 1 | `MDPartsDatabase.ImportMode.ReplaceAndAppend`; parts database import |

Each of these is a run-time failure waiting to happen on 2025 with today's single build. They surface one at a time, as #664 did.

## 2. Today's state

- `EplanMcp.Bridge.Eplan.csproj` has two MSBuild properties, `EplanVersion` (default `2026`) and `EplanBinPath` (default `C:\Program Files\EPLAN\Platform\2026.0.3\Bin`). They only choose the referenced assemblies and drive `ValidateEplanApiReferences`. No compilation symbol is defined and there is no `#if` in the source.
- Version differences are handled at run time: `EplanPaths` keeps the 2026 overload in a `NoInlining` method and falls back after a `MissingMethodException`.
- The API catalog exists for one version only: `catalog/eplan-2026.0.3/`. The extractor `eng/catalog/EplanMcp.Catalog` already writes one folder per version (`extract --sdk <Bin> --out catalog/eplan-<version>`), but the generator in `eng/coverage` hard-codes `catalog/eplan-2026.0.3` in about ten places (`ApiTryGenerator*.cs`, `ApiTryServices.cs`).
- The Host finds one add-in: `AddinLocation` uses `<app>\bridge\net481\EplanMcp.Bridge.Eplan.dll`, or `src\EplanMcp.Bridge.Eplan\bin\<configuration>\net481\` in development.
- Projects that reference `EplanBinPath`: `EplanMcp.Bridge.Eplan`, `EplanMcp.Bridge.Offline`, the three `tests/EplanMcp.Large.*Helper` projects, and the spikes (out of scope).

## 3. Decision

Build the Bridge once per supported EPLAN version, each build against that version's own API assemblies, and register the matching build in each installation.

Considered and rejected:

- **One build against 2025, reflection for 2026-only members.** The generated dispatcher calls 2,701 property identifiers and about 80 members directly; turning those into reflection would remove the compile-time check the generator exists for.
- **One build against 2026 with a run-time fallback per member (today's approach).** Works for a single member, but every missing member is found only when a user hits it, and a missing member anywhere in a method takes the whole method down.

## 4. Plan

Each phase is one or a few commits on `build/bridge-eplan-2025-2026`. Commit rules of this repository apply: English, imperative, Conventional Commits, no AI attribution trailers (GIT-04), no machine-specific absolute paths (GIT-07), explicit `git add <path>`.

### Phase 0: measure the gap (no EPLAN process needed)

1. Extract the 2025 catalog: `dotnet run --project eng\catalog\EplanMcp.Catalog -- extract --sdk "<EPLAN 2025.0.3>\Bin" --out catalog\eplan-2025.0.3`.
2. Diff `catalog/eplan-2025.0.3` against `catalog/eplan-2026.0.3`: types, members and data-dictionary entries present in only one of them.
3. Join the diff with `catalog/mapping/bridge-usage.json` to list the members the Bridge uses that 2025 lacks.
4. Record the result in this note (section 6) and in the api-notes.

Exit criterion: a complete list of Bridge-used members missing in 2025, matching the compiler errors of section 1.

### Phase 1: version-aware build (`EplanMcp.Bridge.Eplan.csproj`)

1. Derive compilation symbols from `EplanVersion`: `EPLAN2025`, `EPLAN2026`, and cumulative `EPLAN2025_OR_GREATER`, `EPLAN2026_OR_GREATER`, added to `DefineConstants`.
2. Default `EplanBinPath` from `EplanVersion` instead of the fixed 2026.0.3 path; keep `-p:EplanBinPath` as the override and keep `ValidateEplanApiReferences`.
3. Put each build in its own folder, `bin\<configuration>\net481\eplan<version>\`, so that building both versions does not overwrite one with the other.
4. Apply the same to `EplanMcp.Bridge.Offline` and the three Large helpers.

Exit criterion: building without arguments produces the same 2026 add-in as before, byte-for-byte apart from the output path.

### Phase 2: generated code per version (`eng/coverage`)

1. Replace the hard-coded `catalog/eplan-2026.0.3` with a `--catalog <version>` option, defaulting to 2026.0.3.
2. `generate` writes one dispatcher per version, for example `Adapters/Api/Generated/eplan2025/ApiTryDispatch.g.cs`; the csproj compiles only the one for its `EplanVersion`.
3. `ApiTryCatalog.g.cs` (Contracts, net10, no EPLAN reference) stays one file: the union of both catalogs, with the versions in which each entry exists. On 2025, a request for a 2026-only member returns `CAPABILITY_MISSING`, not a `MissingMethodException`.
4. `check` verifies every generated file against its own catalog.

Exit criterion: `generate` then `check` succeeds; the Bridge compiles for 2025 with no errors in generated code.

### Phase 3: hand-written code

| File | Change |
|---|---|
| `Adapters/Runtime/EplanPaths.cs` | Replace the `MissingMethodException` fallback with `#if EPLAN2026_OR_GREATER` |
| `Adapters/Pages/EplanDrawingReader.Reading.cs` | Use the `GetBoundingBox` overload that 2025 documents, under `#if` |
| `Adapters/MasterData/EplanPartsDatabaseWriter.cs` | Map `ReplaceAndAppend` to the closest 2025 import mode, or return `CAPABILITY_MISSING` for that mode on 2025; to be decided with the maintainer |
| Anything else from phase 0 | Same pattern |

Exit criterion: `dotnet build src\EplanMcp.Bridge.Eplan -c Release -p:EplanVersion=2025` succeeds with warnings as errors.

### Phase 4: packaging and registration

1. The release ZIP carries `bridge\net481\eplan2025\` and `bridge\net481\eplan2026\`.
2. `AddinLocation` picks the folder from the version of each EPLAN installation found; the development path follows the same layout.
3. `doctor` and the Workbench show which build is registered for each version.
4. `release.yml` and the release process document: both builds need a machine with that EPLAN version installed (ADR-0020: no self-hosted runner), so the release is assembled from local builds.

Exit criterion: on a machine with EPLAN 2025, the Workbench registers the 2025 build, and the add-in starts (`bridge-*.log` shows the connection, no `bridge-startup-error.log`).

### Phase 5: verification

| Version | What | Where |
|---|---|---|
| 2025.0.3 | Bridge builds; Small/Medium tests pass; Large read tests on the offline host (`--filter-trait "Host=Offline"`), with the expectations tied to the 2026 sample project reviewed or marked; the checks requested in #664: `eplan_page_get` with `include_drawings: true`, an `eplan_connection_create` dry run, `eplan_device_get` for coils listed by `eplan_device_list` with `category` | Fork author's machine |
| 2026.0.3 | Default build unchanged; Large tests pass as before | Maintainer |
| Both | `eng/scripts/local-gate.ps1` passes | Both |

### Phase 6: contribution

1. Propose the change of ADR-0009 (one build per version instead of one build for 2024–2027) in issue #664 before opening a pull request; it is an architecture decision for the maintainer.
2. Open a pull request from the fork once phases 0 to 3 pass, with phase 0's gap list as evidence.

## 5. Risks and open questions

- The maintainer may prefer to keep one build. Then phase 0's list still has value: each entry needs a run-time fallback like `EplanPaths`.
- The Large tests assert data of the 2026 sample project (page, function and terminal strip counts); on 2025 they need either a 2025 fixture or per-version expectations.
- 2024 and 2027 stay "in range, not tested" under ADR-0009. With this design each would need its own catalog and build; whether to keep them in the supported range is open.
- `ReplaceAndAppend` on 2025: map or refuse (phase 3).

## 6. Results

### Phase 0 (2026-11-19)

- `catalog/eplan-2025.0.3/` extracted from EPLAN Platform 2025.0.3 with the existing extractor, unchanged.
- New command `diff` in `eng/catalog/EplanMcp.Catalog` (tests in `CatalogDiffTests`): compares two catalog folders by documentation comment ID, after unfolding enum values and data dictionary properties into their `F:` IDs, and marks the IDs that `catalog/mapping/bridge-usage.json` lists. Output: `catalog/mapping/eplan-2026.0.3-vs-2025.0.3.g.tsv`.

  ```powershell
  dotnet run --project eng\catalog\EplanMcp.Catalog -c Release -- diff --base catalog\eplan-2026.0.3 --other catalog\eplan-2025.0.3 --usage catalog\mapping\bridge-usage.json --out catalog\mapping\eplan-2026.0.3-vs-2025.0.3.g.tsv
  ```

| Side | IDs | Used by the Bridge |
|---|---:|---:|
| Only in 2026.0.3 | 3,187 | 2,374 |
| Only in 2025.0.3 | 96 | 0 |

Of the 2,374 used IDs, 2,372 are used only by generated code (`ApiTryDispatch.g.cs`; one also by `ObjectKindDispatch.g.cs`). Four are used by hand-written code:

| File | 2026-only member |
|---|---|
| `Adapters/Runtime/EplanPaths.cs` | `M:Eplan.EplApi.Base.PathMap.SubstitutePath(System.String,System.String)` |
| `Adapters/Pages/EplanDrawingReader.Reading.cs` | `M:Eplan.EplApi.DataModel.Graphics.PropertyPlacement.GetBoundingBox(System.Boolean)` |
| `Adapters/MasterData/EplanPartsDatabaseWriter.cs` | `F:Eplan.EplApi.MasterData.MDPartsDatabase.ImportMode.ReplaceAndAppend` |
| `Adapters/Connections/EplanConnectionRouter.cs` | `P:Eplan.EplApi.DataModel.E3D.InstallationSpace.Connection3Ds` |

The first three match the compiler errors of section 1. The fourth compiled against 2025, so the call binds to a member of the same name that 2025 has elsewhere in the hierarchy; to be confirmed in phase 3, since the bound member may behave differently.

Nothing that only 2025 has is used, so the 2025 build needs no 2025-only code paths.

### Phase 1 (2026-11-19)

- `eng/EplanApi.props` defines `EplanVersion` (default 2026), the default `EplanBinPath` and the compilation symbols. The add-in, the offline host and the three Large helpers import it instead of each hard-coding `...\Platform\2026.0.3\Bin`.
- The default Bin folder is the single `<Program Files>\EPLAN\Platform\<EplanVersion>.*` folder, so 2026.0.3 on the maintainer's machine resolves to the same path as before. None or several installations fail with a message that names `-p:EplanBinPath`.
- Symbols, checked with `dotnet msbuild -getProperty:DefineConstants`: version 2025 gives `EPLAN2025;EPLAN2024_OR_GREATER;EPLAN2025_OR_GREATER`; version 2026 adds `EPLAN2026_OR_GREATER` and has `EPLAN2026` instead of `EPLAN2025`.
- Deviation from section 4: the per-version output folder (step 3) moves to phase 4, together with `AddinLocation`, which reads that folder. Moving it now would break add-in registration from a development build until phase 4.

### Phase 2 (2026-11-20)

- `eng/coverage/CatalogFolder.cs` replaces the ten hard-coded `catalog/eplan-2026.0.3` paths of the API generators. `CatalogFolder.With(folder, render)` renders a file from another catalog. Only the API selection follows it: the API index (`ApiIndexGenerator`), the dispatch generator (`ApiTryGenerator*`, `ApiTryServices`) and `ObjectKindGenerator`. The tool mapping and the coverage report describe the Host's tools and keep reading the scanned version's catalog; a first attempt that switched them too failed because the 2025 catalog has no `actions.json`. The four `RunCache` keys of what follows the override include the folder, so a 2025 render never reuses a 2026 result.
- `ApiTryGenerator.VersionDispatches` lists one extra dispatcher per version, today `Adapters/Api/ApiTryDispatch.Eplan2025.g.cs` from `catalog/eplan-2025.0.3`. `generate` and `check` handle it like every other derived file, but only when that catalog folder exists, so the small synthetic repositories of the tests are unaffected. Like the default dispatcher, it is left out of the Bridge source hash, so regenerating it does not make the scan stale.
- Deviation from section 4, step 3: `ApiTryCatalog.g.cs` is not changed. The 2025 dispatcher has no case for a 2026-only member, so on 2025 such a call reaches the dispatcher's existing `default:` branch ("No compiled method for this signature."). Mapping that to `CAPABILITY_MISSING` is left for later; it needs the Host to know the running EPLAN version.
- `EplanMcp.Bridge.Eplan.csproj` compiles `ApiTryDispatch.Eplan<EplanVersion>.g.cs` instead of `ApiTryDispatch.g.cs` when that file exists.
- Results: `generate` leaves every existing derived file byte-for-byte unchanged and adds the 2025 dispatcher (3.7 MB, against 4.8 MB for 2026); `check` passes. Building the Bridge for 2025 now fails only on the three hand-written members of phase 0 (the fourth, `Connection3Ds`, compiles). Coverage tests: all pass except the 34 that start `pwsh`, which is not installed on this machine; new tests in `CatalogFolderTests` and `CoverageCheckTests.Generate_WithoutTheCatalogOfAnotherVersion_WritesNoDispatcherForIt`.

### Phase 3 (2026-11-20)

| File | Change |
|---|---|
| `Adapters/Runtime/EplanPaths.cs` | The run-time `MissingMethodException` fallback is replaced by `#if EPLAN2026_OR_GREATER`; before 2026 it calls `SubstitutePath(String)`, so the project's own path variables stay unresolved, as before on 2025 |
| `Adapters/Pages/EplanDrawingReader.Reading.cs` | New `PropertyOutline`: `GetBoundingBox(false)` (absolute) on 2026; on 2025 the relative `GetBoundingBox()` plus the owner's location, the sum A87 measured on 2026 for context property texts. Not verified live on 2025 |
| `Adapters/MasterData/EplanPartsDatabaseWriter.cs` | `replace_and_append` on 2025 fails with `CAPABILITY_MISSING` before the database is opened; the other three modes are unchanged |
| `Adapters/Connections/EplanConnectionRouter.cs` | No code change. On 2025, `target.Connection3Ds` binds to the inherited `Placement3D.Connection3Ds`, which may list fewer connections than 2026's `InstallationSpace.Connection3Ds`; documented in the class comment, to be checked in phase 5 |

`tests/EplanMcp.Bridge.Core.Tests` links `EplanPaths.cs` and the drawing reader against its fake EPLAN API, which follows 2026; the project now defines the 2026 symbols itself, without importing `eng/EplanApi.props` (that looks for an EPLAN installation, which CI lacks).

Results:

- `dotnet build src\EplanMcp.Bridge.Eplan -c Release -p:EplanVersion=2025`: succeeds with no warnings (warnings are errors in this repository).
- `tests/EplanMcp.Bridge.Core.Tests`: 5,962 of 5,967 pass. The 5 failures are in `PageExportRoundFourTests` and come from the machine's culture (es-ES, decimal comma), not from this change: with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` all 30 tests of that class pass. A separate issue for the maintainer.
- The default 2026 build is not compiled here (no EPLAN 2026 on this machine); the `#if EPLAN2026_OR_GREATER` branches are the previous code.

### Phase 4 (2026-11-20)

- The add-in builds to `bin\<configuration>\net481\eplan<year>\` (obj split the same way), set in `EplanMcp.Bridge.Eplan.csproj`. This is phase 1, step 3, moved here. The offline host and the Large helpers keep their output folders: they are development tools, and their paths are used by the Large tests and `release-addin.ps1`.
- `AddinLocation.Find(hostDirectory, eplanVersion, out searched)` looks, in order, for the build of that EPLAN version and then for the single build of earlier releases, first in the installed layout (`<app>\bridge\net481\eplan<year>\`, then `<app>\bridge\net481\`), then in the development layout. It never falls back to another version's build. `AddinLocation.Any` tells whether any build is there at all.
- `install-addin` (`AddinCommand.InstallAsync`) looks up the build per EPLAN installation. An installation whose version has no build is reported as refused with `AddinNotFound` and the paths looked at; the others are registered as before. The report's add-in path is the first build found. The not-found message names `eplan<year>` and `-p:EplanVersion`.
- `eng/scripts/eplan-addin.ps1` (development registration) takes `-EplanVersion` (default 2026), picks the build in `eplan<year>` and, without `-EplanBin`, the single installation of that version. Parsed without errors; not run (no EPLAN 2026 here, and registering changes the engineer's EPLAN).
- Tests: `AddinLocationTests` (per-version, single build, development, `Any`, `VersionFolder`) and `AddinCommandTests.Install_WithPerVersionBuilds_RegistersEachVersionsBuild_AndRefusesAVersionWithoutOne`; all 111 tests of `EplanMcp.Host.Tests.Addin` pass.

Not done in this phase, on purpose:

- **Release pipeline** (`eng/scripts/release-addin.ps1`, `eng/scripts/release-package.ps1`, `.github/workflows/release.yml`). Today one add-in ZIP per tag is built on the maintainer's EPLAN 2026 machine, its SHA-256 line goes into the draft notes, and the workflow checks a fixed five-entry allowlist and `eplanVersion == '2026'`. Per-version builds need one ZIP per EPLAN version (for example `EplanMcp-addin-<tag>-eplan<year>.zip` with `bridge/net481/eplan<year>/`), a hash line for each, a loop over the expected versions in both checks, and a machine with each EPLAN version for the tag build. That changes the maintainer's release and supply-chain checks, cannot be run here (no `pwsh`, no EPLAN 2026, no rights on the draft releases), and until it changes the released ZIP keeps the single-build layout, which `AddinLocation` still finds. Proposed to the maintainer instead of changed blind.
- **Workbench and `doctor`** showing which build each version uses: `install-addin`'s report already names the DLL per registration in the record; the UI change is left for after the maintainer's decision on the layout.

### Phase 5 (2026-11-20, EPLAN Electric P8 2025.0.3.23616)

Setup: the add-in built from this branch with `-p:EplanVersion=2025` (`bin\Release\net481\eplan2025\`), the Host of this branch (`eplanmcp` 0.7.0), and a copy of EPLAN 2025's own `EPLAN_Sample_Project` made by `eng/scripts/eval-fixture.ps1` (322 pages, 504 devices, 2,060 connections). Tools were called over stdio.

Registration:

- `install-addin --variant "Electric P8"` picked the 2025 build (`...\net481\eplan2025\EplanMcp.Bridge.Eplan.dll`), as phase 4 intends.
- It did not take: an earlier registration (the 0.6.0 release) loads at start-up, and EPLAN kept loading it. `install-addin` reported this ("EPLAN loaded the add-in from another path ... this one did not take"); the re-registration path of design 4.2 is still unverified, and on this machine it did not work either. A licence selection dialog at the first start also made the first attempt time out.
- Done by hand instead: `EPLAN.exe /Variant:"Electric P8" EplApiModuleAction /unregister:"EplanMcp.Bridge.Eplan"`, close EPLAN, then `/register:"<2025 build>"`. `eplanmcp doctor` then reported `Handshake OK: add-in 0.7.0 ... EPLAN 2025.0.3.23616 (Electric P8)`.

Results:

| Check (asked for in #664 or from phase 3) | 0.6.0 on 2025 (#664) | This branch on 2025 |
|---|---|---|
| `eplan_page_get` with `include_drawings` on `=F+A2&EFS1/1` | `MissingMethodException` | Works: all 803 drawing items read through `nextCursor` |
| Property text bounds (phase 3, `PropertyOutline`) | not reached | All 256 non-empty property texts have a box that contains their absolute location, at most 44.7 mm wide; the other 423 are empty texts with an all-zero box, which `4020e81a` intends |
| `eplan_connection_create` dry run (t-node on `=F+A2&EFS1/1`) | failed in planning | Works: `not_started`, predicted symbol, 1 undo step |
| `eplan_device_get` for each coil `eplan_device_list` prints with `category: "Coil"` | `DEVICE_NOT_FOUND` | All 9 found, including the nested `=EC1+A3-EC1-QA1-K1` |
| `eplan_connection_list` by device `=F+A2-KF1` | `DEVICE_NOT_FOUND` | 6 connections |
| `eplan_device_list` without filters | incomplete | 507 devices |
| `eplan_connection_route` dry run, spaces A1 and A2 (`Connection3Ds`, phase 3) | not tested | **0 and 0 connections**; A141 measured 176 and 332 on the 2026 sample |

The last row confirms the risk noted in phase 3: on 2025, `InstallationSpace.Connection3Ds` does not exist and the code binds to the inherited `Placement3D.Connection3Ds`, which does not list the space's connections. Proposed fix, not done yet: on 2025, collect the connections from the space's 3D placements (or from `Connection.Connection3D` of the project's connections filtered by space), and check the count against 176 / 332. Until then, on 2025 `eplan_connection_route` finds nothing to route; it changes nothing, so it fails safe.

Not checked live: the 2026 build (no EPLAN 2026 here), `replace_and_append` refusal (it writes the system parts database), and the Large test suite (its expectations are the 2026 sample's).
