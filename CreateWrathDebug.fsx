open System
open System.IO

let boot_cfg = """wait-for-managed-debugger=1
player-connection-debug=1
"""

let steam_appid = "1184370"

let unityDebugFiles = [ "UnityPlayer.dll"; "WinPixEventRuntime.dll" ]

let symlinkFolders = [ "Bundles"; "Mods"; "Wrath_Data\\StreamingAssets" ]

let copyDirs = [ "MonoBleedingEdge"; "Wrath_Data";  ]

let skipFiles = [ "blueprints.zip" ]

let mutable args = fsi.CommandLineArgs

args <- if (args.Length < 2) then [||] else args |> Array.skip 1

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

wrathPath <- (if pathInput = "" then wrathPath else pathInput) |> Path.GetFullPath

if (not << Directory.Exists) wrathPath then
    failwith $"Game directory '{wrathPath}' not found"
    
let mutable debugWrathPath =
    Path.Join(
        wrathPath
        |> Path.GetFullPath
        |> Path.GetDirectoryName,
        $@"{(DirectoryInfo wrathPath).Name} Debug")

printf $"Debug path [{debugWrathPath}]: "

let rec getAllParentDirNames (dir : string) =
    seq {
        let parent = dir |> Path.GetDirectoryName
        if not (isNull parent) then
            yield parent
            yield! getAllParentDirNames parent
    }

if getAllParentDirNames debugWrathPath |> Seq.contains wrathPath then
    failwith "Do not place debug directory inside game directory"

let debugPathInput = Console.ReadLine()

debugWrathPath <- if debugPathInput <> "" then debugPathInput |> Path.GetFullPath else debugWrathPath

let deleteExisting =
    if Path.Exists(debugWrathPath) then
        printf $"Remove existing directory {debugWrathPath}? [y/N]: "
        Console.ReadLine().ToLower() = "y"
    else false

let overwriteExisting =
    if not deleteExisting then
        printf $"Overwrite existing files? [y/N]: "
        Console.ReadLine().ToLower() = "y"
    else false

let verbose = args |> Seq.exists(fun t -> t = "--verbose" || t = "-v")

let printfnVerbose (s : string) =
    if verbose then printfn "%s" s

let createBootConfig() =
    let path = Path.Join(debugWrathPath, "Wrath_Data", "boot.config")

    if (path |> File.Exists && overwriteExisting) then
        printfnVerbose $"delete {path}"
        File.Delete(path)

    printfnVerbose $"create {path}"
    use file = File.CreateText(path)
    file.Write boot_cfg

let createSteam_appid() =
    let path = Path.Join(debugWrathPath, "steam_appid.txt")

    if (path |> File.Exists && overwriteExisting) then
        printfnVerbose $"delete {path}"
        File.Delete(path)

    printfnVerbose $"create {path}"
    use file = File.CreateText(path)
    file.Write steam_appid

// let getFullPath (rootPath : string) (relativePath : string) =
//     if relativePath |> Path.IsPathRooted then relativePath
//     else
//         Path.Join(rootPath, relativePath)
    
let rec createWrathDebug nested dir =
    if deleteExisting then Directory.Delete(debugWrathPath, true)

    let outDir =
        Path.Join(debugWrathPath, Path.GetRelativePath(wrathPath, dir))
        |> Path.GetFullPath

    if not (outDir |> Directory.Exists) then
        printfnVerbose $"create {outDir}"

        Directory.CreateDirectory(outDir) |> ignore

    for file in Directory.GetFiles(dir) do
        if not (skipFiles
            |> Seq.map (fun f -> Path.Join(wrathPath, f))
            |> Seq.contains file) then

            let relativePath =
                Path.Join(Path.GetRelativePath(wrathPath, file))

            let copyFrom =
                if unityDebugFiles |> Seq.contains relativePath then
                    relativePath |> Path.GetFullPath
                else
                    Path.Join(wrathPath, relativePath)
            
            let copyTo = 
                Path.Join(
                    debugWrathPath,
                    Path.GetRelativePath(wrathPath, dir),
                    file |> Path.GetFileName)
                |> Path.GetFullPath
            
            if File.Exists copyTo && not overwriteExisting then
                failwith $"{copyTo} exists"

            printfnVerbose $"copy {copyFrom} -> {copyTo}"
            
            File.Copy(copyFrom, copyTo, overwriteExisting)

        else printfnVerbose $"skipping {file}"

    for d in Directory.GetDirectories(dir) do
        if symlinkFolders |> Seq.map (fun d -> Path.Join(wrathPath, d)) |> Seq.contains d then
            let symlink =
                Path.Join(
                    debugWrathPath,
                    Path.GetRelativePath(wrathPath, d))
                |> Path.GetFullPath

            printfnVerbose $"symlink {d} -> {symlink}"
            if (DirectoryInfo(symlink)).LinkTarget |> isNull |> not then
                Directory.Delete(symlink, false)

            Directory.CreateSymbolicLink(symlink, d).ToString() |> ignore

        else if copyDirs |> Seq.map (fun d -> Path.Join(wrathPath, d)) |> Seq.contains d || nested then
            // let newDir =
            //     Path.Combine(
            //         debugWrathPath,
            //         Path.GetRelativePath(wrathPath, d))
            //     |> Path.GetFullPath

            createWrathDebug true d
            
createWrathDebug false wrathPath

createBootConfig()

createSteam_appid()
