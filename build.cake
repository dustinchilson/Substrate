// ARGUMENTS
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var skipTests = Argument("SkipTests", false);

var fullSemVer = Argument("fullSemVer", "0.0.1");
var assemblySemVer = Argument("assemblySemVer", "0.0.1");
var informationalVersion = Argument("informationalVersion", "0.0.1");

// Variables
var artifactsDirectory = Directory("./artifacts");
var solutionFile = "./Substrate.sln";

var msBuildSettings = new DotNetCoreMSBuildSettings
{
    MaxCpuCount = 1
};

msBuildSettings.Properties.Add("PackageVersion", new List<string> { fullSemVer });
msBuildSettings.Properties.Add("Version", new List<string> { assemblySemVer });
msBuildSettings.Properties.Add("FileVersion", new List<string> { assemblySemVer });
msBuildSettings.Properties.Add("AssemblyVersion", new List<string> { assemblySemVer });
msBuildSettings.Properties.Add("AssemblyInformationalVersion", new List<string> { informationalVersion });

// Define directories.
var buildDir = Directory("./build/bin") + Directory(configuration);

Task("Clean")
    .Does(() =>
    {
        CleanDirectory(artifactsDirectory);
    });

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetCoreRestore(solutionFile);
    });

Task("Test")
    .WithCriteria(!skipTests)
    .Does(() =>
    {
        var path = MakeAbsolute(new DirectoryPath(solutionFile));
        DotNetCoreTest(path.FullPath, new DotNetCoreTestSettings
        {
            Configuration = "Debug",
            NoRestore = true,
            ResultsDirectory = artifactsDirectory,
            Logger = "trx;LogFileName=TestResults.xml"
        });
    });

Task("Pack")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        var path = MakeAbsolute(new DirectoryPath(solutionFile));
        DotNetCorePack(path.FullPath, new DotNetCorePackSettings
        {
            Configuration = configuration,
            NoRestore = true,
            OutputDirectory = artifactsDirectory,
            MSBuildSettings = msBuildSettings
        });
    });

Task("Default")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Test")
    .IsDependentOn("Pack");

RunTarget(target);
