# PatPreviewControl

A WPF control for previewing AutoCAD/Revit style hatch (fill) patterns from multiple input sources. Supports both drafting (paper) and model (world) patterns with performant tiling, zoom, pan, and diagnostics.

## Features

- **Multiple Pattern Sources**: Load from .pat files, raw .pat text, Revit FillPattern objects, or internal models
- **High Performance**: Efficient tiling with geometry caching and automatic rendering mode selection
- **Interactive Preview**: Zoom, pan, and interactive controls for pattern exploration
- **Comprehensive Diagnostics**: Detailed parsing metrics, error reporting, and performance monitoring
- **Cross-Platform Ready**: Core library compatible with .NET 8.0, with WPF-specific implementation for Windows

## Project Structure

```
src/FillPatternPreview/          # Main library
├── Controls/                    # WPF control implementation
├── Model/                      # Data models and records
├── Parsing/                    # .pat file parsing logic
├── Rendering/                  # Pattern rendering and geometry
├── Imaging/                    # Headless PNG thumbnail generation
├── Caching/                    # Performance caching systems
├── Diagnostics/                # Diagnostic and monitoring tools
├── Accessibility/              # Accessibility support
└── Adapters/                   # External system adapters (Revit, etc.)

tests/FillPatternPreview.Tests/  # Unit and integration tests
specs/                          # Detailed specifications
memory/                         # Project constitution and guidelines
templates/                      # Development templates
scripts/                        # Automation and helper scripts
```

## Quick Start

```csharp
using FillPatternPreview.Controls;

// Create the control
var patternPreview = new FillPatternPreview();

// Set pattern source (PatRawText, if set, takes priority over PatFilePath)
patternPreview.PatFilePath = @"C:\Patterns\ANSI31.pat";
patternPreview.PatPatternName = "ANSI31";

// Configure display
patternPreview.Scale = 1.0;
patternPreview.Zoom = 2.0;

// Handle events
patternPreview.PatternChanged += (s, e) => {
    Console.WriteLine($"Pattern loaded: {patternPreview.Pattern?.Name}");
};
```

## Generating a thumbnail

`PatternThumbnail` renders a pattern to a PNG **without a control, a window or a running
application**, using the same drawing code as the control.

```csharp
using FillPatternPreview.Imaging;

// From a .pat file (first pattern in the file unless a name is given)
byte[] png = PatternThumbnail.RenderPngFromFile(@"C:\Patterns\acad.pat", "ANSI31");
File.WriteAllBytes("ANSI31.png", png);

// From .pat text, with options
byte[] png2 = PatternThumbnail.RenderPng(patText, "BRICK", new PatternThumbnailOptions
{
    Width = 96,
    Height = 96,
    Dpi = 192,                          // 192x192 pixels, same logical size
    Background = null,                  // transparent
    LineBrush = Brushes.SteelBlue,
    FirstTileBrush = Brushes.Red,       // highlight the first repeat cell
    TilesAcross = 4,                    // about four repeats across the image
});

// From an already-parsed PatternDefinition, straight to a file or stream
PatternThumbnail.SavePng(pattern, "thumb.png");
```

By default the scale is chosen so about three repeats of the pattern span the image, so patterns
with very different tile sizes all give a legible thumbnail; set `Scale` to use a fixed scale as the
control does. It can be called from any thread (non-STA callers are marshalled onto a short-lived
STA thread). Unreadable patterns and out-of-range options throw; extreme patterns are drawn
partially rather than hanging.

## Building

### Prerequisites
- .NET 8.0 SDK or later
- For full WPF functionality: Windows with WPF workload

### Commands
```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Build and test (as specified in copilot-instructions.md)
dotnet test && dotnet build
```

## Development

This project follows a spec-driven development approach with comprehensive planning and testing:

- **Specifications**: See `specs/` directory for detailed technical specifications
- **Implementation Plan**: `specs/FillPatternPreview.implementation.plan.md`
- **Task Tracking**: `specs/FillPatternPreview.tasks.md`
- **Constitution**: `memory/constitution.md` for development principles

### Key Principles
- **Test-Driven Development**: All features must have tests before implementation
- **Performance First**: Visual fidelity within ±1px, parse <50ms, render <5ms
- **Robustness**: No UI thread crashes, comprehensive error handling
- **Caching Excellence**: LRU caching for patterns and geometry

## Current Status

✅ **Spec-Kit Implementation**: Complete project structure with specifications, templates, and automation  
✅ **Project Foundation**: Solution structure, basic models, and cross-platform skeleton  
✅ **Build System**: Working build and test pipeline  
🚧 **Core Implementation**: In progress - following the detailed implementation plan  

See `specs/FillPatternPreview.tasks.md` for detailed progress tracking.

## License

See [LICENSE.txt](LICENSE.txt) for license information.

## Contributing

This project follows the principles outlined in `memory/constitution.md`. All contributions should:
1. Include comprehensive tests
2. Meet performance targets
3. Follow the established patterns
4. Update documentation as needed