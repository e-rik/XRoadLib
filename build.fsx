#r "paket:
nuget Fake.Api.GitHub
nuget Fake.Core.Environment
nuget Fake.Core.ReleaseNotes
nuget Fake.Core.Target
nuget Fake.Documentation.DocFx
nuget Fake.DotNet.Cli
nuget Fake.DotNet.Paket
nuget Fake.Tools.Git
nuget Octokit //"

// http https://github.com/dotnet/docfx/releases/download/v2.45/docfx.zip 

#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Api
open Fake.Core
open Fake.Core.TargetOperators
open Fake.Documentation
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools
open System.IO

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "janno-p"
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "XRoadLib"

// The url for the raw files hosted
let gitRaw = Environment.environVarOrDefault "gitRaw" ("https://raw.github.com/" + gitOwner)

// Strong name key file for assembly signing
let keyFile = "src" </> "XRoadLib.snk"

// Read additional information from the release notes document
let release = ReleaseNotes.load "RELEASE_NOTES.md"

// Projects which will be included in release
let productProjects =
    !! "src/*/*.csproj"

let tempDocsDir = __SOURCE_DIRECTORY__ </> "temp" </> "gh-pages"
let binDir = __SOURCE_DIRECTORY__ </> "bin"

// --------------------------------------------------------------------------------------
// Remove files generated by previous build

Target.create "Clean" (fun _ ->
    !! "src/*/bin"
    ++ "test/*/bin"
    ++ "bin"
    ++ "temp"
    |> Shell.cleanDirs
)

// --------------------------------------------------------------------------------------
// Builds product assemblies in release mode

Target.create "BuildRelease" (fun _ ->
    productProjects
    |> Seq.iter (fun proj ->
        DotNet.restore id proj
        DotNet.build
            (fun p ->
                { p with
                    Common = { p.Common with CustomParams = Some(sprintf "/p:Version=%s" release.NugetVersion) }
                    Configuration = DotNet.BuildConfiguration.Release })
            proj
    )
)

// --------------------------------------------------------------------------------------
// Run tests for all target framework versions

Target.create "RunTests" (fun _ ->
    let testProjects =
        [
            ("test", "XRoadLib.Tests", ["net461"; "net5.0"])
            ("samples", "Calculator.Tests", ["net5.0"])
        ]

    testProjects
    |> List.iter
        (fun (dir, proj, fws) ->
            let testsPath = dir </> proj
            let projectPath = testsPath </> (sprintf "%s.csproj" proj)

            DotNet.restore id projectPath

            fws
            |> List.iter
                (fun fw ->
                    DotNet.build (fun p -> { p with Configuration = DotNet.BuildConfiguration.Debug; Framework = Some(fw) }) projectPath
                    DotNet.exec id "xunit" (testsPath </> "bin" </> "Debug" </> fw </> (sprintf "%s.dll" proj)) |> ignore
                )
        )
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "NuGet" (fun _ ->
    productProjects
    |> Seq.iter (fun proj ->
        DotNet.pack
            (fun p ->
                { p with
                    Common = { p.Common with CustomParams = Some(sprintf "/p:Version=%s" release.NugetVersion) }
                    OutputPath = Some(binDir)
                    Configuration = DotNet.BuildConfiguration.Release
                    VersionSuffix = release.SemVer.PreRelease |> Option.map (fun v -> v.Origin) })
            proj
    )
)

Target.create "PublishNuget" (fun _ ->
    let apiKey = Environment.environVarOrFail "NUGET_KEY"
    !! (binDir </> "*.nupkg")
    |> Seq.iter
        (DotNet.nugetPush
            (fun p ->
                p.WithPushParams(
                    { p.PushParams with
                        ApiKey = Some(apiKey)
                        Source = Some("https://api.nuget.org/v3/index.json")
                    }
                )
            )
        )
)

// --------------------------------------------------------------------------------------
// Generate documentation

Target.create "GenerateHelp" (fun _ ->
    Shell.rm "docs/articles/release-notes.md"
    Shell.copyFile "docs/articles/" "RELEASE_NOTES.md"
    Shell.rename "docs/articles/release-notes.md" "docs/articles/RELEASE_NOTES.md"

    Shell.rm "docs/articles/license.md"
    Shell.copyFile "docs/articles/" "LICENSE.md"
    Shell.rename "docs/articles/license.md" "docs/articles/LICENSE.md"
)

Target.create "CleanDocs" (fun _ ->
    Shell.cleanDirs [ tempDocsDir ]
)

Target.create "Serve" (fun _ ->
    DocFx.exec id "serve" tempDocsDir
)

Target.description "Generate the documentation"
Target.create "GenerateDocs" (fun _ ->
    DocFx.exec id (__SOURCE_DIRECTORY__ </> "docs" </> "docfx.json") ""
)

Target.create "ReleaseDocs" (fun _ ->
    Shell.cleanDirs [ tempDocsDir ]
    Git.Repository.cloneSingleBranch "" (sprintf "%s/%s.git" gitHome gitName) "gh-pages" tempDocsDir
    DocFx.exec id (__SOURCE_DIRECTORY__ </> "docs" </> "docfx.json") ""
    Git.Staging.stageAll tempDocsDir
    Git.Commit.exec tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Git.Branches.push tempDocsDir
)

// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "CheckKeyFile" (fun _ ->
    if not (Shell.testFile keyFile) then
        failwithf "Assembly strong name key file `%s` is not present." keyFile
)

Target.create "Release" (fun _ ->
    let token = Environment.environVarOrFail "GITHUB_TOKEN"

    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s" release.NugetVersion)
    Git.Branches.pushBranch "" remote (Git.Information.getBranchName "")

    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" remote release.NugetVersion

    // release on github
    GitHub.createClientWithToken token
    |> GitHub.draftNewRelease gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    // |> GitHub.uploadFile "PATH_TO_FILE"
    |> GitHub.publishDraft
    |> Async.RunSynchronously
)

Target.create "BuildPackage" ignore
Target.create "All" ignore

"Clean"
    ==> "RunTests"
    ==> "BuildRelease"
    ==> "GenerateDocs"
    ==> "All"
    =?> ("ReleaseDocs", BuildServer.isLocalBuild)

"All"
    ==> "NuGet"
    ==> "BuildPackage"

"CleanDocs"
    ==> "GenerateHelp"
    ==> "GenerateDocs"

"CheckKeyFile"
    ==> "Release"

"ReleaseDocs"
    ==> "Release"

"BuildPackage"
    ==> "PublishNuget"
    ==> "Release"

Target.runOrDefaultWithArguments "All"
