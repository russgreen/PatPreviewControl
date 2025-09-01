# PRD: WPF Fill Pattern Preview Control

## 1. Overview
Create a reusable WPF control (`FillPatternPreview`) that previews AutoCAD/Revit fill (hatch) patterns. Input sources:
1. Path to a `.pat` file (may contain multiple pattern definitions)
2. Raw `.pat` text (already loaded content)
3. An in?memory Revit API `FillPattern` (late-bound via reflection)
4. A pre-parsed internal pattern model

The control must render both drafting (screen scale) and model (world scale) patterns, support zoom/pan, scaling overrides, theming, caching, high-DPI correctness, and graceful error handling.

## 2. Objectives / Success Criteria
- Load and display any valid `.pat` pattern (ANSI, custom, multi-line groups) with visual parity (±1 px) vs Revit reference for common patterns.
- Parse typical pattern files (<200 lines) in <50 ms cold, <5 ms warm (cache hit).
- Render a 200×200 px preview in <5 ms after parse using cached tiling path.
- Handle malformed `.pat` gracefully (diagnostics surfaced, no crashes).
- Zero hard dependency on Revit assemblies unless Revit `FillPattern` object provided.
- Thread-safe background parsing with marshalled UI updates.

## 3. Primary Use Cases
- Design-time preview in property grid / custom pattern picker dialog.
- Inline preview in dropdown / list (thumbnail mode).
- Large inspection canvas with interactive zoom & pan.
- Batch gallery generation (e.g., export thumbnails).
- Comparing selected vs hovered pattern (multiple instances).

## 4. Personas
- CAD/BIM plugin developer integrating pattern selection UI.
- Content author validating custom hatch patterns.
- End user choosing patterns in application dialogs.

## 5. Functional Requirements
### 5.1 Inputs (Dependency Properties)
- PatternSource (enum: None, PatFile, PatText, FillPatternObject, InternalModel)
- PatFilePath (string)
- PatRawText (string)
- PatPatternName (string) – choose named pattern from multi-definition file
- RevitFillPattern (object) – late-bound; ignored if type unavailable
- Scale (double) – uniform multiplier (default 1.0)
- LineBrush / LineColor (Brush/Color) – stroke color
- Background (Brush)
- StrokeThicknessOverride (double?)
- IsModelPatternOverride (bool?)
- Zoom (double, default 1.0, clamp 0.1–20)
- PanOffset (Point, default 0,0)
- RenderMode (enum: Immediate, CachedBitmap, Auto)
- TileSizeHint (Size?) – preferred base tile size for generation
- ShowBounds (bool)
- ErrorTemplate (DataTemplate) or FallbackVisual
- IsInteractive (bool)
- SnapsToDevicePixels (bool)
- Pattern (read-only internal model)
- Diagnostics (read-only structure)

### 5.2 Rendering Behavior
- Parse pattern into internal model: collection of LineGroup(s).
- LineGroup: angle (deg), base origin (x,y), offset (delta-x, delta-y), dash pattern (double[]; positive=segment, negative=gap, zero=dot), repetition determined by viewport.
- For model patterns: spacing scaled by world units (apply `Scale * Zoom`). Drafting patterns treat values as paper units (apply `Scale * Zoom` uniformly).
- Compute minimal repeating tile from periodicities of line groups (LCM approximation with tolerance for deltas).
- Tile rendering path: Build `DrawingGroup` once, wrap in `DrawingBrush` (TileMode=Tile). Adjust brush transforms for Zoom/Scale/Pan.
- Immediate path: Generate required lines per frame (used when non-tileable or extreme transforms).
- High DPI aware: account for `VisualTreeHelper.GetDpi`.
- Optional bounds diagnostic overlay.

### 5.3 Pattern Parsing (.pat)
- Support comments (lines beginning with `;`).
- Header format: `*NAME, optional description`.
- Definition lines: `angle, x-origin, y-origin, delta-x, delta-y, dash1, dash2, ...`.
- Multiple pattern definitions per file; selection by `PatPatternName` (case-insensitive).
- Culture-invariant numeric parsing. Whitespace tolerant.
- Validation: numeric count >= 5; dashes optional; empty dash list => solid line.
- Zero values in dash pattern => dot (render minimal segment length = stroke thickness).
- Result object `PatternParseResult`: Success, Errors(List), Warnings(List), Patterns(Dictionary<string, PatternDefinition>).
- File cache keyed by (full path, last write time) + hash of content.

