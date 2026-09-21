# Specification: FillPatternPreview WPF Control

## 1. Purpose
Provide a reusable WPF control (`FillPatternPreview`) that previews AutoCAD/Revit style hatch (fill) patterns defined in `.pat` syntax, given as raw text or a file path.

## 2. Status
This document describes the **v1 implementation as it actually exists in this repository** — parsing plus immediate-mode rendering. The larger feature set originally envisioned for this control (tiled/cached rendering, Revit reflection adapter, interaction, accessibility, diagnostics events) has **not** been built yet. That vision is preserved in [§13 Deferred to v2](#13-deferred-to-v2) as a roadmap, not a description of current behavior. Keep this document in sync with the code as v1 evolves; when a v2 item is implemented, move its description up into the main body and remove it from §13.

## 3. Scope
**In scope (implemented):**
- Parsing `.pat` text or a `.pat` file into one or more named `PatternDefinition`s (§6).
- A WPF `Control` (`FillPatternPreview`) that binds to raw text or a file path and renders the selected pattern (§5).
- Immediate-mode rendering: on every `OnRender`, each line family in the pattern is expanded directly into `DrawingContext` calls across the visible viewport — no caching, no `DrawingBrush` tiling (§7).
- Solid and dashed line families, including the shift/offset semantics that produce staggered (e.g. running-bond brick) coursing (§7).
- Units-aware default scale: `%UNITS=` is parsed and, for drafting patterns, automatically converted to a print-true device-pixel scale (§9) — no per-pattern manual `Scale` calculation needed.
- An experimental, opt-in alternate rendering path (`UsePatternMaker`) that is **not** a faithful interpreter of arbitrary `.pat` content — see §8.

**Out of scope for v1** — see §13 for the full list; headline items: geometry/tile caching, Revit `FillPattern` reflection adapter, accessibility automation peer, a wired-up `Diagnostics` property/event.

## 4. Core Concepts
- **PatternDefinition**: name, optional description, `IsModel` flag, list of `LineGroup`s.
- **LineGroup**: one hatch line *family* — an infinite line repeated indefinitely to fill the plane, defined by:
  - `AngleDeg` — angle of every line in the family, relative to the X axis.
  - `OriginX`, `OriginY` — a point the first line of the family passes through.
  - `DeltaX`, `DeltaY` — **not** a raw world-space vector. Per the `.pat` format, these are defined in the family's own rotated frame:
    - `DeltaX` ("shift") is measured **along** the line's own direction. It offsets the origin of each successive parallel copy along that direction, which shifts the dash-pattern phase between copies — this is what produces staggered/coursed patterns (e.g. brick running bond).
    - `DeltaY` ("offset") is measured **perpendicular** to the line's direction. It is the spacing between successive parallel copies.
    - To get the world-space step vector: `worldDelta = DeltaX * direction + DeltaY * normal`, where `direction = (cos(angle), sin(angle))` and `normal = (-direction.Y, direction.X)`.
  - `DashPattern` — an ordered list of values repeating periodically along each line, starting at that line's own origin (not wherever the line happens to enter the visible viewport): positive = drawn segment length, negative = gap length, zero = dot (rendered as a small square sized to the stroke thickness). An empty list means a solid, undashed line.
- **Units**: `PatternDefinition.Units` holds the raw `%UNITS=` value (e.g. `"INCH"`, `"MM"`), uppercased, or `null` if never declared. See §9 for how it affects default scale.
- **Tile domain**: not computed in v1. The control does not derive a minimal repeating rectangle; it directly enumerates and clips line copies against the current viewport every render pass (§7).

## 5. Data Model
```csharp
sealed record PatternDefinition(string Name, string? Description, bool IsModel, IReadOnlyList<LineGroup> LineGroups, string? Units = null);
sealed record LineGroup(double AngleDeg, double OriginX, double OriginY, double DeltaX, double DeltaY, IReadOnlyList<double> DashPattern);
```
`PatternDiagnostics` (Success, LineGroupCount, WarningCount, ErrorCount, TileSize, Tileable, ParseDuration, Message) exists as a record in `Model/PatternDefinition.cs` but is **not currently populated or exposed** by the control or parser. It is reserved for the v2 diagnostics work in §13.

## 6. Public Control API (Dependency Properties)
Implemented on `FillPatternPreview` (`Controls/FillPatternPreview.cs`):
- `PatFilePath` (string?) — path to a `.pat` file to parse.
- `PatRawText` (string?) — raw `.pat` text to parse. Takes precedence over `PatFilePath` when both are set.
- `PatPatternName` (string?) — selects a specific pattern by name from a multi-pattern file/text; when omitted, the first parsed pattern is used.
- `LineBrush` (Brush, default `Brushes.Black`) — stroke brush for all rendered lines.
- `Scale` (double, default `1.0`) — world-unit-to-device-pixel multiplier. Coerced via `CoerceValueCallback` to `[0.0001, 1e6]`; NaN becomes `1.0`. A render-time guard (`Math.Max(0.0001, ...)`, non-finite -> 1) remains as a last line of defence.
- `Zoom` (double, default `1.0`) — multiplies with `Scale`. Coerced to the same `[0.0001, 1e6]` range as `Scale` (NaN becomes `1.0`). The narrower 0.1–20 clamp applies only to interactive zoom (§6a); the originally-envisioned 0.1–20 clamp on the DP itself was not adopted.
- `PanOffset` (Point, default `(0,0)`) — shifts the pattern by this amount in the pattern's native units (device offset = `PanOffset * EffectiveScale`), so the visible region of the pattern plane moves under a fixed pattern origin.
- `IsInteractive` (bool, default `false`) — enables the interaction in §6a.
- `UsePatternMaker` (bool, default `false`) — see §8. Leave at its default for accurate previews.
- `Pattern` (PatternDefinition?, read-only) — the currently resolved pattern, or `null` if nothing parsed successfully.

Event: `PatternChanged` — raised whenever the resolved `Pattern` is recomputed (source property changed), regardless of whether parsing succeeded.

Event: `InteractionChanged` — raised after user interaction (§6a) actually changes `Zoom` or `PanOffset`, including a reset. Not raised when the properties are set programmatically.

Not implemented: `PatternSource` enum, `RevitFillPattern`, `StrokeThicknessOverride`, `IsModelPatternOverride`, `RenderMode`, `TileSizeHint`, `ShowBounds`, `ErrorTemplate`/`FallbackVisual`, `SnapsToDevicePixels`, `Diagnostics`. The `ParseFailed` event is not implemented.

## 6a. Interaction
Active only when `IsInteractive` is true. The view maps a pattern point `w` to the device point `(w + PanOffset) * EffectiveScale` (§9). Math lives in `Rendering/InteractionMath.cs`.
- **Wheel**: zoom by `1.1^(delta/120)`, clamped to `Zoom` in `[0.1, 20]`; `PanOffset` is adjusted so the pattern point under the cursor stays under it.
- **Drag** (left button, mouse captured): pans by the pointer delta divided by `EffectiveScale`.
- **Double-click**: resets `Zoom = 1`, `PanOffset = (0,0)`.
- **Keyboard** (control must have focus; a click focuses it): `+`/`-` (main row or numpad) zoom by 1.1x about the control centre; arrow keys pan by 10 device units, moving the pattern in the arrow's direction; `Ctrl+0` resets.
- The 0.1-20 clamp applies to interaction only; setting `Zoom` directly is only coerced to the wider `[0.0001, 1e6]` range (§6).

## 7. Parsing (`PatParser`)
### Input Forms
`ParseText(string)` and `ParseFile(string path)`. Both return a `PatternParseResult { Patterns, Errors, Warnings, ParseDuration }`. `ParseFile` re-reads and re-parses on every call — there is no file-level cache keyed by path/last-write-time/hash.

### File Structure
- **Comments**: a line whose first non-whitespace character is `;` is a full-line comment. An inline `;` on a data or header line truncates the rest of that line as a trailing comment.
- **Units** (`;%UNITS=...`): parsed and stored on `PatternDefinition.Units` (uppercased). Conventionally a file-level declaration appearing before the first pattern header, applying to every pattern that follows; the parser tracks the most recently seen value and also honors it if repeated per-pattern (after a `*Name` header, mirroring `%TYPE`). See §9 for how this drives default scale.
- **Header**: `*name[, description]` starts a new pattern. Any preceding open pattern is committed first (see Duplicate names below).
- **Type declaration** (`%TYPE=MODEL` / `%TYPE=DRAFTING`): recognized both as a trailing comment on the header line itself (`*Name ;%TYPE=MODEL`) and — the far more common real-world form, matching `Constants.PAT_FILE_TEMPLATE`'s own output — as its own standalone comment line immediately following the header. An unrecognized `%TYPE=` value is recorded as a warning and does not change `IsModel`. Absent any `%TYPE=` tag, `IsModel` defaults to `false` (drafting).
- **Definition lines**: comma-separated tokens, `AngleDeg, OriginX, OriginY, DeltaX, DeltaY[, dash1, dash2, ...]`. At least 5 numeric tokens are required; the dash list may be empty (solid line) or arbitrarily long (not limited to exactly one dash + one gap). All tokens are parsed with `double.TryParse` using `NumberStyles.Float | NumberStyles.AllowThousands` and `CultureInfo.InvariantCulture`. A line with fewer than 5 tokens, or any unparsable token, is recorded as an error and skipped — it does not abort the rest of the file.
- **Duplicate pattern names**: **the last definition wins**, replacing any earlier one under the same name, with a warning recorded. (This is the opposite of "first wins" — if first-wins semantics are ever required, this is a one-line change in `CommitCurrent`.)
- **Limits** (hostile/corrupt input; all far above real files): input <= 4 MiB (`ParseFile` checks the file size before reading); line <= 8192 chars; <= 10,000 patterns (parsing stops with an error); <= 512 definition lines per pattern and <= 64 dash entries per line (extra/over-long lines are skipped with an error). Any number that is NaN, Infinity, overflows on parse, or has |value| > 1e9 makes that line an error. There is no dash-length *clamping* with a warning - values within the range are used as-is.
- A pattern with zero definition lines is committed anyway (so it appears in `Patterns`) with a warning, rather than being dropped.

## 8. Rendering
`FillPatternPreview.OnRender` fills the background, then dispatches to exactly one of two paths based on `UsePatternMaker`. **`RenderLegacy` is the default and the only path verified to faithfully reproduce arbitrary `.pat` content; it is the one described by this spec.**

### RenderLegacy (default, `UsePatternMaker = false`)
For each `LineGroup`, every render pass:
1. Compute `direction = (cos(angleRad), sin(angleRad))` (skip the group if this is degenerate) and `normal = (-direction.Y, direction.X)`.
2. Compute the world-space step vector per §4: `delta = (direction*DeltaX + normal*DeltaY) * scale`, where `scale = max(0.0001, Scale*Zoom)`.
3. Spacing between copies = `|delta · normal|`, with fallbacks (`delta.Length`, then a fixed `8*scale`) for degenerate zero-spacing input.
4. Determine the range of repeat indices `k` needed to cover the visible control rect (projected onto `normal`), capped at 4000 repeats per family as a performance guard (families requiring more are skipped entirely rather than truncated). This range must be derived from the *signed* step `delta · normal` (how much the projection actually changes per unit `k`), not from its absolute value (the spacing magnitude) — the two have different signs whenever a family's angle/offset combination makes a positive `k` step decrease the projection. Using the absolute value here silently clamps the usable range to only 2-3 repeats in that case (a positive-only band near `k=0`), rendering just the first few copies of the family and none beyond, until fixed.
5. For each `k`, compute `basePoint = origin + k*delta` (the true origin of that copy) and intersect the infinite line through `basePoint` in `direction` with the control's rect to get a visible chord `(p1, p2)`. This intersection is ordered so that `p2` is always further along `+direction` than `p1` — i.e. walking from `p1` by `direction*t` for increasing `t` moves toward `p2`, never away from it. (This ordering is *required* for correct dashing — a solid line looks the same either way, but a dashed line walked backwards is pushed entirely outside the render clip and disappears. This exact bug affected every family whose direction pointed "backwards" relative to a naive left→right/top→bottom intersection order, e.g. 180° and -90° families, until fixed.)
6. Expand the dash pattern along `(p1, p2)`, with its phase anchored to `basePoint` (not to `p1`) so that copies shifted by the "shift" component of `delta` show the correct staggered phase relative to each other. A dash cycle scaling to less than 1 device pixel is drawn as a solid line instead of being walked segment-by-segment — dashes finer than a pixel aren't individually resolvable anyway, and iterating them across a long visible chord (common for drafting patterns previewed before their units-based scale is applied, §9) could require millions of draw calls and hang the UI thread. A hard cap of 20,000 dash segments per visible chord is a second, defense-in-depth backstop against any other pathological combination (e.g. an extreme `Zoom`).

Stroke thickness is fixed at `1` device unit; there is no `StrokeThicknessOverride`. `LineBrush` controls color only.

### RenderWithPatternMaker (opt-in, `UsePatternMaker = true`) — experimental, not spec-conformant
See §8 in the codebase's `PatternMaker/` folder (`PatternDomain`, `PatternSafeGrid`, `PatternGrid`, `PatternMakerAdapter`). This is a port of a *pattern-authoring* algorithm — given a target tile domain, it snaps to the nearest angle from a generated set of "safe" tileable angles (the kind of tool used to synthesize a new custom pattern that Revit can tile cleanly), not a renderer of an already-authored pattern's exact geometry. Concretely, it currently:
- Collapses every `LineGroup` in the pattern into one shared bounding domain (from the single largest `|Delta|` across all groups), discarding each family's individual spacing.
- Re-derives each family's angle by snapping to the nearest "safe" angle for that shared domain, discarding the family's actual angle when they don't already coincide.
- Draws solid, undashed lines only — `DashPattern` is ignored entirely.

Because of this, enabling `UsePatternMaker` on an arbitrary existing `.pat` (especially multi-family patterns like a brick detail) does not reproduce the source pattern; it currently exists as an experimental path only. **Do not enable it for accurate preview.** Recommendation: either remove this path, or rework it as a clearly separate "pattern authoring helper" feature outside the `FillPatternPreview` control's rendering contract, so a future default-flip or accidental `UsePatternMaker = true` (as happened during development of this spec) can't silently produce incorrect previews again.

## 8a. Thumbnail export (`PatternThumbnail`)
Headless PNG generation with no control, window or `Application`: `FillPatternPreview.Imaging.PatternThumbnail` (static) with `PatternThumbnailOptions` (sealed record; all properties defaulted).
- **Shared drawing**: the control and the exporter both draw through `Rendering/PatternRenderer` (a `DrawingContext` renderer: clip, pan, tile/first-tile highlight, `RenderLegacy`), and both choose the pattern through `Rendering/PatternResolver` (raw text beats file; named pattern else first; tile size from `PatternTileCalculator`). A thumbnail with the same scale/zoom/pan/colours is pixel-identical to the control (tested). The experimental PatternMaker path is control-only.
- **API**: `Render(PatternDefinition, options)` -> frozen `BitmapSource` (Pbgra32); `RenderPng(PatternDefinition | patText, patternName, options)` -> `byte[]`; `RenderPngFromFile(path, patternName, options)`; `SavePng(pattern, Stream | path, options)`.
- **Options**: `Width`/`Height` (DIPs, default 128), `Dpi` (default 96; pixel size = DIPs x Dpi/96, stored in the PNG), `Background` (null = transparent, default white), `LineBrush`, `FirstTileBrush` (set = highlight first tile), `TilePattern`, `TilesAcross` (default 3), `Scale` (null = fit), `Zoom`, `PanOffset`.
- **Scaling**: an explicit `Scale` wins (as the control's `Scale`). Otherwise the tile's longer side is sized to `Min(Width, Height) / TilesAcross` (with `TilePattern = false`, to the whole image) so patterns of any tile size stay legible; patterns with no repeat cell use `Scale = 1`. `Zoom` multiplies on top. Scale/Zoom/Pan are coerced exactly as the control's DPs.
- **Errors**: unlike the control, the exporter throws: `ArgumentNullException`/`ArgumentException` for a null or unreadable source, `FileNotFoundException` for a missing file, `ArgumentOutOfRangeException` for options out of range (Width/Height >= 1, 0 < Dpi <= 1200, TilesAcross > 0, resulting size <= 4096 px per side). The same `RenderBudget` bounds work; an exhausted budget yields a partial image, not an error.
- **Threading**: `RenderTargetBitmap` needs an STA thread. On a non-STA caller the work runs on a short-lived STA thread and the call blocks until done, so it is safe from any thread and in parallel. Caller brushes are snapshotted (frozen copies) on the calling thread; brushes owned by another thread cannot be read, so frozen brushes are recommended.

## 9. Transforms & Units
- `EffectiveScale = Scale * Zoom * UnitsToDipFactor(pattern)`, applied uniformly to all coordinates read from the pattern.
- `UnitsToDipFactor` (`GetUnitsToDipFactor` in `FillPatternPreview.cs`) is `1.0` for **model** patterns — their native units are real-world/model size, and their "correct" apparent size is inherently a matter of view zoom, not a fixed conversion, so `Scale`/`Zoom` are the only controls and default to a plain 1-native-unit-to-1-DIP mapping.
- For **drafting** patterns, native units are paper/plot units by convention (inches unless `%UNITS=` says otherwise), and WPF's own device-independent unit is defined as exactly 1/96 inch — so `UnitsToDipFactor` converts the pattern's declared unit to inches and multiplies by 96, giving a "print-true" default: at `Scale=1` (its default), a drafting pattern renders at the same size it would print at 100%, with **no per-pattern manual scale calculation needed**. Recognized units: `INCH` (default when `%UNITS=` is absent, matching AutoCAD/Revit convention) → `96`; `MM` → `96/25.4`; `CM` → `96/2.54`; `M`/`METER` → `96/0.0254`; `FOOT`/`FT` → `96*12`. An unrecognized unit string falls back to the inch factor.
- `Scale` and `Zoom` still apply on top of this as user-controlled multipliers (e.g. for interactive zoom) — the units factor only establishes the *default* (`Scale=1, Zoom=1`) baseline.
- `PanOffset` is in pattern-native units and translates the rendered pattern by `PanOffset * EffectiveScale` device units (see §6, §6a).
- No DPI-awareness: stroke thickness is not adjusted for `VisualTreeHelper.GetDpi` or `SnapsToDevicePixels` (the latter isn't implemented as a DP at all) — the 96-DIP-per-inch assumption above is a fixed constant, not the *actual* screen DPI, which is what "print-true at 100% zoom on a 96 DPI-normalized WPF surface" means in practice (WPF already normalizes DIPs this way regardless of physical monitor DPI).

## 10. Error Handling
- File not found, unreadable, locked, or too large: `ParseFile` returns a result with an error message (IO, permission, and invalid-path exceptions are caught inside it). The control additionally catches anything thrown while loading a pattern (result: `Pattern == null`) and anything thrown from `OnRender` (result: pattern left undrawn), logging to `Debug.WriteLine` - an exception escaping either would otherwise be an unhandled UI-thread exception in the host app.
- `Scale` and `Zoom` are coerced to `[0.0001, 1e6]` (NaN becomes 1) and `PanOffset` to finite values within +/-1e9, so a bad binding can't drive the renderer into pathological work.
- Malformed definition line: recorded as an error and skipped; parsing continues. There is currently no "if all lines invalid, fail the whole pattern" special case — a pattern with zero valid line groups is still added to `Patterns` with a warning (§7).
- No `DesignerProperties.GetIsInDesignMode` guard exists; the try/catch around parsing incidentally covers most design-time failure modes today.

## 11. Testing
`tests/FillPatternPreview.Tests` (xUnit, `net8.0-windows`, referenced by `PatPreviewControl.sln`) covers:
- **`ParsingTests.cs`**: headers with/without description, `%TYPE` on both the header line and a standalone comment line, `%UNITS` at file level and per-pattern override, full-line and trailing comments, malformed lines (too few tokens, unparsable numbers) recorded as errors without aborting the file, duplicate-name last-wins behavior, `PatPatternName` selection, invariant-culture number parsing (verified under a comma-decimal thread culture), an all-comment/empty pattern, and a missing file.
- **`LineFamilyExpanderTests.cs`**: regression tests for every rendering bug found and fixed getting real `.pat` files (ANSI31 hatches, an English-bond brick detail, a soldier-bond brick pattern, and a drafting "Concrete" scatter pattern) to render correctly - the shift/offset-into-world-space rotation, the repeat-range sign bug (verified to actually fail when the bug is reintroduced, not just tautologically pass), the intersection-chord ordering fix across 8 angles, dash-phase anchoring to the family's true origin rather than the rect-clip point, the sub-pixel-cycle solid-line shortcut and the hard segment cap (both hang fixes), exact dash/gap/dot segment boundaries, and the units-to-DIP scale factor for both model and drafting patterns.

- **`ThumbnailTests.cs`**: `PatternResolver` selection rules, and `PatternThumbnail` (§8a): PNG signature and size (including DPI), non-blank/non-solid output for tiny/huge/dashed tiles, transparent background, fit-N-tiles pitch, explicit-scale override, pixel-for-pixel match against the control, first-tile highlight and single-tile bounds, unfrozen caller brushes, MTA/STA/parallel callers, hostile pattern/scale/zoom/pan, stream/file/named-pattern output, and argument validation.

To make the pure rendering math testable at all, it was extracted from `Controls/FillPatternPreview.cs` (a WPF `Control`, awkward to unit test directly) into `internal static class LineFamilyExpander` in `Rendering/LineFamilyExpander.cs`, with `dc.DrawLine`/`dc.DrawRectangle` calls replaced by a `DashSegment` sequence the control then draws. The test project sees these `internal` members via `[InternalsVisibleTo("FillPatternPreview.Tests")]` on the main project.

Run with `dotnet test` (or target the project directly: `dotnet test tests/FillPatternPreview.Tests/FillPatternPreview.Tests.csproj`).

Not yet covered, left for v2: visual/pixel-diff tests via `RenderTargetBitmap` (harder to keep stable across DPI/font-rendering differences than the geometry-level tests above, which check exact computed coordinates instead); the `RenderWithPatternMaker`/`PatternMaker` path (deliberately - see §8, it's flagged for removal/rework rather than being tested as-is); CI wiring to run `dotnet test` automatically.

## 12. Security & Reliability
Render-time work is bounded three ways: the per-family repeat cap and per-chord dash-segment cap (§8), and a **per-render-pass `RenderBudget` of 250,000 units** (one per visible chord and per drawn dash segment) that caps the total across all families - when it runs out the rest of the pattern is simply not drawn. The stroke `Pen` and a snapshot of its brush are frozen before drawing: with an unfrozen pen, WPF's per-draw-call bookkeeping made cost grow quadratically (a 512-family pattern didn't finish in 30 s even with the budget lifted, because the cost was in recording draw calls, not generating them). Repeat-index math clamps before casting to `int` and compares in `long`, because an out-of-range double-to-int cast can wrap and turn "skip this family" into a multi-billion-iteration loop. Remaining gaps: parsing and file IO are synchronous on the UI thread (bounded by the limits in §7, but a hung network path can still block), and there is no cancellation.

No external network IO. File access is limited to the path the caller supplies. No explicit bound on total lines parsed or total repeats rendered beyond the per-family 4000-repeat cap in §8 (a pattern with very many families, each near that cap, has no aggregate cap) and the per-chord 20,000-dash-segment cap (§8). Before that dash-segment cap and the sub-pixel solid-line shortcut were added, a legitimate drafting `.pat` previewed before its units-based scale (§9) was applied — i.e. any drafting pattern rendered at `Scale=1` prior to that feature — could hang the UI thread indefinitely: native dash values far smaller than a device pixel, multiplied across many line groups and a wide viewport, drove dash-expansion into the hundreds of millions of draw calls. Both issues are now mitigated, but there is still no aggregate cross-family work budget, so a sufficiently adversarial combination of many families each individually under both caps remains a theoretical slow-render risk.

## 13. Deferred to v2
Preserved from the original design draft as a roadmap — **none of the following is implemented today**:
- **Multi-source input**: `PatternSource` enum, `RevitFillPattern` reflection adapter (detect `Autodesk.Revit.DB.FillPattern`, extract `IsModel`/name/segments without a hard assembly reference), internal-model source.
- **Tiled/cached rendering**: compute a minimal repeating tile domain per pattern, build a frozen `DrawingBrush { TileMode=Tile }` once and reuse it, with a geometry cache (LRU, ~16 entries) keyed by pattern hash + stroke-thickness bucket + tile-size hint, falling back to immediate mode when a tile is non-periodic or exceeds a size cap (e.g. 4096 px).
- **Diagnostics**: populate and expose the existing `PatternDiagnostics` record via a `Diagnostics` DP; raise `ParseFailed` on failed parses; a static "last diagnostics" helper for tooling.
- **Accessibility**: custom `AutomationPeer` exposing pattern name/description/`IsModel`/diagnostic summary.
- **Extensibility**: `IFillPatternSource`, `IPatternRenderer` strategy interfaces; pluggable logger for parse/render lifecycle events.
- **Parser correctness/robustness gaps carried forward from §7**: first-wins duplicate-name resolution (currently last-wins), dash-length clamping with warning, file-level parse cache keyed by (path, last-write-time, content hash).
- **DP coercion**: done for `Scale`/`Zoom`/`PanOffset` (§6). Not done: a tighter `Zoom` range (e.g. 0.1–20) on the property itself.
- **`StrokeThicknessOverride`**, DPI-aware stroke alignment via `SnapsToDevicePixels`.
- Resolution of §8's `PatternMaker` path: either remove it, or redefine it as an explicitly separate feature (e.g. a pattern-authoring helper) with its own opt-in surface, so it can no longer be mistaken for — or accidentally wired up as — the control's primary rendering path.

---
End of Specification.
