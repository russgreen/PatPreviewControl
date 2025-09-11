# Technical Implementation Plan: FillPatternPreview WPF Control

## 1. Architectural Overview
Layered design:
- Public Control Layer (FillPatternPreview): WPF Control + Dependency Properties + Interaction + OnRender pipeline selector.
- Pattern Acquisition Layer: Sources (File path, Raw text, Reflection object, Internal model) unified into internal PatternDefinition.
- Parsing Layer: Stateless parser producing PatternParseResult with diagnostics.
- Rendering Layer: Tile computation, geometry generation, drawing brush creation, immediate rendering fallback.
- Caching Layer: LRU cache for parsed patterns (by file) and geometry/brush assets (by pattern hash + stroke thickness bucket + tile size).
- Diagnostics Layer: Aggregates parse & render metrics, exposes immutable PatternDiagnostics.
- Extensibility Interfaces (future): IFillPatternSource, IPatternRenderer.

Threading: Parsing and heavy geometry building performed on background Task; UI updates marshalled via Dispatcher. Rendering itself remains on UI thread. All caches UI-thread constrained except file parse cache which is thread-safe.

## 2. Core Data Structures
Records (immutable):
- PatternDefinition (Name, Description, IsModel, IReadOnlyList<LineGroup>)
- LineGroup (AngleDeg, OriginX, OriginY, DeltaX, DeltaY, IReadOnlyList<double> DashPattern)
- PatternDiagnostics (Success, LineGroupCount, WarningCount, ErrorCount, TileSize, Tileable, ParseDuration, Message)
- PatternParseResult (Success flag, Lists of errors/warnings, Dictionary<string, PatternDefinition>)

Internal (classes):
- ParsedFileCacheEntry (Path, LastWriteTimeUtc, ContentHash, PatternParseResult, Timestamp)
- GeometryCacheEntry (PatternHash, StrokeBucket, TileKey, DrawingGroup / DrawingBrush / Metadata)
- LruCache<TKey,TValue> (custom or wrapper) with O(1) add/move/remove.

## 3. Hashing & Keys
Pattern hash: Serialize ordered line groups: Angle, OriginX, OriginY, DeltaX, DeltaY, dash array values each rounded to 1e-6; include Name + IsModel flag. Use StringBuilder -> SHA256 -> base64 (shortened) or stable xxHash64. Hash used for geometry cache key plus stroke thickness & tile sizing.

Stroke bucket: Round(StrokeThickness * 100)/100 to limit variants.
Tile key: Width/Height each rounded to 1e-3.

## 4. Dependency Properties
Register with PropertyMetadata including: default, property changed callback, coercion where needed.
List (CLR wrappers):
- PatternSource (enum)
- PatFilePath
- PatRawText
- PatPatternName
- RevitFillPattern
- Scale (coerce > 0)
- Zoom (coerce to [0.1, 20])
- PanOffset
- LineBrush
- Background
- StrokeThicknessOverride (coerce null or >0)
- IsModelPatternOverride
- RenderMode
- TileSizeHint
- ShowBounds
- ErrorTemplate
- IsInteractive
- SnapsToDevicePixels
Read-only DP (Key registration):
- Pattern (PatternDefinition)
- Diagnostics (PatternDiagnostics)

Property changed handling groups:
- Source-affecting: PatternSource, PatFilePath, PatRawText, PatPatternName, RevitFillPattern, IsModelPatternOverride -> triggers AcquirePatternAsync.
- Visual-affecting (regen geometry if needed): Scale, StrokeThicknessOverride, TileSizeHint.
- Transform-only (no regen): Zoom, PanOffset, LineBrush, Background, RenderMode (may switch mode evaluation), ShowBounds, SnapsToDevicePixels.

## 5. Acquisition Workflow
1. On any source-affecting change, cancel prior pending acquisition token.
2. Determine source branch:
   - InternalModel: expect Pattern already provided externally (future) or set directly.
   - PatFile: Read file async -> content string -> Parse.
   - PatText: Parse directly.
   - FillPatternObject: Reflect -> build PatternDefinition.
