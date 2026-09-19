# PatPreviewControl Constitution

## Core Principles

### I. Control-First Architecture
Every feature starts as a reusable WPF control; Controls must be self-contained, independently testable, and well-documented; Clear purpose required - focus on fill pattern preview functionality without organizational-only abstractions.

### II. Performance & Visual Fidelity
Visual fidelity within ±1 px vs reference for common ANSI/Revit patterns across typical DPI scales; Parse typical patterns (<200 lines) in <50 ms cold, <5 ms warm cache; Render 200×200 px preview in <5 ms; Robust against malformed input with no UI thread crashes.

### III. Test-Driven Development (NON-NEGOTIABLE)
TDD mandatory: Tests written → User approved → Tests fail → Then implement; Red-Green-Refactor cycle strictly enforced; Focus on parsing accuracy, rendering performance, and interaction reliability.

### IV. Caching & Efficiency
Implement LRU caching for both parsed patterns and geometry; Geometry cache keyed by pattern hash + stroke thickness + tile size; File cache based on path, modification time, and content hash; Minimize allocations and freeze Freezables for performance.

### V. Robustness & Diagnostics
Comprehensive error handling without crashes; Surface diagnostics for parse failures and warnings; Support design-mode safe operation; Provide meaningful error messages and fallback visuals; Log performance metrics and cache effectiveness.

## Technology Constraints

### WPF-Specific Requirements
- Target .NET Framework or .NET 6+ with WPF support
- Use dependency properties for all configurable aspects
- Implement proper XAML design-time support
- Follow WPF conventions for control development
- Support accessibility via AutomationPeer implementation

### Pattern Support Standards
- Support AutoCAD/Revit .pat file format parsing
- Handle both drafting (paper) and model (world) patterns
- Optional reflection-based Revit FillPattern object support (no hard dependencies)
- Maintain visual compatibility with reference implementations

## Development Workflow

### Implementation Phases
1. Data model & parser implementation with comprehensive tests
2. Control skeleton with dependency property registration  
3. Tile computation and geometry generation with caching
4. Rendering pipeline with auto/immediate mode selection
5. User interaction (zoom, pan) and accessibility features
6. Performance optimization and diagnostic enhancements

### Quality Gates
- All public APIs must have XML documentation
- Performance targets verified with benchmarks
- Visual regression testing where applicable
- Accessibility compliance verified
- Memory usage and cache behavior validated

## Governance

This constitution supersedes all other development practices for the PatPreviewControl project. All feature development must verify compliance with these principles. Complexity beyond the core pattern preview functionality must be justified with clear user value. Use the specifications in `specs/` for detailed technical guidance and implementation requirements.

**Version**: 1.0.0 | **Ratified**: 2025-01-12 | **Last Amended**: 2025-01-12