# PatPreviewControl - WPF Fill Pattern Preview Control

PatPreviewControl is a planned WPF control library for rendering AutoCAD and Revit fill (hatch) patterns. This control will parse `.pat` files, integrate with Revit API via reflection, and provide interactive preview capabilities with zoom, pan, and caching.

**Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Current Repository Status

This repository currently contains:
- **Empty Visual Studio solution** (`PatPreviewControl.sln`) with no projects
- **Comprehensive PRD** (`FillPatternPreviewControl_PRD.md`) defining requirements
- **Planning documentation** but no implementation yet

**Next steps**: Follow project creation instructions below to implement the WPF control library described in the PRD.

## Critical Environment Requirements

**⚠️ WINDOWS DEVELOPMENT REQUIRED**: This is a WPF project that requires Windows Desktop development capabilities. While basic .NET development works on Linux environments, **WPF workloads and templates are only available on Windows**.

- **Linux environments**: Cannot create or build WPF projects (missing WindowsDesktop SDK)
- **Windows environments**: Full WPF development capabilities available
- **GitHub Codespaces/Linux**: Limited to .NET class library development and testing

## Working with This Repository

### Current Solution Management
- **Check solution status**: `dotnet sln list` (currently shows "No projects found")
- **Add new projects to existing solution**: `dotnet sln add <project-path>`
- **Remove projects**: `dotnet sln remove <project-path>`

### Key Repository Files
- `PatPreviewControl.sln` - Empty Visual Studio solution file
- `FillPatternPreviewControl_PRD.md` - Complete product requirements document
- `README.md` - Basic project description
- `.gitignore` - Standard Visual Studio .gitignore

## Working Effectively

### Environment Setup and Validation
- **Verify .NET version**: `dotnet --version` (requires .NET 8.0+)
- **Check available templates**: `dotnet new list`
- **Install WPF workload** (Windows only): `dotnet workload install microsoft-net-sdk-windowsdesktop`

### Project Creation (Windows Environment)
1. **Create WPF Control Library**:
   ```bash
   dotnet new classlib -n PatPreviewControl
   cd PatPreviewControl
   ```
