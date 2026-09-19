# PatPreviewControl

A WPF control (`FillPatternPreview`) for previewing AutoCAD/Revit style hatch (fill) patterns from `.pat` files or raw `.pat` text. It renders both drafting (paper) and model (world) patterns at print-true size and supports interactive zoom, pan and repeat-tile inspection.

## Features

- **`.pat` sources**: load from a file (`PatFilePath`) or from raw text (`PatRawText`), and pick a pattern by name from a multi-pattern file (`PatPatternName`)
- **Drafting and model patterns**: `;%TYPE=MODEL` / `;%TYPE=DRAFTING` headers are honoured
- **Units aware**: `%UNITS=` (file-level or per pattern) is parsed and applied, so drafting patterns render at their print size at `Scale = 1`
- **Dashes and dots**: positive dash entries draw, negative entries are gaps, zero draws a dot
- **Interactive view**: mouse-wheel zoom around the cursor, drag to pan, double-click to reset, plus keyboard shortcuts
- **Tiling**: repeat the pattern across the control, or show just its first repeat cell, optionally highlighted in a different colour
- **Robust by design**: parsing and rendering are bounded (see [Safety limits](#safety-limits)); bad input or a rendering failure leaves the pattern undrawn rather than crashing the host app
- **Sample app**: `samples/PatternPreviewSampleApp` demonstrates the control

## Quick start

Reference the `FillPatternPreview` package (see [Building](#building) to produce it) or add a project reference to `src/FillPatternPreview`. The library targets `net8.0-windows` (WPF).

### XAML

```xml
<Window xmlns:controls="clr-namespace:FillPatternPreview.Controls;assembly=FillPatternPreview" ...>
    <controls:FillPatternPreview
        PatFilePath="C:\Patterns\acad.pat"
        PatPatternName="ANSI31"
        IsInteractive="True"
        HighlightFirstTile="True" />
</Window>
```

### Code

```csharp
using FillPatternPreview.Controls;

var preview = new FillPatternPreview
{
    PatRawText = "*ANSI31, ANSI Iron, Brick, Stone masonry\n45, 0,0, 0,3.175",
    PatPatternName = "ANSI31",   // optional; the first pattern is used if omitted or not found
    Scale = 1.0,
    IsInteractive = true,
};

preview.PatternChanged += (s, e) =>
    Console.WriteLine($"Loaded: {preview.Pattern?.Name} (model: {preview.Pattern?.IsModel})");

preview.InteractionChanged += (s, e) =>
    Console.WriteLine($"Zoom {preview.Zoom:0.00}x, pan {preview.PanOffset}");
```

`PatRawText` takes priority over `PatFilePath` when both are set. If the source can't be loaded or parsed, `Pattern` is `null` and nothing is drawn.

## Control reference

### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `PatFilePath` | `string?` | `null` | Path to a `.pat` file |
| `PatRawText` | `string?` | `null` | Raw `.pat` content (wins over `PatFilePath`) |
| `PatPatternName` | `string?` | `null` | Pattern to show from a multi-pattern source; falls back to the first pattern |
| `LineBrush` | `Brush` | `Black` | Brush for the pattern lines |
| `Scale` | `double` | `1.0` | Pattern scale, applied on top of the units-to-DIP conversion |
| `Zoom` | `double` | `1.0` | View zoom |
| `PanOffset` | `Point` | `0,0` | View pan, in pattern units |
| `IsInteractive` | `bool` | `false` | Enables mouse and keyboard zoom/pan (below) |
| `TilePattern` | `bool` | `true` | `true` repeats the pattern across the control; `false` draws only the first repeat cell at the top-left |
| `HighlightFirstTile` | `bool` | `false` | Draws the first repeat cell in `FirstTileBrush` |
| `FirstTileBrush` | `Brush` | `Red` | Brush used for the highlighted tile |
| `UsePatternMaker` | `bool` | `false` | Experimental alternative rendering path |
| `Pattern` | `PatternDefinition?` | — | Read-only; the pattern currently displayed |

`Scale`, `Zoom` and `PanOffset` are coerced to finite, bounded values, so a bad binding (NaN, infinity, negative) can't drive the renderer into pathological work. `TilePattern` and `HighlightFirstTile` have no effect on patterns whose repeat cell can't be determined; those are always fully tiled.

### Events

| Event | Raised when |
|---|---|
| `PatternChanged` | A new pattern has been loaded (or loading failed and `Pattern` became `null`) |
| `InteractionChanged` | The user changed `Zoom` or `PanOffset`, including a reset |

### Interaction (when `IsInteractive` is `true`)

| Input | Action |
|---|---|
| Mouse wheel | Zoom around the cursor |
| Drag | Pan |
| Double-click, `Ctrl+0` | Reset zoom and pan |
| `+` / `-` | Zoom in / out (control must have focus; clicking it gives focus) |
| Arrow keys | Pan |

Interactive zoom is limited to 0.1x–20x.

## Units and scaling

- **Drafting patterns** are defined in paper units. They are converted to device-independent units at 96 per inch using `%UNITS=` (`INCH`, `MM`, `CM`, `M`, `FEET`; inches if unspecified), so at `Scale = 1` a pattern renders at the size it would print at 100%.
- **Model patterns** are real-world sizes with no fixed paper conversion, so they are drawn at 1 unit per DIP and are meant to be sized with `Scale` and `Zoom`.

## Parsing `.pat` content directly

To get at parse errors and warnings (the control itself only exposes the resulting `Pattern`), use the parser:

```csharp
using FillPatternPreview.Parsing;

PatternParseResult result = PatParser.ParseFile(@"C:\Patterns\acad.pat");   // or ParseText(string)

foreach (var error in result.Errors)   Console.WriteLine($"Error: {error}");
foreach (var warning in result.Warnings) Console.WriteLine($"Warning: {warning}");

foreach (var (name, pattern) in result.Patterns)
    Console.WriteLine($"{name}: {pattern.LineGroups.Count} line groups, model={pattern.IsModel}, units={pattern.Units}");
```

Supported syntax: multiple patterns per file, `*NAME, description` headers with an optional `;%TYPE=MODEL|DRAFTING`, `%UNITS=` declarations, `;` comments, and definition lines of the form `angle, x-origin, y-origin, delta-x, delta-y [, dash, ...]`.

## Safety limits

Parsing and rendering run on the UI thread, so both are bounded so that hostile or corrupt input can't hang the app:

- Parser: 4 MB of input, 8192 characters per line, 10,000 patterns, 512 line groups per pattern and 64 dash entries per line; values beyond ±1e9, NaN or infinity are rejected.
- Renderer: dash cycles smaller than 1 DIP are drawn solid; at most 20,000 dash segments per line and 4,000 repeats per line family; and an overall budget of 250,000 drawing primitives per render, after which the pattern is drawn incompletely.

## Project structure

```
src/FillPatternPreview/          # The control library (net8.0-windows, WPF)
├── Controls/                    # FillPatternPreview control
├── Model/                       # PatternDefinition, LineGroup
├── Parsing/                     # .pat parser and parse result
├── Rendering/                   # Line expansion, tile calculation, interaction math, render budget
├── PatternMaker/                # Geometry types used by the experimental rendering path
├── Adapters/                    # PatternMakerAdapter
└── Themes/                      # Default control template

samples/PatternPreviewSampleApp/ # Demo app (paste .pat text, toggle tiling / highlight / interaction)
tests/FillPatternPreview.Tests/  # xUnit tests
build/                           # Fallout build project (compile, sign, pack)
specs/                           # Specification, implementation plan and task tracking
memory/                          # Project constitution and guidelines
```

## Building

### Prerequisites

- .NET 8.0 SDK or later (the build project targets .NET 10)
- Windows with the WPF workload

### Commands

```bash
# Build the solution
dotnet build

# Run the tests
dotnet test
```

### Building the NuGet package

The Fallout build in `build/` cleans, compiles (Release), signs the assembly and packs it:

```powershell
./build.ps1          # PowerShell
./build.cmd          # cmd
```

Individual targets can be run by name, for example `./build.ps1 Compile`. The `.nupkg` and `.snupkg` are written to `output/`.

Signing and packing only run on the `master` or `main` branch. Signing uses `signtool` with the certificate whose subject is "Open Source Developer, Russell Green" in the current user's certificate store (SHA-256 with a Certum timestamp), so it needs that certificate to be installed.

## Current status

The v1 scope is implemented: `.pat` parsing (including units), immediate-mode rendering, interactive zoom/pan, and tiling. Parse and geometry caching, a diagnostics object, a Revit `FillPattern` adapter, and an accessibility peer were part of the original design but are not implemented; `specs/FillPatternPreview.tasks.md` tracks what is and isn't done.

## License

See [LICENSE.txt](LICENSE.txt) for license information.

## Contributing

Development follows the principles in `memory/constitution.md`: include tests, keep the UI thread safe, and update the documentation and specs with your change.
