# Specification: FillPatternPreview WPF Control

## 1. Purpose
Provide a reusable WPF control (FillPatternPreview) that previews AutoCAD/Revit style hatch (fill) patterns from multiple input sources (.pat file/text, Revit FillPattern object via reflection, or internal model). Supports both drafting (paper) and model (world) patterns with performant tiling, zoom, pan, and diagnostics.

## 2. Scope
In scope: Parsing .pat text, computing tile geometry, rendering via DrawingBrush (tiled) or immediate drawing fallback, dependency properties, async parsing, diagnostics, limited interaction (zoom/pan), optional Revit reflection adapter, caching, error handling, design-time sample.
Out of scope (v1): Complex embedded shape patterns, editing, GPU/D2D acceleration, streaming very large libraries.

## 3. Key Goals & Success Criteria
- Visual fidelity: ±1 px vs reference for common ANSI/Revit patterns across typical DPI scales.
- Parse typical (<200 lines) pattern file: <50 ms cold, <5 ms warm cache.
- Render 200×200 px preview (cached tile): <5 ms.
- Robust against malformed input (no UI thread crash; surfaced diagnostics).
- No hard dependency on Revit assemblies unless FillPattern object provided.

## 4. Core Concepts
PatternDefinition: name, description, IsModel flag, line groups.
LineGroup: angle (deg), origin (x,y), offsets (delta-x, delta-y), dash sequence (positive=segment, negative=gap, zero=dot).
Tile: Minimal repeating domain bounding all line groups (approx periodicity from deltas + angle projection). Used to build DrawingBrush.
Immediate Mode: Direct on-viewport line expansion when tile too large / non-periodic / extreme transforms.
Caching: Geometry & brush structures keyed by pattern hash + stroke thickness bucket + tile size (if hinted).

## 5. Data Model
```
record PatternDefinition(string Name, string? Description, bool IsModel, IReadOnlyList<LineGroup> LineGroups);
record LineGroup(double AngleDeg, double OriginX, double OriginY, double DeltaX, double DeltaY, IReadOnlyList<double> DashPattern);
record PatternDiagnostics(
  bool Success,
  int LineGroupCount,
  int WarningCount,
  int ErrorCount,
  Size? TileSize,
  bool Tileable,
  TimeSpan ParseDuration,
  string? Message);
```

## 6. Public Control API (Dependency Properties)
- PatternSource (enum None, PatFile, PatText, FillPatternObject, InternalModel)
- PatFilePath (string?)
- PatRawText (string?)
- PatPatternName (string?)
- RevitFillPattern (object?)
- Scale (double, default 1.0, >0)
- Zoom (double, default 1.0, clamp 0.1–20)
- PanOffset (Point, default 0,0)
- LineBrush (Brush, default SystemColors.ControlTextBrush)
- Background (Brush, default Transparent)
- StrokeThicknessOverride (double?, >0)
- IsModelPatternOverride (bool?)
- RenderMode (enum Immediate, CachedBitmap, Auto)
- TileSizeHint (Size?)
- ShowBounds (bool)
- ErrorTemplate (DataTemplate?) or FallbackVisual
- IsInteractive (bool)
- SnapsToDevicePixels (bool)
- Pattern (read-only PatternDefinition?)
- Diagnostics (read-only PatternDiagnostics?)
Events: PatternChanged, ParseFailed(PatternErrorEventArgs), InteractionChanged.

## 7. Parsing (.pat)
### Input Forms
- File path with optional pattern name, raw text, internal model, or Revit FillPattern reflection.

### .pat File Structure (Revit conventions)

#### Units Declaration (Optional)
- `;%UNITS=[value]`
  - Specifies the units for the pattern (e.g., INCH, MM).
  - If omitted, default units are assumed (typically inches for drafting, model units for model patterns).

#### Header
- Each pattern begins with a header line: `*pattern-name, [optional description]`
  - The asterisk `*` marks the start of a new pattern.
  - `pattern-name` is the identifier (no spaces).
  - The description is optional and follows a comma.

#### Type Declaration
- `;%TYPE=[value]`
  - Indicates the type of pattern (MODEL or DRAFTING).

