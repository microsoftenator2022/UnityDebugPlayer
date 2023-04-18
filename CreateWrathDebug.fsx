open System
open System.IO

let boot_cfg = """wait-for-managed-debugger=1
player-connection-debug=1
"""

let steam_appid = "1184370"

let unityDebugFiles =
  [ "UnityPlayer.dll"; "WinPixEventRuntime.dll" ]

let symlinkFilesFromDirectories =
  [ "MonoBleedingEdge"
    Path.Join("MonoBleedingEdge", "EmbedRuntime")
    "Wrath_Data"
    Path.Join("Wrath_Data", "Plugins")
    Path.Join("Wrath_Data", "Plugins", "x86_64")
    Path.Join("Wrath_Data", "Managed")
    Path.Join("Wrath_Data", "Managed", "UnityModManager") ]

let ignoredFiles =
  [ "blueprints.zip"; Path.Join("Wrath_Data", "boot.config") ]

// let ignoredDirs =
//   [ "Mods" ]

let mutable args = fsi.CommandLineArgs

args <-
  ( if (args.Length < 2) then [||]
    else args |> Array.skip 1 )

let mutable wrathPath = 
    try
        Environment.GetEnvironmentVariable("WrathPath")
    with
    | :? ArgumentException -> 
        printfn "WrathPath environment variable not found"
        ""

printf "Game root directory "

if wrathPath <> "" then
    printf $"[{wrathPath}]"

printf ": "

let pathInput = Console.ReadLine()

wrathPath <- if pathInput = "" then wrathPath else pathInput

if (not << Directory.Exists) wrathPath then
    failwith $"Game directory '{wrathPath}' not found"

let outputPath = "Debug"
let mutable debugWrathPath =
    if Path.IsPathRooted(outputPath) |> not then Path.Join(wrathPath, outputPath)
    else outputPath

printf $"Debug path [{debugWrathPath}]: "

let debugPathInput = Console.ReadLine()

debugWrathPath <- if debugPathInput <> "" then debugPathInput else debugWrathPath

let deleteExisting =
    if Path.Exists(debugWrathPath) then
        printf $"Delete existing files at {debugWrathPath}? [y/N]: "
        Console.ReadLine().ToLower() = "y"
    else false

let verbose = args |> Seq.exists(fun t -> t = "--verbose" || t = "-v")

let printVerbose (s : string) =
    if verbose then printfn "%s" s
    
let createSymlinks() =

    // let ignoredDirs = debugWrathPath :: (ignoredDirs |> List.map (fun d -> Path.Join (wrathPath, d)))

    if deleteExisting then
        if Path.Exists debugWrathPath then
            printfn "Deleting existing debug directory"
            Directory.Delete(debugWrathPath, true)

    if (not << Path.Exists) debugWrathPath then
        printfn "Debug directory does not exist, creating"
        Directory.CreateDirectory(debugWrathPath) |> ignore

    let dirs = wrathPath :: (symlinkFilesFromDirectories |> List.map (fun d -> Path.Join(wrathPath, d)))

    for path in dirs do
        let relativeDir = Path.GetRelativePath(wrathPath, path)
        let linksDir = Path.Join(debugWrathPath, relativeDir)

        printVerbose $"Directory: {path}"
        if (not << Directory.Exists) path then
            failwith "Target dir not found"

        if (not << Directory.Exists) linksDir then
            printVerbose $"  Creating directory: {path}"
            Directory.CreateDirectory linksDir |> ignore

        let innerDirs = 
            Directory.GetDirectories(path)
            |> Seq.where (fun d -> dirs |> Seq.contains d |> not)
            // |> Seq.where (fun d -> ignoredDirs |> Seq.contains d |> not)

        for dir in innerDirs do
            printVerbose $"  Directory {dir}"
            let relativePath = Path.GetRelativePath(wrathPath, dir)
            printVerbose $"    Relative Path: {relativePath}"
            let targetPath = Path.Join(wrathPath, relativePath)
            let linkPath = Path.Join(debugWrathPath, relativePath)

            if (not << Directory.Exists) linkPath then
                printVerbose $"    Creating directory symlink: {linkPath} -> {targetPath}"
                Directory.CreateSymbolicLink(linkPath, targetPath) |> ignore

        for f in Directory.GetFiles(path) do
            printVerbose $"  File: {f}"
            let relativePath = Path.GetRelativePath(wrathPath, f)
            printVerbose $"    Relative Path: {relativePath}"
            
            if ignoredFiles |> Seq.contains relativePath || unityDebugFiles |> Seq.contains relativePath then
                printVerbose "    ignored"
            else
                let targetPath = Path.Join(wrathPath, relativePath)
                let linkPath = Path.Join(debugWrathPath, relativePath)
                
                if (not << File.Exists) linkPath then
                    if targetPath.EndsWith(".exe") || targetPath.EndsWith(".dll") then
                        printVerbose $"    Copying file '{targetPath}' to '{linkPath}'"  
                        File.Copy(targetPath, linkPath)
                    else
                        printVerbose $"    Creating file symlink: {linkPath}-> {targetPath}"
                        File.CreateSymbolicLink(linkPath, targetPath) |> ignore
    
    printfn "Creating boot.config"
    use file = File.CreateText(Path.Join(debugWrathPath, "Wrath_Data", "boot.config"))
    file.Write boot_cfg

    printfn "Creating steam_appid.txt"
    use file = File.CreateText(Path.Join(debugWrathPath, "steam_appid.txt"))
    file.Write steam_appid

    for f in unityDebugFiles do
        let copyTo = Path.Join(debugWrathPath, f)
        if (not << Path.Exists) copyTo then
            printVerbose $"Copying file '{f}' to '{copyTo}'" 
            File.Copy(f, copyTo) |> ignore

createSymlinks()
