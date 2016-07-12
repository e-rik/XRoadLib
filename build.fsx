#r @"packages/FAKE/tools/FakeLib.dll"

open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.Testing.NUnit3
open Fake.UserInputHelper
open System
open System.IO

#if MONO
#else
#load "packages/SourceLink.Fake/tools/Fake.fsx"
open SourceLink
#endif

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "janno-p"
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "XRoadLib"

// The url for the raw files hosted
let gitRaw = environVarOrDefault "gitRaw" ("https://raw.github.com/" + gitOwner)

// Strong name key file for assembly signing
let keyFile = "src" @@ "XRoadLib.pfx"

// Read additional information from the release notes document
let release = LoadReleaseNotes "RELEASE_NOTES.md"

// Projects which will be included in release
let productProjects =
    !! "src/*/project.json"
    -- "src/XRoadLib.Extensions.ProtoBuf/*"
    -- "src/XRoadLib.Tools/*"

// --------------------------------------------------------------------------------------
// Helper functions for dotnet command

let Dotnet command args =
    ExecProcess (fun p ->
        p.FileName <- "dotnet"
        p.Arguments <- String.Join(" ", command::args)
        ) TimeSpan.MaxValue
    |> ignore

let DotnetSingleArg command arg = Dotnet command [arg]
let DotnetBuild = Dotnet "build"
let DotnetPack = Dotnet "pack"
let DotnetRestore = DotnetSingleArg "restore"
let DotnetTest = DotnetSingleArg "test"

// --------------------------------------------------------------------------------------
// Remove files generated by previous build

Target "Clean" (fun _ ->
    !! "src/*/bin"
    ++ "test/*/bin"
    ++ "bin"
    ++ "temp"
    |> CleanDirs
)

Target "CleanDocs" (fun _ ->
    CleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Builds product assemblies in release mode

Target "BuildRelease" (fun _ ->
    productProjects
    |> Seq.iter (fun proj ->
        DotnetRestore proj
        DotnetBuild [proj; "--configuration"; "Release"])
)

// --------------------------------------------------------------------------------------
// Copies binaries from default location to expected bin folder
// But keeps a subdirectory structure for each project in the
// src folder to support multiple project outputs

Target "CopyBinaries" (fun _ ->
    productProjects
    |> Seq.map (fun f -> Path.GetDirectoryName(f))
    |> Seq.filter (fun d -> Directory.Exists(d </> "bin" </> "Release" </> "net45"))
    |> Seq.map (fun d -> (d </> "bin" </> "Release" </> "net45", "bin" </> DirectoryInfo(d).Name))
    |> Seq.iter (fun (fromDir, toDir) -> CopyDir toDir fromDir (fun _ -> true))
)

// --------------------------------------------------------------------------------------
// Run tests for all target framework versions

Target "RunTests" (fun _ ->
    !! "test/*/project.json"
    -- "test/XRoadLib.Tests.Contract/project.json"
    |> Seq.iter (fun proj ->
        DotnetRestore proj
        DotnetTest proj)
)

#if MONO
#else
// --------------------------------------------------------------------------------------
// SourceLink allows Source Indexing on the PDB generated by the compiler, this allows
// the ability to step through the source code of external libraries http://ctaggart.github.io/SourceLink/

Target "SourceLink" (fun _ ->
    let baseUrl = sprintf "%s/XRoadLib/{0}/%%var2%%" gitRaw
    !! "src/**/*.??proj"
    -- "src/**/*.shproj"
    |> Seq.iter (fun projFile ->
        let proj = VsProj.LoadRelease projFile
        SourceLink.Index proj.CompilesNotLinked proj.OutputFilePdb __SOURCE_DIRECTORY__ baseUrl
    )
)

#endif

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->
    productProjects
    |> Seq.iter (fun proj ->
        let versionSuffix = release.SemVer.PreRelease |> Option.fold (fun _ v -> ["--version-suffix"; v.Origin]) []
        DotnetPack ([proj; "--output"; "bin"; "--configuration"; "Release"] @ versionSuffix))
)

Target "PublishNuget" (fun _ ->
    Paket.Push(fun p ->
        { p with
            WorkingDir = "bin" })
)


// --------------------------------------------------------------------------------------
// Generate the documentation


let fakePath = "packages" </> "FAKE" </> "tools" </> "FAKE.exe"
let fakeStartInfo script workingDirectory args fsiargs environmentVars =
    (fun (info: System.Diagnostics.ProcessStartInfo) ->
        info.FileName <- System.IO.Path.GetFullPath fakePath
        info.Arguments <- sprintf "%s --fsiargs -d:FAKE %s \"%s\"" args fsiargs script
        info.WorkingDirectory <- workingDirectory
        let setVar k v =
            info.EnvironmentVariables.[k] <- v
        for (k, v) in environmentVars do
            setVar k v
        setVar "MSBuild" msBuildExe
        setVar "GIT" Git.CommandHelper.gitPath
        setVar "FSI" fsiPath)

/// Run the given buildscript with FAKE.exe
let executeFAKEWithOutput workingDirectory script fsiargs envArgs =
    let exitCode =
        ExecProcessWithLambdas
            (fakeStartInfo script workingDirectory "" fsiargs envArgs)
            TimeSpan.MaxValue false ignore ignore
    System.Threading.Thread.Sleep 1000
    exitCode

