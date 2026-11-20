# Upstream contribution plan for per-version Bridge builds (issue #664)

Date: 2026-11-20. Status: in progress. Source branch: `build/bridge-eplan-2025-2026` (kept as the reference
implementation; see `2026-11-19-per-version-bridge-builds.md`).

## Context

The maintainer answered on #664 (comments 7028770298 and 7029222131):

- v0.8.0 preview ships run-time fallbacks for `PathMap.SubstitutePath` and `PropertyPlacement.GetBoundingBox`, and the
  device lookup fix. `InstallationSpace.Connection3Ds` is still open on `main`.
- The direction (one Bridge build per EPLAN version) is accepted in principle and written up as ADR-0060 (proposed).
  ADR-0060 adds four requirements on top of our branch:
  1. Version conditions use only symbols registered in one file, each with its version range; a check rejects others.
     Symbols are named after the capability, not the year.
  2. The support matrix has three levels: verified live, compiled but not verified live, not covered.
  3. The handshake and `doctor` report the EPLAN version the add-in was built for; a mismatch with the running EPLAN
     refuses writes.
  4. A release ZIP carries only builds the maintainer compiled and ran the offline tests for.
- ADR-0060 also asks that `ApiTryCatalog` records which versions each entry exists in, that `eplan_api_try` answers
  `CAPABILITY_MISSING` for a member the build's version lacks, and that registration migrates from the old single path.
- The work is to come as small pull requests, rebased on current `main`, following `CONTRIBUTING.md`, with no EPLAN
  assemblies anywhere.

## Constraints

- Only EPLAN 2025.0.3 is available here; the maintainer verifies on 2026.
- Parts 3 to 5 wait until ADR-0060 is accepted. Parts 1 and 2 do not.
- Nothing is pushed to the upstream repository or opened as a pull request without explicit approval.
- Code, comments, documentation and commit messages are in English.

## Plan

### Step 0. Fresh branches from `origin/main`

Each pull request gets its own branch cut from `origin/main`; the source branch is not rebased. Generated files
(`catalog/*.g.*`, `bridge-usage.json`, `coverage-report.md`, `ApiTryDispatch*.g.cs`) are regenerated, never merged by
hand. Expected conflicts: `EplanDrawingReader.Reading.cs` (new `EplanTextBounds`), the generated catalog files.

### PR 1a. Page export tests under a culture with a decimal comma

Find the culture-dependent formatting or parsing behind the 5 failing `PageExportRoundFourTests`, fix it with the
invariant culture where the value is data, and add a test that runs under es-ES so it cannot come back.

### PR 1b. `install-addin` over an earlier registration

1. Answer the maintainer's question with the `bridge-20261120.log` lines (EPLAN state, earlier registration = 0.6.0 ZIP).
2. Reproduce on 2025: 0.6.0 registered, then `install-addin` from another build.
3. Find why the unregister/re-register path in `AddinCommand.RegisterAsync` did not take.
4. Fix it with a unit test, or hand the diagnosis back if a fix needs a 2026 check.

### PR 2. EPLAN 2025.0.3 catalog

`catalog/eplan-2025.0.3`, the `CatalogDiff` command with its tests, the 2026-vs-2025 mapping, and the list of members
the Bridge uses that 2025 lacks (as api-notes entries or a separate file, as the maintainer prefers). Metadata only.

### PR 3. Build properties, registered symbols and their check (after ADR-0060)

`eng/EplanApi.props` as on the source branch; the default build stays 2026 and its output is unchanged. New: one file
registering capability symbols with version ranges, and a check (test or `local-gate.ps1` step) that rejects any
preprocessor symbol in the Bridge sources that is not registered.

### PR 4. Generator per catalog (after ADR-0060)

`CatalogFolder` and the per-version dispatcher from the source branch. New: `ApiTryCatalog` records the versions of each
entry, and `eplan_api_try` returns `CAPABILITY_MISSING` for a member missing in the build's version.

### PR 5. Hand-written differences, registration, handshake and doctor (after ADR-0060)

May be split into 5a and 5b.

- 5a: the four API differences become compile-time branches on registered symbols, replacing the `EplanPaths` and
  `EplanTextBounds` run-time fallbacks, plus the `Connection3Ds` fix.
- 5b: registration per installation with migration from the single legacy path; the handshake carries the build's
  EPLAN version, `doctor` shows it, and a mismatch refuses writes. Older add-ins that do not send it keep working.

### Closing

Run the ADR-0009 live checklist on 2025 and hand the results over for the acceptance ledger. The support matrix and the
release remain the maintainer's.

## Per pull request

- Conventional Commits in English, no AI attribution trailers, explicit `git add`.
- `eng/scripts/local-gate.ps1` passes.
- Pushed to the fork and opened upstream only after approval.

## Progress

| Step | Branch | Status |
|---|---|---|
| 0 | one branch per pull request from `origin/main` (`aaa0398c`) | done |
| 1a | `fix/page-export-tests-culture` | pull request #665; rebased on `80faa802` (not pushed yet), gate green again (17,955 tests under es-ES). Besides the 5 `PageExportRoundFourTests`, two `Host.Tests` fakes formatted numbers with the current culture. |
| 1b | `fix/install-addin-eplan-not-closed` | the maintainer chose both changes, the second as a list of harmless windows (#664 comment 7036431319, ADR-0063 proposed). Three local commits: window recognition, the closer closing the system messages window and `eplan_not_closed`, and api-notes A172. Gate running; live run on 2025 pending. |
| 2 | `feat/catalog-eplan-2025` | pull request #666; rebased on `80faa802` (not pushed yet). The maintainer had added his own B19 (build 23406), so ours became B20; gate green except the 7 es-ES failures that #665 fixes |
| 3 | — | waiting for ADR-0060 |
| 4 | — | waiting for ADR-0060 |
| 5 | — | waiting for ADR-0060 |