#### Pattern Descriptors
- Each subsequent line defines a hatch line group:
  - Format: `angle, x-origin, y-origin, shift, offet, dash, space`
    - `angle`: Angle of the lines in degrees (relative to X-axis).
    - `x-origin`, `y-origin`: Starting point for the line group.
    - `shift`: With a SHIFT value of 0 the lines we created with OFFSET are all aligned with each other. As each line is repeated, the SHIFT values move the origin of each individual line long its axis. This has no impact on solid lines, but lines with DASH and SPACE values can be shifted dramatically. 
    - `offset`: Offset to repeat the line group (delta-x, delta-y).
    - `dash`: Length of line drawn (positive = drawn, negative = gap, zero = dot).
    - `space`: Distance between lines in the group (positive = drawn, negative = gap).
    
- All values are comma-separated and use invariant culture (period as decimal separator).
- At least 5 numeric values are required per line after the header.

#### Comments
- Lines starting with `;` are comments and ignored.
- Inline comments (after data) are also supported using `;`.

#### Validation & Rules
- Each pattern must have at least one valid data line.
- Dash list can be empty (solid line).
- Zero dash value means a dot (rendered as a short segment, length = stroke thickness).
- Extremely large dash values are clamped (default MaxSegment = 10,000) with a warning.
- Malformed lines are skipped and recorded as errors; if all lines are invalid, the pattern fails to parse.
- Duplicate pattern names are allowed but only the first is used unless a specific name is requested.

#### Caching
- File cache is keyed by (full path, last write time, content hash) and stores parse results.

#### Result
- Parsing produces a `PatternParseResult`:
  - `Success`, `Errors`, `Warnings`, `Dictionary<string, PatternDefinition>`

## 8. Revit FillPattern Adapter (Reflection)
- Detect Autodesk.Revit.DB.FillPattern type
- Extract: IsModel, Name, pattern segments (angle, origin, shift, dash array)
- Normalize to internal LineGroup list
- If assembly not loaded or members missing -> warning diagnostic

## 9. Tile Computation
Algorithm Outline:
1. For each line group: Determine fundamental repeat vectors using (DeltaX, DeltaY). If zeros/near-zero, approximate based on dash aggregate length or fallback constant.
2. Collect extents: max |DeltaX|, |DeltaY| across groups (exclude near-zero <1e-6) as base bounds.
3. If insufficient periodic offset (all near-zero), synthesize minimal tile (e.g., max dash magnitude or default 10×10).
4. Expand bounds slightly (stroke thickness / 2) for safe edge drawing.
5. Mark Tileable=false if any group lacks periodic offset and dash pattern cannot imply tiling (e.g., entirely zero-length with no offset variety) or computed tile exceeds hard cap (e.g., >4096 in either dimension).
6. If Tileable=false -> Immediate mode fallback (unless explicitly forced).

## 10. Rendering
Tiled Path:
- Enumerate lines intersecting tile rectangle + margin.
- For each line group:
  - Determine line orientation (angle rad; direction and perpendicular vectors).
  - Compute line spacing along perpendicular using projected offset vectors.
  - Generate repeating line start points across tile.
  - Expand dash pattern along direction: positive => segment geometry; negative => skip; zero => dot (short segment length = stroke thickness, center aligned).
- Combine into GeometryDrawing(s) with shared Pen. Freeze.
- Wrap in DrawingBrush { TileMode=Tile, Viewport=tile size, ViewportUnits=Absolute }. Apply Transform = Scale * Zoom and Pan (TranslateTransform or MatrixTransform adjusting viewport origin).
Immediate Mode:
- At OnRender, compute visible world rect = (ActualWidth, ActualHeight) inverse transform.
- Enumerate only necessary lines (clamp count) and draw via DrawingContext.DrawLine / mini Geometries.
- Use same dash expansion logic.

## 11. Transforms & Units
- EffectiveScale = Scale * Zoom.
- Model patterns: coordinate values remain world scaled through EffectiveScale.
- Drafting patterns: treat numeric values as paper units; EffectiveScale applied uniformly.
- PanOffset applied post scale (Translate after scale matrix).
- DPI: Acquire via VisualTreeHelper.GetDpi; optionally adjust stroke thickness to align device pixels when SnapsToDevicePixels.