// Documentation
let buildDocumentationTarget fsiargs target =
    trace (sprintf "Building documentation (%s), this could take some time, please wait..." target)
    let exit = executeFAKEWithOutput "docs/tools" "generate.fsx" fsiargs ["target", target]
    if exit <> 0 then
        failwith "generating reference documentation failed"
    ()

Target "GenerateReferenceDocs" (fun _ ->
    buildDocumentationTarget "-d:RELEASE -d:REFERENCE" "Default"
)

let generateHelp' fail debug =
    let args =
        if debug then "--define:HELP"
        else "--define:RELEASE --define:HELP"
    try
        buildDocumentationTarget args "Default"
        traceImportant "Help generated"
    with
    | e when not fail ->
        traceImportant "generating help documentation failed"

let generateHelp fail =
    generateHelp' fail false

Target "GenerateHelp" (fun _ ->
    DeleteFile "docs/content/release-notes.md"
    CopyFile "docs/content/" "RELEASE_NOTES.md"
    Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    DeleteFile "docs/content/license.md"
    CopyFile "docs/content/" "LICENSE.txt"
    Rename "docs/content/license.md" "docs/content/LICENSE.txt"

    generateHelp true
)

Target "GenerateHelpDebug" (fun _ ->
    DeleteFile "docs/content/release-notes.md"
    CopyFile "docs/content/" "RELEASE_NOTES.md"
    Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    DeleteFile "docs/content/license.md"
    CopyFile "docs/content/" "LICENSE.txt"
    Rename "docs/content/license.md" "docs/content/LICENSE.txt"

    generateHelp' true true
)

Target "KeepRunning" (fun _ ->
    use watcher = !! "docs/content/**/*.*" |> WatchChanges (fun changes ->
         generateHelp' true true
    )

    traceImportant "Waiting for help edits. Press any key to stop."

    System.Console.ReadKey() |> ignore

    watcher.Dispose()
)

Target "GenerateDocs" DoNothing

let createIndexFsx lang =
    let content = """(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../../bin"

(**
F# Project Scaffold ({0})
=========================
*)
"""
    let targetDir = "docs/content" </> lang
    let targetFile = targetDir </> "index.fsx"
    ensureDirectory targetDir
    System.IO.File.WriteAllText(targetFile, System.String.Format(content, lang))

Target "AddLangDocs" (fun _ ->
    let args = System.Environment.GetCommandLineArgs()
    if args.Length < 4 then
        failwith "Language not specified."

    args.[3..]
    |> Seq.iter (fun lang ->
        if lang.Length <> 2 && lang.Length <> 3 then
            failwithf "Language must be 2 or 3 characters (ex. 'de', 'fr', 'ja', 'gsw', etc.): %s" lang

        let templateFileName = "template.cshtml"
        let templateDir = "docs/tools/templates"
        let langTemplateDir = templateDir </> lang
        let langTemplateFileName = langTemplateDir </> templateFileName

        if System.IO.File.Exists(langTemplateFileName) then
            failwithf "Documents for specified language '%s' have already been added." lang

        ensureDirectory langTemplateDir
        Copy langTemplateDir [ templateDir </> templateFileName ]

        createIndexFsx lang)
)

// --------------------------------------------------------------------------------------
// Release Scripts

Target "CheckKeyFile" (fun _ ->
    if not (TestFile keyFile) then
        failwithf "Assembly strong name key file `%s` is not present." keyFile)

Target "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    CleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    CopyRecursive "docs/output" tempDocsDir true |> tracefn "%A"
    StageAll tempDocsDir
    Git.Commit.Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Branches.push tempDocsDir
)

#load "paket-files/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target "Release" (fun _ ->
    let user =
        match getBuildParam "github-user" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> getUserInput "Username: "
    let pw =
        match getBuildParam "github-pw" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> getUserPassword "Password: "
    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.pushBranch "" remote (Information.getBranchName "")

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" remote release.NugetVersion

    // release on github
    createClient user pw
    |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    // TODO: |> uploadFile "PATH_TO_FILE"
    |> releaseDraft
    |> Async.RunSynchronously
)

Target "BuildPackage" DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "RunTests"
  ==> "BuildRelease"
#if MONO
#else
  ==> "CopyBinaries"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"
#endif
  ==> "All"
#if MONO
#else
  =?> ("ReleaseDocs",isLocalBuild)
#endif

"All"
#if MONO
#else
  =?> ("SourceLink", Pdbstr.tryFind().IsSome )
#endif
  ==> "NuGet"
  ==> "BuildPackage"

#if MONO
#else
"CleanDocs"
  ==> "GenerateHelp"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"

"CleanDocs"
  ==> "GenerateHelpDebug"

"GenerateHelpDebug"
  ==> "KeepRunning"
#endif

"CheckKeyFile"
  ==> "Release"

#if MONO
#else
"ReleaseDocs"
  ==> "Release"
#endif

"BuildPackage"
  ==> "PublishNuget"
  ==> "Release"

RunTargetOrDefault "All"
