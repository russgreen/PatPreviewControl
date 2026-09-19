# FillPatternPreview - Implementation Task List

This checklist is derived from specs/FillPatternPreview.implementation.plan.md and specs/FillPatternPreview.spec.md. Use it to track delivery.

> Status note (see `specs/FillPatternPreview.spec.md` section 2): the spec was rewritten to describe the actual v1 build - parsing plus immediate-mode rendering only. This checklist still tracks the original, larger design; checkboxes below reflect what's genuinely implemented today, not what the file paths/structure originally called for. `[~]` marks an item that's partially done or done differently than described (e.g. inline in `Controls/FillPatternPreview.cs` rather than a separate `Rendering/` file) - see the note under it.

## Setup
- [~] Create/verify folders: `Rendering`, `Caching`, `Diagnostics`, `Accessibility`, `Adapters` under `src/FillPatternPreview`. (`Adapters/`, `PatternMaker/`, and now `Rendering/` (added this session - see Milestone 4) exist; `Caching/`, `Diagnostics/`, `Accessibility/` were never created - there's no code to house yet.)
- [x] Add test project `tests/FillPatternPreview.Tests` and include it in the solution/CI. (Added this session: xUnit, `net8.0-windows`, referenced from `PatPreviewControl.sln`. Not yet in CI - no CI configuration exists in the repo at all.)

## Milestone 1 - Data & Parser
- [x] Define immutable records (spec section 5)
  - [x] `src/FillPatternPreview/Model/PatternDefinition.cs`: `PatternDefinition`, `LineGroup`, `PatternDiagnostics`. (All three exist; `PatternDiagnostics` is defined but still never populated/exposed - see Milestone 8.)
  - [x] `src/FillPatternPreview/Parsing/PatternParseResult.cs`: `Success`, `Errors`, `Warnings`, `Dictionary<string, PatternDefinition>`.
- [~] Implement `.pat` parser (spec section 7)
  - [x] `src/FillPatternPreview/Parsing/PatParser.cs`: headers, comments, `;%UNITS`, `;%TYPE`, invariant doubles. (`%UNITS` parsing and units-aware default scale were added this session - see spec section 9.)
  - [ ] Require >=5 numeric tokens; clamp large dash values; skip/record malformed lines. (5-token minimum and malformed-line skipping are done. Dash values are not clamped, but any value with |v| > 1e9, NaN or Infinity is rejected as an error and the line skipped.)
  - [x] Duplicate name handling; selection by `PatPatternName`. (Implemented, but as "last wins" - the spec originally called for "first wins"; flagged as a known deviation in spec section 7.)
  - [ ] File parse cache key inputs prepared (path, last write, content hash). (No caching at all - `ParseFile` re-reads and re-parses on every call.)
- [x] Tests
  - [x] `tests/FillPatternPreview.Tests/ParsingTests.cs`: valid/invalid lines, multiple patterns, comments, `%TYPE`/`%UNITS` (both forms), culture invariance, duplicates, missing file, empty pattern. (Added this session. "Large dash" clamping isn't tested since it isn't implemented yet - see the parser row above.)

## Milestone 2 - Control Skeleton & Acquisition
- [~] Register dependency properties (spec section 6)
  - [~] `src/FillPatternPreview/Controls/FillPatternPreview.cs`: all DPs + coercion (Scale>0, Zoom in [0.1,20]). (Implemented: `PatFilePath`, `PatRawText`, `PatPatternName`, `LineBrush`, `Scale`, `Zoom`, `PanOffset`, `UsePatternMaker`, `Pattern`. Not implemented: `PatternSource`, `RevitFillPattern`, `StrokeThicknessOverride`, `IsModelPatternOverride`, `RenderMode`, `TileSizeHint`, `ShowBounds`, `ErrorTemplate`, `IsInteractive`, `SnapsToDevicePixels`. `Scale`/`Zoom` are coerced to `[0.0001, 1e6]` and `PanOffset` to finite +/-1e9, but `Zoom` is not narrowed to 0.1-20 on the property itself - only interactive zoom is.)
  - [ ] Read-only DPs: `Pattern`, `Diagnostics`. (`Pattern` done; `Diagnostics` not implemented.)
  - [ ] Events: `PatternChanged`, `ParseFailed`, `InteractionChanged`. (Only `PatternChanged` exists.)
- [ ] Implement acquisition workflow
  - [ ] Cancellation tokens; Dispatcher marshalling. (Parsing is synchronous on the calling thread; no async path exists.)
  - [~] Source branches: PatFile (async read+parse), PatText (parse), FillPatternObject (reflection), InternalModel. (`PatFilePath`/`PatRawText` work, synchronously. No Revit `FillPatternObject` or `InternalModel` source.)
  - [ ] Apply `IsModelPatternOverride`. (Property doesn't exist; `IsModel` always comes from the parsed `%TYPE`.)

## Milestone 3 - Tile Computation & Geometry
- [ ] Tile computation (spec section 9)
  - [ ] `src/FillPatternPreview/Rendering/PatternTiler.cs`: tile size, Tileable flag, bounds expansion, limits (MaxTileDimension), defaults. (Not implemented - no tile domain is ever computed; see spec section 4 "Tile domain".)
- [ ] Tiled geometry generation (spec section 10)
  - [ ] Enumerate lines per group across tile; dash expansion (positive/negative/zero=dot). (Dash expansion itself is implemented, but per-viewport every render pass, not per-tile.)
  - [ ] Build `StreamGeometry` batches; `DrawingGroup` + `DrawingBrush`; Freeze all. (No `DrawingBrush`/tiling pipeline exists at all - see Milestone 4.)

## Milestone 4 - Rendering Integration
- [ ] Rendering pipeline selector
  - [ ] `src/FillPatternPreview/Controls/FillPatternPreview.cs`: Auto/Immediate decision (thresholds). Apply transforms: Scale*Zoom then Pan. Optional bounds. (No Auto/tiled-vs-immediate decision exists - rendering is unconditionally immediate-mode. Transform is `Scale*Zoom*UnitsToDipFactor` (units factor added this session, spec section 9); `Pan` is a no-op - `PanOffset` has no effect on `OnRender`.)
- [x] Immediate renderer
  - [x] `src/FillPatternPreview/Rendering/PatternImmediateRenderer.cs`: visible rect, bounded enumeration, `DrawLine`/`StreamGeometry` per frame. (Renamed/implemented as `src/FillPatternPreview/Rendering/LineFamilyExpander.cs` - the pure geometry (world-delta rotation, repeat-range, chord intersection, dash expansion) was extracted from the `Control` into this `internal static` class this session specifically so it could be unit tested (see Milestone 10); `Controls/FillPatternPreview.cs` now just calls it and issues the resulting `DrawLine`/`DrawRectangle` calls. This was the focus of most of this session's bug fixes: delta shift/offset rotation, dash-phase anchoring, intersection-order sign fix, k-range sign fix, and a dash-cycle sub-pixel/segment-count cap to prevent hangs on drafting patterns - all now covered by `LineFamilyExpanderTests.cs`.)

## Milestone 5 - Caching
- [ ] Parse file cache
  - [ ] `src/FillPatternPreview/Caching/ParseFileCache.cs`: `ConcurrentDictionary<(Path, LastWrite, ContentHash), PatternParseResult>` with soft limit. (Not implemented.)
- [ ] Geometry cache (LRU)
  - [ ] `src/FillPatternPreview/Caching/GeometryCache.cs`: key `(PatternHash, StrokeBucket, TileKey)`, size=16, eviction; invalidation on Pattern/Scale/Stroke/TileSizeHint. (Not implemented - nothing is cached; every `OnRender` re-does full line enumeration and dash expansion.)
- [ ] Hashing utilities
  - [ ] Stable PatternHash (round 1e-6); stroke bucket; tile key rounding. (Not implemented.)

## Milestone 6 - Reflection Adapter (Revit) (spec section 8)
- [ ] `src/FillPatternPreview/Adapters/RevitFillPatternAdapter.cs`: detect `Autodesk.Revit.DB.FillPattern`; extract `Name`, `IsModel`, segments via reflection; normalize to `LineGroup`; warnings on partial/missing members. (Not implemented. Note: `Adapters/PatternMakerAdapter.cs` exists but is unrelated - it's the experimental `UsePatternMaker` rendering path, not a Revit reflection adapter.)

## Milestone 7 - Interaction (spec section 15)
- [x] Mouse: wheel zoom around cursor; drag pan; double-click reset. (`IsInteractive` DP, default `false`. `OnMouseWheel`/`OnMouseLeftButtonDown`/`OnMouseMove` in `Controls/FillPatternPreview.cs`; zoom is 1.1x per wheel notch, clamped to 0.1-20 in the interaction path only - the `Zoom` DP itself is still not coerced. `PanOffset` is now actually applied in `OnRender` (a translate of `PanOffset * EffectiveScale`), in pattern-native units.)
- [x] Keyboard: `+`/`-` zoom; arrows pan; `Ctrl+0` reset. (Needs keyboard focus; a click on the control focuses it. Arrows pan 10 device units and move the pattern in the arrow's direction, like a drag.)
- [x] Raise `InteractionChanged` on state changes. (`EventHandler`, raised only when Zoom/PanOffset actually change through interaction, including reset; not raised for programmatic DP sets.)
- [x] Tests: `tests/FillPatternPreview.Tests/InteractionMathTests.cs` covers the zoom-at-cursor anchor invariant, zoom clamping, and pan math, via the extracted `Rendering/InteractionMath.cs`. (The `Control` event handlers themselves aren't unit tested; sample app has an Interactive checkbox and status line for manual checking.)

## Milestone 8 - Diagnostics, Accessibility, Logging
- [ ] Diagnostics aggregation/update (spec section 14)
  - [ ] `src/FillPatternPreview/Diagnostics/PatternDiagnosticsBuilder.cs`: initial parse diagnostics; update with TileSize/Tileable post-geometry. (Not implemented - `PatternDiagnostics` record exists but nothing constructs or exposes it.)
- [ ] Accessibility (spec section 17)
  - [ ] `src/FillPatternPreview/Accessibility/FillPatternPreviewAutomationPeer.cs`: name/description/IsModel/diagnostics summary. (Not implemented.)
- [ ] Logging hooks (spec section 18)
  - [ ] `IFillPatternLogger` no-op default; call at parse/render/error points. (Not implemented; the only diagnostics today are `Debug.WriteLine` calls in the parser and control.)

## Milestone 9 - Performance & Reliability (spec sections 12, 16, 20)
- [~] Freeze Freezables: geometry, brushes, pens, drawings. (The stroke `Pen` and a snapshot of its brush are now frozen each render - this was a real freeze: drawing thousands of dashes with an unfrozen pen took seconds and grew quadratically (2 families ~2.4 s, 4 ~9.5 s, 32 > 20 s; afterwards 32 in ~70 ms). Clip geometries/transforms are still per-render and unfrozen; no pen/geometry reuse across renders yet.)
- [ ] Minimize allocations; reuse pens/matrices; conditional `GuidelineSet` when `SnapsToDevicePixels`. (Not done; `SnapsToDevicePixels` DP doesn't exist.)
- [~] Enforce limits: `MaxTileDimension`, `MaxLinesPerGroup`. (No tile concept to bound (Milestone 3 isn't built), but equivalent immediate-mode guards exist and were hardened this session: a 4000-repeat cap per line family, and a new 20,000-segment cap plus sub-pixel-cycle solid-line shortcut per visible chord - the latter added specifically to fix a real hang on a drafting pattern with sub-pixel-scale dash values. A per-render-pass aggregate budget (`Rendering/RenderBudget.cs`, 250,000 units: one per visible chord and per dash segment) now bounds total work; when spent, remaining lines are not drawn. Repeat-range math is overflow-safe (`RepeatRangeFromBounds`/`IsRepeatRangeUsable`); parser limits: 4 MiB input, 8192-char lines, 10,000 patterns, 512 line groups/pattern, 64 dash entries, |value| <= 1e9, NaN/Infinity rejected.)
- [~] Cancellation guards; design-mode safe try/catch. (`OnRender` and `LoadPattern` now catch all exceptions, so a failure leaves the pattern undrawn/absent rather than crashing the host; `ParseFile` returns an error result for IO/permission failures; clip/transform pushes are popped via `using` scopes. `Scale`/`Zoom` (`[0.0001, 1e6]`, NaN -> 1) and `PanOffset` (finite, +/-1e9) are coerced at the DP level. Still missing: cancellation, and file reads are synchronous on the UI thread - a hung network path can still block it. No explicit `GetIsInDesignMode` guard.)

## Milestone 10 - Testing, Samples, Docs
- [~] Unit/integration tests
  - [~] Tiling behavior; dash expansion; hashing stability; cache reuse/eviction; render decision; interaction math; error resilience. (Dash expansion, and the immediate-mode geometry it depends on (world-delta rotation, repeat-range, chord intersection, units scale), are covered by `LineFamilyExpanderTests.cs` - added this session, 40 tests total across both test files, all passing. Tiling/hashing/caching/interaction don't exist yet to test - see Milestones 3, 5, 7.)
- [ ] Visual tests (optional)
  - [ ] `RenderTargetBitmap` golden comparisons with +/-1 px tolerance across key patterns and DPIs. (Not implemented - the geometry-level tests above check exact computed coordinates instead, which is more stable across environments than pixel comparison.)
- [~] Samples
  - [ ] Update `samples/PatternPreviewSampleApp` to demonstrate all `PatternSource` modes and interactions. (Sample app only demonstrates raw-text/file-path input via a textbox + Apply button; no interaction to demonstrate since none is implemented, and no other `PatternSource` modes exist.)
- [~] Documentation & CI
  - [x] Keep spec aligned with implementation. (`specs/FillPatternPreview.spec.md` was rewritten this session to accurately describe v1 as built, with the original larger design preserved as a "Deferred to v2" roadmap.)
  - [ ] Public XML docs; README usage and DP table. (Not done - no XML doc comments on the public DPs/API, no README.)
  - [ ] Ensure CI runs `dotnet test && dotnet build`. (No CI configuration exists in the repo; there's also no `dotnet test` target since there are no tests.)

## Definition of Done (summary)
- [ ] Parser robust per spec; diagnostics populated; warm cache works. (Parser is reasonably robust for malformed lines and now has test coverage, but diagnostics aren't populated and there's no cache.)
- [ ] Rendering Auto mode correct; meets perf targets for 200x200 px preview. (No Auto mode exists; immediate-mode rendering is now correctness-verified for solid/dashed lines at multiple angles and a hang-prone drafting pattern, both by manual visual testing and by an automated regression suite, but no formal perf benchmarking has been done.)
- [ ] Interactivity smooth and bounded; accessibility present. (Neither exists.)
- [ ] Caching effective with LRU; geometry reused across color changes. (No caching exists.)
- [~] Tests cover parsing, tiling, rendering, interaction, caching; docs updated. (Parsing and immediate-mode rendering are covered by `tests/FillPatternPreview.Tests` (40 tests, added this session); tiling, interaction, and caching don't exist yet to test. Spec docs are updated and accurate as of this session.)