2. **Convert to WPF project** - Edit `.csproj`:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net8.0-windows</TargetFramework>
       <UseWPF>true</UseWPF>
       <ImplicitUsings>enable</ImplicitUsings>
       <Nullable>enable</Nullable>
     </PropertyGroup>
   </Project>
   ```
3. **Create test projects**:
   ```bash
   dotnet new xunit -n PatPreviewControl.Tests
   dotnet new console -n PatPreviewControl.Demo
   ```
4. **Create solution and add projects**:
   ```bash
   dotnet new sln -n PatPreviewControl
   dotnet sln add PatPreviewControl/PatPreviewControl.csproj
   dotnet sln add PatPreviewControl.Tests/PatPreviewControl.Tests.csproj
   dotnet sln add PatPreviewControl.Demo/PatPreviewControl.Demo.csproj
   ```

### Project Creation (Linux/Limited Environment)
1. **Create basic class library structure**:
   ```bash
   dotnet new classlib -n PatPreviewControl
   dotnet new xunit -n PatPreviewControl.Tests
   dotnet new sln -n PatPreviewControl
   dotnet sln add PatPreviewControl/PatPreviewControl.csproj PatPreviewControl.Tests/PatPreviewControl.Tests.csproj
   ```
2. **Note**: WPF-specific functionality cannot be tested in Linux environments

### Building and Testing
- **Build solution**: `dotnet build` -- **Fast: 1-5 seconds for clean build**
- **Build specific configuration**: `dotnet build --configuration Release`
- **Clean build**: `dotnet clean && dotnet build` -- **Takes 15 seconds cold, 1-4 seconds warm**
- **Run tests**: `dotnet test` -- **Very fast: <1 second for typical unit tests**
- **Run with coverage**: `dotnet test --collect:"XPlat Code Coverage"`

### Package Management
- **Restore packages**: `dotnet restore` -- **Fast: <1 second if already restored**
- **Add package**: `dotnet add package <PackageName>` -- **Takes 1-3 seconds**
- **List packages**: `dotnet list package`

### Code Quality and Formatting
- **Format code**: `dotnet format` -- **Fast: <2 seconds**
- **Format with verification**: `dotnet format --verify-no-changes`
- **Format specific files**: `dotnet format --include <path>`

## Key Project Dependencies

Based on the PRD, this project will require:
- **Core WPF**: `Microsoft.WindowsDesktop.App` (implicit with UseWPF=true)
- **Testing**: `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`
- **Reflection utilities** for Revit integration
- **Performance testing**: `BenchmarkDotNet` (optional)

## Validation Scenarios

Always test these scenarios after making changes:

### Basic Development Validation
1. **Verify build succeeds**: `dotnet build` (expect 0 warnings/errors)
2. **Run all tests**: `dotnet test` (expect all pass)
3. **Check code formatting**: `dotnet format --verify-no-changes`

### WPF-Specific Validation (Windows only)
1. **Create sample WPF control**:
   ```csharp
   public class FillPatternPreview : System.Windows.Controls.Control
   {
       static FillPatternPreview()
       {
           DefaultStyleKeyProperty.OverrideMetadata(typeof(FillPatternPreview), 
               new FrameworkPropertyMetadata(typeof(FillPatternPreview)));
       }
   }
   ```
2. **Test WPF compilation**: Should compile without errors
3. **Demo application**: Create simple WPF app to load the control

### Pattern Parsing Validation
1. **Create sample .pat parser** and test with basic patterns
2. **Test error handling** with malformed .pat content
3. **Performance testing** - parsing should complete in <50ms for typical files

## Performance Expectations

**All operations are FAST - No long timeouts needed:**
- **Project creation**: 1-11 seconds (includes NuGet restore)
- **Clean builds**: 15 seconds maximum (typically 1-4 seconds)
- **Incremental builds**: 1-5 seconds
- **Test execution**: <1 second for unit tests
- **Code formatting**: <2 seconds
- **Package installation**: 1-3 seconds

**⚠️ NO "NEVER CANCEL" warnings needed** - all operations complete quickly.

## Common Development Tasks

### Adding WPF Dependencies (Windows)
```bash
dotnet add package Microsoft.Xaml.Behaviors.Wpf
dotnet add package System.Drawing.Common
```

### Creating Control Templates
- Add `Themes/Generic.xaml` resource dictionary
- Define default control templates
- Test with sample WPF application

### Pattern File Testing
- Create sample `.pat` files in `TestData/` folder
- Test parser with various pattern types (ANSI31, custom patterns)
- Validate error handling with malformed files

### Revit Integration Testing
- Mock Revit API objects for testing without Revit installation
- Use reflection-safe testing patterns
- Test graceful fallback when Revit assemblies unavailable

## Project Structure

Expected final structure:
```
PatPreviewControl/
├── PatPreviewControl/              # Main WPF control library
│   ├── Controls/
│   │   └── FillPatternPreview.cs
│   ├── Models/
│   │   ├── PatternDefinition.cs
│   │   └── LineGroup.cs
│   ├── Parsers/
│   │   └── PatFileParser.cs
│   ├── Themes/
│   │   └── Generic.xaml
│   └── PatPreviewControl.csproj
├── PatPreviewControl.Tests/        # Unit tests
├── PatPreviewControl.Demo/         # Demo WPF application
├── TestData/                       # Sample .pat files
└── PatPreviewControl.sln
```

## Troubleshooting

### Linux Environment Limitations
- **Error**: "Could not resolve SDK Microsoft.NET.Sdk.WindowsDesktop"
  - **Solution**: This is expected on Linux. Use class library development for cross-platform work.
- **Cannot run WPF apps**
  - **Solution**: Use Windows development environment or Windows Subsystem for Linux with X11 forwarding.

### Windows Environment Issues
- **Missing WPF templates**
  - **Solution**: `dotnet workload install microsoft-net-sdk-windowsdesktop`
- **Build errors with UseWPF=true**
  - **Verify**: TargetFramework is `net8.0-windows`

### Package Restore Issues
- **Slow package restore**
  - **Check**: Internet connectivity and NuGet.org accessibility
  - **Solution**: `dotnet nuget locals all --clear` then `dotnet restore`

## Design-Time Support

For Visual Studio/VS Code integration:
- Install C# extension for VS Code
- Use `dotnet watch` for continuous building during development
- Configure `.editorconfig` for consistent formatting

## Testing Strategy

1. **Unit Tests**: Core pattern parsing and model classes
2. **Integration Tests**: File I/O and Revit API integration
3. **UI Tests**: WPF control rendering and interaction (Windows only)
4. **Performance Tests**: Pattern parsing and rendering benchmarks

Always run `dotnet test` before committing changes.