### 5.4 Revit FillPattern Integration
- Reflection detection of `Autodesk.Revit.DB.FillPattern`.
- Extract: IsModel, Name, Segments (angle, origin, shift, dash pattern).
- Convert to internal model; mark `IsModel` accordingly.
- If reflection fails (assembly not loaded), emit warning.

### 5.5 Interaction (Optional)
- Mouse wheel: zoom around cursor (Ctrl+Wheel optional modifier).
- Mouse drag (left) or middle button: pan.
- Double-click: reset Zoom=1, PanOffset=(0,0).
- Keyboard (if focus + IsInteractive): `+` / `-` zoom; arrow keys pan; `Ctrl+0` reset.
- Routed events: `PatternChanged`, `ParseFailed`, `InteractionChanged`.

### 5.6 Error Handling
- Invalid path: Diagnostics state = Error(FileNotFound).
- Parse errors: aggregate; skip failing lines; if no valid lines => show ErrorTemplate.
- Invalid numeric / overflow: clamp or discard with warning.
- Provide helper `GetLastDiagnostics()`.

### 5.7 Design-Time Support
- If no pattern set (and not running) provide built-in sample (e.g., ANSI31) to show control shape.
- Catch all exceptions inside design mode (avoid designer crash).

## 6. Non-Functional Requirements
- Performance: Parse <50 ms cold typical; cached pattern render <2 ms (tile path) at 200×200.
- Memory: Single `DrawingGroup` per pattern variant; LRU cache size configurable (default 16 patterns).
- Threading: Parsing done on background Task; apply result via Dispatcher.
- Accessibility: `AutomationPeer` exposes Name (PatternName), Description, IsModel status.
- Theming: Respects system theme; brushes updatable via resources.
- Testability: Parser pure functions; rendering deterministic for given DPI & inputs.
- Logging: Optional `ILogger` injection or simple interface for diagnostics sink.

## 7. Public API Sketch
```csharp
public sealed class FillPatternPreview : Control
{
    // Dependency properties (DP registration omitted for brevity)
    public string? PatFilePath { get; set; }
    public string? PatRawText { get; set; }
    public string? PatPatternName { get; set; }
    public object? RevitFillPattern { get; set; }
    public double Scale { get; set; }
    public double Zoom { get; set; }
    public Point PanOffset { get; set; }
    public Brush LineBrush { get; set; }
    public Brush Background { get; set; }
    public bool ShowBounds { get; set; }
    public RenderMode RenderMode { get; set; }
    public PatternDefinition? Pattern { get; }
    public PatternDiagnostics? Diagnostics { get; }

    public event EventHandler? PatternChanged;
    public event EventHandler<PatternErrorEventArgs>? ParseFailed;
}
```
Internal model:
```csharp
public sealed record PatternDefinition(string Name, string? Description, bool IsModel, IReadOnlyList<LineGroup> LineGroups);
public sealed record LineGroup(double AngleDeg, double OriginX, double OriginY, double DeltaX, double DeltaY, IReadOnlyList<double> DashPattern);
```
Diagnostics:
```csharp
public sealed record PatternDiagnostics(
    bool Success,
    int LineGroupCount,
    int WarningCount,
    int ErrorCount,
    Size? TileSize,
    bool Tileable,
    TimeSpan ParseDuration,
    string? Message);
```

## 8. Rendering Approach
1. On pattern load/DP change: compute canonical tile bounds from group periodicities (approximate LCM using tolerance for floating values). If bounds exceed safety threshold (e.g., > 4096 px), fall back to immediate mode.
2. Build geometry: For each line group, compute direction (angle) vector and perpendicular spacing; enumerate lines covering tile + margin (1 stroke beyond edges for anti-alias correctness).
3. Dash expansion: iterate dash pattern sequence; positive => draw; negative => skip; zero => minimal segment (dot) length = stroke thickness.
4. Compose into `DrawingGroup`; freeze; wrap into `DrawingBrush` with `Viewport`=tile size, `TileMode=Tile`.
5. During `OnRender`, apply transform matrix (Zoom * Scale) and pan (adjust brush viewport origin or apply translate transform). For Immediate mode, iterate bounding lines just for current viewport.
6. Cache geometry keyed by (pattern hash, stroke thickness bucket, tile size). Re-color by cloning Pen or using `DrawingGroup` + `ReplaceBrush` routine.