3. Parse or adapt -> PatternDefinition or failure.
4. Apply IsModel override if IsModelPatternOverride not null.
5. Compute PatternDiagnostics (parse duration, counts, message, success placeholder tile fields null).
6. Set Pattern DP (read-only) & Diagnostics; raise PatternChanged; if failure raise ParseFailed.
7. Kick Tile + Geometry generation on background (if success) -> update Diagnostics with tile metrics when ready.

## 6. Parsing Implementation
Parser responsibilities:
- Split lines; trim; skip empty or comment (starts with ';').
- Detect headers: line starts with '*' then name until comma or end, optional description after first comma.
- Accumulate definition lines until next header or EOF.
- For each definition line: tokenize by comma, trim whitespace; require ?5 numeric tokens.
- Double parsing: Use InvariantCulture, double.TryParse.
- Construct LineGroup (dash tokens may be absent => empty list meaning continuous line).
- Validation warnings: extremely large |dash| > MaxSegment -> clamp & warn.
- If any line group valid -> pattern success; else error.
- Multi-pattern selection: If a specific name requested (case-insensitive) pick it; else first or all for dictionary.

Performance: Single pass, no allocations for token splitting beyond arrays (use Span if micro-optimizing later). Provide parse duration via Stopwatch.

## 7. Reflection Adapter (FillPatternObject)
- Attempt to resolve type at runtime: obj.GetType().FullName == "Autodesk.Revit.DB.FillPattern".
- Extract with PropertyInfos: Name, IsModel, GetSegments() or Segments property.
- Segment iteration via dynamic or reflection: each segment expected to expose Angle, Origin, Shift, SegmentLengths (dash array).
- Wrap failures with warnings; partial extraction allowed.
- No compile-time dependency.

## 8. Tile Computation Algorithm
Input: PatternDefinition + optional TileSizeHint.
Steps:
1. Iterate line groups collecting candidate repeat distances: |DeltaX|, |DeltaY| where > epsilon (1e-6).
2. maxX = max(candidate DeltaX, synthesized from dash if necessary); same for maxY.
3. If both remain near zero: use fallback 10x10 or TileSizeHint.
4. Clamp tile dimensions to [1e-3, MaxTileDimension].
5. Determine Tileable: True if at least one non-zero periodic offset OR multiple line groups with different origins creating a repeat region AND tile size ? tile limit.
6. Record tile size in Diagnostics; store PatternDomain (minX=0, minY=0, maxX, maxY, expandable=false).

Note: Real LCM across floating values is approximated by taking max offsets; improvement backlog: rational reduction with tolerance.

## 9. Geometry Generation (Tiled Mode)
Performed background thread then marshalled.
Steps per pattern:
1. Convert angle degrees -> radians once per group. dir = (cos, sin); perp = (-sin, cos).
2. Determine repetition across perpendicular axis: compute spacing from projection of (DeltaX, DeltaY) onto perp; if near zero, approximate using aggregated dash length or fallback constant.
3. Enumerate line indices covering tile expanded by margin = strokeThickness.
4. For each line: compute anchor point origin_i = baseOrigin + perp * (i * spacing).
5. Generate dash segments: iterate dash pattern circularly until covering tile diagonal length + margin. Maintain cursor along dir direction; positive length => emit segment (line geometry); negative => skip length; zero => dot (length = strokeThickness, centered).
6. Use StreamGeometry for each group (batched segments) to minimize object count. Freeze geometry.
7. Create DrawingGroup containing GeometryDrawing(s) with shared Pen (StrokeThickness from override or default 1.0). Freeze.
8. Create DrawingBrush (TileMode=Tile, Viewport=Rect(0,0,tileW,tileH), ViewportUnits=Absolute, Stretch=None). Freeze.
9. Store in geometry cache.

## 10. Immediate Rendering Path
OnRender:
1. Determine visible world rect by inverting transform matrix (Scale*Zoom + Pan).
2. For each line group: enumerate only lines intersecting inflated rect (similar enumeration as tiled but bounding by view).
3. Draw segments directly with DrawLine for each dash segment (or prebuild a lightweight StreamGeometry and reuse within frame).
4. Enforce line enumeration count cap.

