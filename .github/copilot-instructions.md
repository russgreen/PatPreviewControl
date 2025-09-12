# PatPreviewControl Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-01-12

## Active Technologies
- .NET 8.0 + WPF (PatPreviewControl Project)
- Cross-platform core library with Windows-specific WPF implementation

## Project Structure
```
src/FillPatternPreview/          # Main library with WPF control
tests/FillPatternPreview.Tests/  # Comprehensive test suite
specs/                          # Detailed specifications and plans
memory/                         # Project constitution and principles
templates/                      # Development templates
scripts/                        # Automation scripts
FillPatternPreviewControl_PRD.md # Product requirements document
```

## Commands
dotnet test && dotnet build

## Code Style
C#: Follow standard conventions with comprehensive XML documentation

## Recent Changes
- ✅ Spec-kit structure fully implemented and verified
- ✅ Project foundation with working build/test pipeline
- ✅ Cross-platform compatible core models and control skeleton
- ✅ Constitution updated with project-specific principles
- ✅ README documentation completed

## Current Implementation Status
Following the detailed plan in `specs/FillPatternPreview.implementation.plan.md`:
- **Milestone 1**: Data & Parser models ✅ (basic structure complete)
- **Milestone 2**: Control skeleton ✅ (cross-platform compatible)
- **Remaining**: Full .pat parser, geometry rendering, caching, interaction

## Key Principles
1. **Test-Driven Development** (NON-NEGOTIABLE): Tests first, then implementation
2. **Performance Targets**: Visual fidelity ±1px, parse <50ms, render <5ms  
3. **Robustness**: No UI crashes, comprehensive error handling
4. **Cross-Platform**: Core functionality works on all .NET platforms

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->