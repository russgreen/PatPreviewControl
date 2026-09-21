using Fallout.Common;
using Fallout.Common.Git;
using Fallout.Common.IO;
using Fallout.Common.Tools.SignTool;
using Serilog;
using System.Linq;
using static Fallout.Common.Tools.SignTool.SignToolTasks;

partial class Build
{
    Target Sign => _ => _
        .DependsOn(Compile)
        .OnlyWhenStatic(() => GitRepository.IsOnMainOrMasterBranch())
        .Executes(() =>
        {
            var projectDirectory = LibraryProject.Directory;

            var files = (projectDirectory / "bin" / Configuration)
                .GlobFiles(new[] { $"**/{CompiledAssembly}" })
                .ToList();

            Log.Information("Files found : {files}", files.Count);
            Assert.NotEmpty(files, "No compiled assemblies found to sign");

            foreach (var file in files)
            {
                Log.Information("File : {file}", file);
            }

            SignFiles(files.Select(x => x.ToString()).ToArray());
        });

    static void SignFiles(string[] files) => SignTool(s => s
        .SetFileDigestAlgorithm("sha256")
        .SetTimestampServerUrl("http://time.certum.pl")
        .SetSigningSubjectName("Open Source Developer, Russell Green")
        .SetFiles(files));
}