## 12. Performance & Limits
- Max tile dimension default: 4096 px; exceed => fallback immediate.
- Max lines per group per tile: 10,000 (truncate + warning).
- Geometry cache size: 16 entries (LRU). Evict least recently used on overflow.
- Pattern hash = stable serialization of: Name, IsModel, rounded line group numeric values (1e-6), dash arrays.
- Use Freezable objects (Brush, Geometry, Pen) to reduce overhead.

## 13. Caching Strategies
Entries keyed by (PatternHash, StrokeThicknessBucket, TileSizeKey). Stroke thickness bucket = round(thickness * 100)/100.
Color changes: reuse geometry; create tinted clone by replacing Pen.Brush at render time or using a cached Pen per color.
Invalidate Geometry Cache on: Pattern change, Scale change (affects tile?), StrokeThickness change, TileSizeHint change.

## 14. Diagnostics
Populate PatternDiagnostics:
- Success flag (true if ?1 valid line group)
- Counts (line groups, warnings, errors)
- Computed TileSize & Tileable
- ParseDuration
- Message (summary or first error)
Events: ParseFailed fired once per failed attempt (Success=false).
Expose static helper to retrieve last diagnostics instance for global tools.

## 15. Interaction (If IsInteractive)
- Mouse Wheel: Zoom around cursor (Ctrl modifier optional future)
- Mouse Drag (Left or Middle): Pan (update PanOffset)
- Double-click: Reset Zoom=1, PanOffset=0
- Keyboard: + / - (zoom increment 10%), Arrow keys (pan by 10 device-independent units * (1/Zoom)), Ctrl+0 reset
- Raise InteractionChanged on state modifications

## 16. Error Handling
- File not found -> Diagnostics.ErrorCount>0; message; no throw (except invalid DP argument usage).
- Malformed line: record error, skip; if all invalid -> Fail.
- Argument coercion: Scale/Zoom <=0 -> coerce to minimum (0.1 for Zoom, 1e-6 for internal math) or throw when set via CLR property (DP coercion path safe).
- All try/catch around parsing & rendering path for design mode (DesignerProperties.GetIsInDesignMode) to prevent crashes.

## 17. Accessibility
Custom AutomationPeer returning: Name (pattern name), Description, IsModel, Diagnostic summary. Provide AutomationProperties.Name fallback to PatternDefinition.Name.

## 18. Logging / Extensibility
Optional injection (static property or service locator) of ILogger-like interface for parse/render events (start, success, failure). Extension interfaces:
- IFillPatternSource (Resolve PatternDefinition asynchronously)
- IPatternRenderer (Strategy for custom rendering)

## 19. Testing Outline
Parsing Tests: headers, multiple patterns, comments only, malformed tokens, negative/zero dashes, large numbers, duplicate names.
Rendering Tests (Golden / pixel diff tolerance 1 px): dense spacing, sparse, extreme zoom, pan, DPI 100/150/200.
Performance Benchmarks: parse time, warm cache reuse, rendering tile vs immediate.
Interaction: zoom focal point retention, reset, boundaries.
Error Resilience: invalid file, empty file, random fuzz lines.
Cache Behavior: LRU eviction after >16 unique patterns.

## 20. Security & Reliability
No external network IO. File access limited to provided path. Avoid unbounded memory by limiting lines and tile size. Defensive parsing against overflow (double.TryParse with range checks).

## 21. Open Issues
- Clarify tolerance for floating periodic detection (current draft: near-zero <1e-6; rounding for hash 1e-6). Might need configurable.
- Potential support for fallback sample pattern selection (ANSI31 hardcoded vs small embedded resource) – finalize.
- RenderMode.Auto heuristics (initial rule: use tile if Tileable && tile <= 2048 else immediate).

## 22. Development Phases
Phases: Parser, Control skeleton, Rendering & Cache, Revit Adapter, Interaction & Diagnostics, Tests/Polish.

## 23. Acceptance Criteria Mapping
Covers: visual fidelity (Sections 9–10, 12), error surfacing (14,16), performance (12), interaction (15), stability (16).

## 24. Implementation Notes
- Prefer immutable records for model (already defined) to ease hashing.
- Consider Source Generators later for DP boilerplate (out of scope v1).
- Use ConfigureAwait(false) for background parse tasks; marshal results via Dispatcher.
- Freeze all Freezables when possible.

---
End of Specification.
