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

    Dictionary<string, string> versions = new Dictionary<string, string>();

    Target Versioning => _ => _
    .DependsOn(Restore)
          .Executes(() =>
          {

              var projectFiles = SourceDirectory.GlobFiles("**/*.csproj");
              foreach (var projectFile in projectFiles)
              {
                  //TODO: This component is throwing an exception. See Nuke GitHub project.
                  var result = NerdbankGitVersioningTasks.NerdbankGitVersioningGetVersion(v => v.SetProcessWorkingDirectory(projectFile.Parent).SetProcessArgumentConfigurator(a => a.Add("-f json"))).Result;
                  versions.Add(projectFile, result.SimpleVersion);
                  //this process requires to install nbgv
                  //dotnet tool install --tool-path . nbgv


                  //using (Process p = new Process())
                  //{
                  //    // set start info
                  //    p.StartInfo = new ProcessStartInfo("cmd.exe")
                  //    {
                  //        RedirectStandardInput = true,
                  //        UseShellExecute = false,
                  //        WorkingDirectory = projectFile.Parent
                  //    };
                  //    // event handlers for output & error
                  //    p.OutputDataReceived += p_OutputDataReceived;
                  //    //p.ErrorDataReceived += p_ErrorDataReceived;

                  //    // start process
                  //    p.Start();
                  //    // send command to its input
                  //    p.StandardInput.Write("nbgv get-version -v Version + p.StandardInput.NewLine);
                  //    p.StandardInput.Write("nbgv set-version - p CakeTest.WebApp.NetFramework48.csproj 3.0.0" + p.StandardInput.NewLine); 
                  //}
              }

          });

    static void p_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        Process p = sender as Process;
        if (p == null)
            return;
        //Console.WriteLine(e.Data);
    }

    static void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        Process p = sender as Process;
        if (p == null)
            return;
        Console.WriteLine(e.Data);
    }

    Target Compile => _ => _
    .DependsOn(Versioning)
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
                                   .SetVersion(versions.GetValueOrDefault(@$"{SourceDirectory}\CakeTest.WebApp.NetFramework48\CakeTest.WebApp.NetFramework48.csproj")));

        //NuGetTasks.NuGetPack(n => n.SetTargetPath(@$"{SourceDirectory}\CakeTest.WebApp.NetCore31\CakeTest.WebApp.NetCore31.nuspec")
        //                           .SetOutputDirectory(OutputDirectory)
        //                           .SetVersion("1.0.2"));

        OctopusTasks.OctopusPack(o => o.SetBasePath(@$"{SourceDirectory}\CakeTest.WebApp.NetCore31\bin\Release\netcoreapp3.1")
                                       .SetOutputFolder(OutputDirectory)
                                       .SetId("CakeTest.WebApp.NetCore")
                                       .SetVersion(versions.GetValueOrDefault(@$"{SourceDirectory}\CakeTest.WebApp.NetCore31\CakeTest.WebApp.NetCore31.csproj")));
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
