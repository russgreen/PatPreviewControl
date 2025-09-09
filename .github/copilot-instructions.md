# PatPreviewControl Development Guidelines

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Current Repository State

**CRITICAL**: This repository contains planning documentation and templates for a WPF Fill Pattern Preview Control, but the actual C# implementation does not exist yet. The .sln file is empty and contains no projects.

## Working Effectively

### Bootstrap and Setup
- .NET 8.0.119 is installed and available via `dotnet --version`
- Current commands that work:
  - `dotnet --version` -- immediate response
  - `dotnet build` -- succeeds but warns "Unable to find a project to restore" (expected)
  - `dotnet test` -- succeeds but finds no projects (expected)
  - `ls -la` -- lists repository contents in <1ms

### When Development Begins (Future Instructions)
- Create WPF projects with: `dotnet new classlib -n [ProjectName]` -- takes ~3 seconds including restore
- Build projects with: `dotnet build` -- takes ~10 seconds for clean build. NEVER CANCEL. Set timeout to 30+ seconds.
- Test projects with: `dotnet test` -- takes ~1 second when no tests exist, longer with actual tests. NEVER CANCEL. Set timeout to 60+ seconds.

### Repository Navigation
- **Core Documentation**: 
  - `FillPatternPreviewControl_PRD.md` - 226-line comprehensive product requirements document
  - `README.md` - Currently minimal (19 bytes)
  - `.github/copilot-instructions.md` - This file
- **Planning Infrastructure**:
  - `scripts/` - Project management scripts for feature development lifecycle
  - `templates/` - Templates for specs, plans, and tasks
  - `memory/` - Constitutional guidelines and update checklists

## Validation

**CURRENT STATE VALIDATION**: 
- Repository browse and document reading works immediately
- .NET tooling is available but no projects exist to build/test yet
- Scripts require feature branch setup (format: `001-feature-name`)

**WHEN PROJECTS EXIST**: 
- Always run `dotnet build` after making code changes
- Always run `dotnet test` to verify functionality
- CRITICAL: WPF projects require UI validation - take screenshots of running application
- Test the Fill Pattern Preview Control with actual .pat files per the PRD requirements

## Build and Test Timing Expectations
- **Project Creation**: 3-4 seconds. NEVER CANCEL. Set timeout to 30+ seconds.
- **Clean Build**: 10 seconds for simple projects. NEVER CANCEL. Set timeout to 60+ seconds.
- **Incremental Build**: 2-5 seconds. NEVER CANCEL. Set timeout to 30+ seconds.  
- **Test Execution**: 1 second (no tests) to 30+ seconds (full suite). NEVER CANCEL. Set timeout to 60+ seconds.

## Project Goals and Architecture

Based on `FillPatternPreviewControl_PRD.md`, this project will create:
- **Primary Goal**: Reusable WPF control (`FillPatternPreview`) for previewing AutoCAD/Revit fill patterns
- **Input Sources**: .pat files, raw .pat text, Revit API FillPattern objects, internal pattern models  
- **Key Features**: Zoom/pan, scaling, theming, caching, high-DPI support, error handling
- **Performance Targets**: Parse <50ms cold, <5ms warm; render 200x200px in <5ms

## Expected Project Structure (When Implemented)
```
src/
├── PatPreviewControl/
│   ├── FillPatternPreview.cs
│   ├── PatternParser.cs
│   ├── Models/
│   └── Themes/
tests/
├── PatPreviewControl.Tests/
│   ├── ParsingTests.cs
│   ├── RenderingTests.cs
│   └── TestData/
FillPatternPreviewControl_PRD.md
PatPreviewControl.sln
```

## Common Tasks

### Repository Structure (Current)
```
.
├── .github/
│   ├── copilot-instructions.md
│   └── prompts/
├── scripts/                    # Project lifecycle automation
├── templates/                  # Planning templates  
├── memory/                     # Constitutional guidelines
├── FillPatternPreviewControl_PRD.md  # 226-line requirements doc
├── PatPreviewControl.sln      # Empty solution file
├── README.md                  # Minimal (19 bytes)
└── LICENSE.txt
```

### Available Scripts
- `scripts/update-agent-context.sh` - Updates agent context files
- `scripts/setup-plan.sh` - Sets up implementation planning structure
- `scripts/check-task-prerequisites.sh` - Validates planning prerequisites
- **Note**: Scripts require feature branch format `001-feature-name`

## Error Conditions to Expect
- `dotnet build` currently warns "Unable to find a project to restore" - this is expected and normal
- `dotnet test` finds no projects - this is expected and normal  
- WPF templates not available via `dotnet new wpf` in current environment - use `dotnet new classlib` and add WPF references
- Script failures if not on proper feature branch format

## Future Development Validation Scenarios

**When WPF Control Implementation Begins**:
1. Create and build the control project successfully
2. Load a sample .pat file (ANSI31 recommended per PRD)
3. Render pattern preview and take screenshot
4. Test zoom/pan functionality  
5. Verify error handling with malformed .pat file
6. Performance test: parse typical pattern in <50ms
7. Performance test: render 200x200px preview in <5ms

## Critical Reminders
- **NEVER CANCEL** build or test operations - they may take 60+ seconds
- Always validate WPF UI changes with screenshots
- Follow PRD requirements for performance (<50ms parse, <5ms render)
- Test with real .pat files, not just synthetic data
- Repository currently has no C# code - instructions above apply when development begins

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->