## 9. Caching Strategy
- Pattern hash: stable serialization (rounded doubles to 1e-6) + name + model flag.
- LRU cache: Dictionary + LinkedList or `ConcurrentDictionary` + usage queue (UI thread only for eviction). Configurable `MaxEntries` (default 16) + `MaxMemoryEstimate` optional.
- Invalidated when: Pattern changes, Scale changes (affects geometry), StrokeThickness changes. Color changes reuse geometry.

## 10. Diagnostics & Telemetry
Expose `PatternDiagnostics` with parse & rendering metadata. Optionally fire tracing events (ETW/EventSource) or call injected logger. Provide static `EnableGlobalDiagnostics` flag.

## 11. Risks & Mitigations
| Risk | Mitigation |
|------|------------|
| Floating precision yields drifting tiles | Normalize & round tokens when hashing / computing tile extents |
| Very large tile sizes degrade perf | Hard cap + fallback to immediate mode |
| Malformed .pat crashes UI thread | Full try/catch; return error diagnostics |
| Revit API version differences | Reflection with safe property extraction fallback |
| Excess memory with many patterns | LRU eviction, weak references |
| High DPI rendering blur | Device-independent geometry + SnapToDevicePixels optional |

## 12. Extensibility
- `IFillPatternSource` abstraction for custom sources (database, network).
- `IPatternRenderer` strategy for alternative rendering (e.g., GPU accelerated path).
- Future: Complex hatch shapes, gradient fills, vector symbol fills.

## 13. Accessibility
- Custom `AutomationPeer` exposes pattern properties & state.
- ToolTip derived from description if not set explicitly.

## 14. Validation Rules
- Scale, Zoom > 0 (coerce). If invalid assignment in code-behind => throw `ArgumentOutOfRangeException`.
- StrokeThickness > 0; default 1.
- Dash absolute value <= MaxSegment (configurable, default 10,000). Larger => warning & clamp.
- Limit enumerated lines per group (e.g., max 10,000 per tile) to prevent runaway generation.

## 15. Testing Matrix
Categories:
- Parsing: single pattern, multiple headers, comments only, malformed tokens, negative dashes, zero dash (dot), huge numeric values, duplicate names.
- Rendering: dense line spacing, sparse spacing, extreme zoom in/out, pan offsets, DPI 100/150/200/300, stroke thickness changes.
- Performance: warm cache vs cold parse; large file (~1000 lines) handled under guard.
- Interaction: zoom around cursor accuracy, pan clamping, reset behavior.
- Error: missing file, empty file, unsupported format, reflection failure.

## 16. Milestones
1. Parser & internal model (Week 1)
2. Control skeleton + dependency properties (Week 2)
3. Rendering engine (tile + immediate) + caching (Week 3)
4. Revit FillPattern adapter via reflection (Week 4)
5. Interaction layer + diagnostics (Week 5)
6. Unit tests, docs, polish, accessibility (Week 6)

## 17. Open Questions
- Include support for pattern types with embedded shapes (Type 2) in v1? (Proposed: out of scope)
- Provide export to bitmap helper? (Maybe v1.1)
- Animations for zoom transitions? (Optional, default off)
- Theming via dynamic resource keys vs direct property sets? (Use dynamic resources where possible)

## 18. Acceptance Criteria
- Given valid `.pat` (e.g., ANSI31), preview visually matches reference within 1 px tolerance across 3 DPIs.
- Given invalid `.pat`, control surfaces error (ErrorTemplate) and `ParseFailed` event raised exactly once per load attempt.
- Switching patterns (10 sequential loads) does not exceed memory baseline + cache limit (evictions occur).
- Continuous zoom/pan interaction maintains >55 FPS on mid-tier hardware (pattern tile path) for 400×400 viewport.
- No unhandled exceptions under fuzzed invalid inputs.

## 19. Future Enhancements (Backlog)
- Async streaming parse for very large pattern libraries.
- GPU (D2D) renderer for massive pattern galleries.
- Pattern editing overlay (interactive modification).
- Pattern similarity search (hash-based clustering).

---
End of PRD.