## 11. Rendering Decision (Auto Mode)
Use tile path if Tileable && tile dimension ? AutoTileThreshold (default 2048) && EffectiveScale not extreme (e.g., within [0.05, 200]). Else immediate.

## 12. Transform Application
Matrix sequence for brush (tiled path): M = Scale(EffectiveScale) * Translate(PanX, PanY). Pan applied as negative offset to brush Transform (so panning moves pattern visually). For immediate path, apply GuidelineSet if SnapsToDevicePixels to reduce antialias jitter.

## 13. Caching Strategy
Caches:
- File Parse Cache: ConcurrentDictionary<(Path, LastWriteTimeUtc, ContentHash), PatternParseResult>. Soft limit entries (e.g., 64) trimmed opportunistically.
- Geometry Cache: LruCache<(PatternHash, StrokeBucket, TileKey), GeometryCacheEntry> with max 16 entries (configurable static property). Evict on insertion overflow.
Invalidation triggers:
- Pattern change -> clear geometry entries for that pattern hash.
- Scale or StrokeThicknessOverride or TileSizeHint change -> flush geometry entries referencing current PatternHash.
Color change (LineBrush) does not invalidate geometry; at render clone Pen if necessary (or maintain Pen pool keyed by Brush reference + stroke thickness).

## 14. Diagnostics Update Flow
Initial parse sets base diagnostics (tile fields null). After geometry built, update Diagnostics with TileSize, Tileable. Maintain immutable record by copying with modifications.
Collect warnings/errors in lists; counts stored; Success = errors.Count == 0 && lineGroups > 0.

## 15. Interaction Handling
If IsInteractive true:
- Override OnMouseWheel: adjust Zoom with exponential step (e.g., factor 1.1 per wheel delta notch). Re-center around cursor: translate PanOffset so that logical point remains under cursor.
- OnMouseDown (capture) + Move: update PanOffset by delta / EffectiveScale.
- DoubleClick: reset Zoom=1, PanOffset=(0,0).
- OnKeyDown: +/- adjust Zoom; arrows adjust PanOffset by (10 / EffectiveScale) units; Ctrl+0 reset.
Raise InteractionChanged after any Zoom/Pan reset or update.

## 16. Error Handling Strategy
- Parsing exceptions: caught -> Diagnostics with ErrorCount and message.
- File IO: catch (FileNotFound, UnauthorizedAccess, IOException) -> error entry.
- Reflection: catch and add warning; still allow fallback if partial.
- Rendering: wrap geometry build in try/catch; if failure -> fallback immediate simple error visual (diagonal cross lines) unless ErrorTemplate provided.
- Design Mode: swallow all exceptions after logging (DesignerProperties.GetIsInDesignMode).

## 17. Accessibility
Create FillPatternPreviewAutomationPeer deriving from FrameworkElementAutomationPeer.
Expose pattern name, description, IsModel via GetNameCore / GetItemStatusCore / GetHelpTextCore. Include warnings summary if any.

## 18. Performance Considerations
- Freeze all Freezables.
- Avoid per-frame allocations: reuse matrices, pens if possible.
- Conditional guideline snapping only when SnapsToDevicePixels.
- Lazy geometry generation: only after successful parse; cancel pending build if Pattern changes before completion.
- Limit enumerated lines early by bounding math rather than post-filtering.

## 19. Testing Strategy
Unit Tests:
- Parser: variety of valid/invalid lines, multiple pattern selection, numeric culture invariance.
- Hash stability: same input -> same hash; order-sensitivity checks.
- Tile computation: patterns with zero offsets, large offsets, hint usage.
- Dash expansion: positive, negative, zero dashes; overflow clamp.
- Geometry generation: count of segments within expected bounds; tile brush viewport sizing.
- Caching: eviction order after threshold; geometry reuse on color change.
- Interaction math: zoom around cursor retains anchored point within tolerance.

