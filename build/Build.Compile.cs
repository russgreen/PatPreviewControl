using Fallout.Common;
using Fallout.Common.Tools.DotNet;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

partial class Build
{
    Target Compile => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetBuild(settings => settings
                .SetProjectFile(LibraryProject)
                .SetConfiguration(Configuration)
                .SetVerbosity(DotNetVerbosity.minimal));
        });
}
