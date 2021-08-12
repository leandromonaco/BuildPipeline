using System;
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

    public static int Main () => Execute<Build>(x => x.Pack);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    //readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    readonly Configuration Configuration = Configuration.Release;

    [Solution] readonly Solution Solution;
    //[GitRepository] readonly GitRepository GitRepository;
    //[GitVersion(Framework = "netcoreapp3.1")] readonly GitVersion GitVersion;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
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

    //Target Compile => _ => _
    //    .DependsOn(Restore)
    //    .Executes(() =>
    //    {
    //        DotNetBuild(s => s
    //            .SetProjectFile(Solution)
    //            .SetConfiguration(Configuration)
    //            //.SetAssemblyVersion(GitVersion.AssemblySemVer)
    //            //.SetFileVersion(GitVersion.AssemblySemFileVer)
    //            //.SetInformationalVersion(GitVersion.InformationalVersion)
    //            .EnableNoRestore());
    //    });


    Target Compile => _ => _
    .DependsOn(Restore)
    .Executes(() =>
    {
        MSBuildTasks.MSBuild(s => s
            .SetTargetPath(Solution)
            .SetConfiguration(Configuration)
            //.SetMaxCpuCount(Environment.ProcessorCount)
            //.SetNodeReuse(IsLocalBuild)
            //.SetProperty("DeployOnBuild", "true")
            //.SetProperty("PublishProfile", Configuration.ToString())
            //.SetProperty("Platform", "AnyCPU")
            //.SetProperty("PublishUrl", publishDestination)
            //.SetTargets("Build", "Publish")
            );
    });



    Target Pack => _ => _
    .DependsOn(Compile)
    .Executes(() =>
    {
        NuGetTasks.NuGetInstall();
        NuGetTasks.NuGetPack(n => n.SetTargetPath(@$"{SourceDirectory}\CakeTest.WebApp.NetFramework48\CakeTest.WebApp.NetFramework48.csproj")
                                   .SetOutputDirectory(OutputDirectory)
                                   .SetVersion("1.0.0"));

        //NuGetTasks.NuGetPack(n => n.SetTargetPath(@$"{SourceDirectory}\CakeTest.WebApp.NetCore31\CakeTest.WebApp.NetCore31.nuspec")
        //                           .SetOutputDirectory(OutputDirectory)
        //                           .SetVersion("1.0.2"));

        OctopusTasks.OctopusPack(o => o.SetBasePath(@$"{SourceDirectory}\CakeTest.WebApp.NetCore31\bin\Release\netcoreapp3.1")
                                       .SetOutputFolder(OutputDirectory)
                                       .SetId("CakeTest.WebApp.NetCore")
                                       .SetVersion("2.0.0.0"));
        //GitVersionTasks.GitVersion(g => g)

        //DotNetTasks.DotNetPack(n => n.SetProject(@$"{SourceDirectory}\CakeTest.WebApp.NetCore31\CakeTest.WebApp.NetCore31.csproj")
        //                          .SetOutputDirectory(OutputDirectory)
        //                          .SetVersion("1.0.0"));

        //DotNetTasks.DotNetPublish(n => n.SetProject(@$"{SourceDirectory}\CakeTest.WebApp.NetCore31\CakeTest.WebApp.NetCore31.csproj")
        //                                .SetPackageDirectory(@$"{OutputDirectory}\core"));
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