Integration / Visual Tests (optional): pixel diff using RenderTargetBitmap with tolerances.
Performance: Stopwatch instrumentation for parse & geometry build using representative patterns.

## 20. Logging Hooks
Provide static IFillPatternLogger? with methods: OnParseStart(path/name), OnParseComplete(result), OnGeometryBuilt(patternHash, tileSize, duration), OnError(context, exception).
No-op default implementation.

## 21. Configuration Constants
- Epsilon = 1e-6
- MaxSegmentLength = 10_000
- MaxTileDimension = 4096
- MaxLinesPerGroup = 10_000
- AutoTileThreshold = 2048
- DefaultFallbackTile = 10 x 10
- ZoomMin = 0.1, ZoomMax = 20.0

## 22. File Layout (Proposed)
src/FillPatternPreview/Controls/FillPatternPreview.cs
src/FillPatternPreview/Parsing/PatParser.cs
src/FillPatternPreview/Parsing/PatternParseResult.cs
src/FillPatternPreview/Model/PatternDefinition.cs (if not already split)
src/FillPatternPreview/Rendering/PatternTiler.cs (tile computation + geometry build)
src/FillPatternPreview/Rendering/PatternImmediateRenderer.cs
src/FillPatternPreview/Caching/GeometryCache.cs
src/FillPatternPreview/Caching/ParseFileCache.cs
src/FillPatternPreview/Adapters/RevitFillPatternAdapter.cs
src/FillPatternPreview/Diagnostics/PatternDiagnosticsBuilder.cs
src/FillPatternPreview/Interaction/InteractionHelper.cs (optional)
src/FillPatternPreview/Accessibility/FillPatternPreviewAutomationPeer.cs

## 23. Implementation Order (Milestones)
1. Data & Parser: Records, parser, parse result, unit tests.
2. Control Skeleton: DP registration, pattern acquisition plumbing (without rendering).
3. Tile & Geometry: Tile computation + geometry generator + brush creation.
4. Rendering Integration: OnRender logic, Auto mode heuristic, immediate fallback stub.
5. Caching Layer: Geometry + parse caches, invalidation wiring.
6. Reflection Adapter: Revit pattern extraction.
7. Interaction: Mouse & keyboard, transform math refinements.
8. Diagnostics & Logging: Populate and update diagnostics; automation peer.
9. Performance Tuning: Freeze objects, reduce allocations, finalize thresholds.
10. Testing & Polish: Expand tests, finalize docs & comments.

## 24. Risk Mitigation Actions
- Floating drift: Round values early when hashing & tile computation.
- Giant tiles: Enforce MaxTileDimension before geometry build.
- Memory pressure: Keep cache small, provide ClearCache static method.
- UI hitches: Offload heavy parse & geometry build; cancellation tokens.
- DPI artifacts: Provide optional SnapToDevicePixels & guideline sets.

## 25. Pseudocode Highlights
AcquirePatternAsync():
```
CancelPrevious();
var token = CreateToken();
var sw = Stopwatch.StartNew();
PatternParseResult parse;
switch(source) { case File: content = await ReadFileAsync; parse = Parse(content); ... }
if(token.IsCancelled) return;
var pattern = SelectDefinition(parse, name);
var diags = BuildDiagnostics(parse, sw.Elapsed);
SetPattern(pattern, diags);
if(diags.Success) StartGeometryBuild(pattern, token);
```

Geometry Build:
```
ComputeTile(pattern);
foreach(group in pattern.LineGroups) BuildGroupGeometry(group, tile, stroke);
Compose DrawingGroup -> DrawingBrush;
Freeze; Cache;
UpdateDiagnostics(tile info);
InvalidateVisual();
```

OnRender:
```
if(UseTiled) DrawRectangle(BackgroundBrush);
Apply Brush Transform (scale+pan);
Draw bounds if ShowBounds;
Else ImmediateRender(context);
```

## 26. Tooling & Future Enhancements
- Potential source generator for DP boilerplate.
- Optional GPU renderer later: implement IPatternRenderer.
- Add export helper: Render to RenderTargetBitmap using existing geometry.

---
End of Technical Implementation Plan.
