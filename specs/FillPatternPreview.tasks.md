# FillPatternPreview — Implementation Task List

This checklist is derived from specs/FillPatternPreview.implementation.plan.md and specs/FillPatternPreview.spec.md. Use it to track delivery.

## Setup
- [ ] Create/verify folders: `Rendering`, `Caching`, `Diagnostics`, `Accessibility`, `Adapters` under `src/FillPatternPreview`.
- [ ] Add test project `tests/FillPatternPreview.Tests` and include it in the solution/CI.

## Milestone 1 — Data & Parser
- [ ] Define immutable records (spec §5)
  - [ ] `src/FillPatternPreview/Model/PatternDefinition.cs`: `PatternDefinition`, `LineGroup`, `PatternDiagnostics`.
  - [ ] `src/FillPatternPreview/Parsing/PatternParseResult.cs`: `Success`, `Errors`, `Warnings`, `Dictionary<string, PatternDefinition>`.
- [ ] Implement `.pat` parser (spec §7)
  - [ ] `src/FillPatternPreview/Parsing/PatParser.cs`: headers, comments, `;%UNITS`, `;%TYPE`, invariant doubles.
  - [ ] Require ?5 numeric tokens; clamp large dash values; skip/record malformed lines.
  - [ ] Duplicate name handling; selection by `PatPatternName`.
  - [ ] File parse cache key inputs prepared (path, last write, content hash).
- [ ] Tests
  - [ ] `tests/FillPatternPreview.Tests/ParsingTests.cs`: valid/invalid lines, multiple patterns, comments, zero/negative/large dash, culture invariance, duplicates.

## Milestone 2 — Control Skeleton & Acquisition
- [ ] Register dependency properties (spec §6)
  - [ ] `src/FillPatternPreview/Controls/FillPatternPreview.cs`: all DPs + coercion (Scale>0, Zoom?[0.1,20]).
  - [ ] Read-only DPs: `Pattern`, `Diagnostics`.
  - [ ] Events: `PatternChanged`, `ParseFailed`, `InteractionChanged`.
- [ ] Implement acquisition workflow
  - [ ] Cancellation tokens; Dispatcher marshalling.
  - [ ] Source branches: PatFile (async read+parse), PatText (parse), FillPatternObject (reflection), InternalModel.
  - [ ] Apply `IsModelPatternOverride`.

## Milestone 3 — Tile Computation & Geometry
- [ ] Tile computation (spec §9)
  - [ ] `src/FillPatternPreview/Rendering/PatternTiler.cs`: tile size, Tileable flag, bounds expansion, limits (MaxTileDimension), defaults.
- [ ] Tiled geometry generation (spec §10)
  - [ ] Enumerate lines per group across tile; dash expansion (positive/negative/zero=dot).
  - [ ] Build `StreamGeometry` batches; `DrawingGroup` + `DrawingBrush`; Freeze all.

## Milestone 4 — Rendering Integration
- [ ] Rendering pipeline selector
  - [ ] `src/FillPatternPreview/Controls/FillPatternPreview.cs`: Auto/Immediate decision (thresholds). Apply transforms: Scale*Zoom then Pan. Optional bounds.
- [ ] Immediate renderer
  - [ ] `src/FillPatternPreview/Rendering/PatternImmediateRenderer.cs`: visible rect, bounded enumeration, `DrawLine`/`StreamGeometry` per frame.

## Milestone 5 — Caching
- [ ] Parse file cache
  - [ ] `src/FillPatternPreview/Caching/ParseFileCache.cs`: `ConcurrentDictionary<(Path, LastWrite, ContentHash), PatternParseResult>` with soft limit.
- [ ] Geometry cache (LRU)
  - [ ] `src/FillPatternPreview/Caching/GeometryCache.cs`: key `(PatternHash, StrokeBucket, TileKey)`, size=16, eviction; invalidation on Pattern/Scale/Stroke/TileSizeHint.
- [ ] Hashing utilities
  - [ ] Stable PatternHash (round 1e-6); stroke bucket; tile key rounding.

## Milestone 6 — Reflection Adapter (Revit) (spec §8)
- [ ] `src/FillPatternPreview/Adapters/RevitFillPatternAdapter.cs`: detect `Autodesk.Revit.DB.FillPattern`; extract `Name`, `IsModel`, segments via reflection; normalize to `LineGroup`; warnings on partial/missing members.

## Milestone 7 — Interaction (spec §15)
- [ ] Mouse: wheel zoom around cursor; drag pan; double-click reset.
- [ ] Keyboard: `+`/`-` zoom; arrows pan; `Ctrl+0` reset.
- [ ] Raise `InteractionChanged` on state changes.

## Milestone 8 — Diagnostics, Accessibility, Logging
- [ ] Diagnostics aggregation/update (spec §14)
  - [ ] `src/FillPatternPreview/Diagnostics/PatternDiagnosticsBuilder.cs`: initial parse diagnostics; update with TileSize/Tileable post-geometry.
- [ ] Accessibility (spec §17)
  - [ ] `src/FillPatternPreview/Accessibility/FillPatternPreviewAutomationPeer.cs`: name/description/IsModel/diagnostics summary.
- [ ] Logging hooks (spec §18)
  - [ ] `IFillPatternLogger` no-op default; call at parse/render/error points.

## Milestone 9 — Performance & Reliability (spec §12, §16, §20)
- [ ] Freeze Freezables: geometry, brushes, pens, drawings.
- [ ] Minimize allocations; reuse pens/matrices; conditional `GuidelineSet` when `SnapsToDevicePixels`.
- [ ] Enforce limits: `MaxTileDimension`, `MaxLinesPerGroup`.
- [ ] Cancellation guards; design-mode safe try/catch.

## Milestone 10 — Testing, Samples, Docs
- [ ] Unit/integration tests
  - [ ] Tiling behavior; dash expansion; hashing stability; cache reuse/eviction; render decision; interaction math; error resilience.
- [ ] Visual tests (optional)
  - [ ] `RenderTargetBitmap` golden comparisons with ±1 px tolerance across key patterns and DPIs.
- [ ] Samples
  - [ ] Update `samples/PatternPreviewSampleApp` to demonstrate all `PatternSource` modes and interactions.
- [ ] Documentation & CI
  - [ ] Public XML docs; README usage and DP table; keep `FillPatternPreviewControl_PRD.md` aligned.
  - [ ] Ensure CI runs `dotnet test && dotnet build`.

## Definition of Done (summary)
- [ ] Parser robust per spec; diagnostics populated; warm cache works.
- [ ] Rendering Auto mode correct; meets perf targets for 200×200 px preview.
- [ ] Interactivity smooth and bounded; accessibility present.
- [ ] Caching effective with LRU; geometry reused across color changes.
- [ ] Tests cover parsing, tiling, rendering, interaction, caching; docs updated.
