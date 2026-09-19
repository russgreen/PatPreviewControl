using System.Linq;
using Fallout.Common;
using Fallout.Common.Git;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Solutions;
using static Fallout.Common.EnvironmentInfo;

partial class Build : FalloutBuild
{
    readonly AbsolutePath OutputDirectory = RootDirectory / "output";
    readonly AbsolutePath SourceDirectory = RootDirectory / "src";

    const string LibraryProjectName = "FillPatternPreview";
    const string CompiledAssembly = "FillPatternPreview.dll";

    [Parameter("Configuration to build - Default is 'Release'")]
    readonly Configuration Configuration = Configuration.Release;

    [GitRepository]
    [Required]
    readonly GitRepository GitRepository;

    [Solution]
    Solution Solution;

    Project LibraryProject => Solution.AllProjects.Single(x => x.Name == LibraryProjectName);

    public static int Main() => Execute<Build>(x => x.Pack);
}
