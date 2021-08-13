using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NerdbankGitVersioning;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Tools.Octopus;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;


[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Pack);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    //readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    readonly Configuration Configuration = Configuration.Release;

    [Solution] readonly Solution Solution;
    //[GitRepository] readonly GitRepository GitRepository;
    //[GitVersion(Framework = "netcoreapp3.1")] readonly GitVersion GitVersion;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "test";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Versioning => _ => _
    .DependsOn(Restore)
          .Executes(() =>
          {

              var projectFiles = SourceDirectory.GlobFiles("**/*.csproj");
              foreach (var projectFile in projectFiles)
              {
                
                  var version = NerdbankGitVersioningTasks.NerdbankGitVersioningGetVersion(v => v.SetProcessWorkingDirectory(projectFile.Parent).SetProcessArgumentConfigurator(a => a.Add("-f json"))).Result.Version;
                  NerdbankGitVersioningTasks.NerdbankGitVersioningSetVersion(v => v.SetProject(projectFile)
                                                                                   .SetVersion(version));
              }

          });

    Target Compile => _ => _
    .DependsOn(Versioning)
    .Executes(() =>
    {
        MSBuildTasks.MSBuild(s => s
            .SetTargetPath(Solution)
            .SetConfiguration(Configuration)
            );
    });



    Target Pack => _ => _
    .DependsOn(Compile)
    .Executes(() =>
    {
        NuGetTasks.NuGetInstall();
        NuGetTasks.NuGetPack(n => n.SetTargetPath(@$"{SourceDirectory}\CakeTest.WebApp.NetFramework48\CakeTest.WebApp.NetFramework48.csproj")
                                   .SetOutputDirectory(OutputDirectory));

       
        OctopusTasks.OctopusPack(o => o.SetBasePath(@$"{SourceDirectory}\CakeTest.WebApp.NetCore31\bin\Release\netcoreapp3.1")
                                       .SetOutputFolder(OutputDirectory)
                                       .SetId("CakeTest.WebApp.NetCore"));
       
    });

    //Target Test => _ => _
    //.DependsOn(Compile)
    //.Executes(() =>
    //{
    //    DotNetTest(s => s
    //        .SetProjectFile(Solution)
    //        .SetConfiguration(Configuration)
    //        .EnableNoRestore()
    //        .EnableNoBuild());
    //});

    //Target Pack => _ => _
    //    .DependsOn(Compile)
    //    .Executes(() =>
    //    {
    //        MSBuildTasks.p
    //    });
}
