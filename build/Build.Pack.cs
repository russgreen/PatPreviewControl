using Fallout.Common;
using Fallout.Common.Git;
using Fallout.Common.Tools.DotNet;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

partial class Build
{
    Target Pack => _ => _
        .DependsOn(Sign)
        .OnlyWhenStatic(() => GitRepository.IsOnMainOrMasterBranch())
        .Executes(() =>
        {
            // --no-build so the signed assemblies in bin/ are packed rather than being rebuilt
            DotNetPack(settings => settings
                .SetProject(LibraryProject)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(OutputDirectory)
                .EnableNoBuild()
                .SetVerbosity(DotNetVerbosity.minimal));
        });
}
