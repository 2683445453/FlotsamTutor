#requires -Version 7.2
param(
    [string]$GameDirectory = 'D:\SteamLibrary\steamapps\common\Flotsam'
)

$ErrorActionPreference = 'Stop'
$gameManaged = Join-Path (Resolve-Path -LiteralPath $GameDirectory).ProviderPath 'Flotsam_Data/Managed'
$build = Join-Path $PSScriptRoot 'build'
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $build,$dist | Out-Null

$loaderVersion = '5.4.23.6'
$loaderName = "BepInEx_win_x64_$loaderVersion.zip"
$loaderZip = Join-Path $build $loaderName
if (!(Test-Path -LiteralPath $loaderZip)) {
    Invoke-WebRequest "https://github.com/BepInEx/BepInEx/releases/download/v$loaderVersion/$loaderName" -OutFile $loaderZip
}
if ((Get-FileHash -LiteralPath $loaderZip -Algorithm SHA256).Hash -ne
    '6C57A714A892EFB4E9E1FEF63D608A3391F30EC984031C465404E4EE381A0755') {
    throw "BepInEx archive checksum mismatch: $loaderZip"
}

$stage = Join-Path $build ('release-' + [guid]::NewGuid().ToString('N'))
$payload = Join-Path $stage 'payload'
$loader = Join-Path $payload 'loader'
New-Item -ItemType Directory -Force -Path $loader | Out-Null
try {
    Expand-Archive -LiteralPath $loaderZip -DestinationPath $loader
    $project = Join-Path $PSScriptRoot 'src/Flotsam.Tutor.csproj'
    $core = Join-Path $loader 'BepInEx/core'
    & dotnet build $project -c Release -o (Join-Path $build 'plugin') "-p:GameManaged=$gameManaged" "-p:BepInExCore=$core" --nologo
    if ($LASTEXITCODE) { throw 'Managed build failed.' }
    Copy-Item -LiteralPath (Join-Path $build 'plugin/Flotsam.Tutor.dll') -Destination $payload
    foreach ($file in 'Install.cmd','Uninstall.cmd','Install-Mod.ps1','使用说明.txt','LICENSE','LICENSE.BepInEx.txt') {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $stage
    }
    $package = Join-Path $dist 'FlotsamTutor.zip'
    $nextPackage = Join-Path $build ([guid]::NewGuid().ToString('N') + '.zip')
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $nextPackage)
    Move-Item -LiteralPath $nextPackage -Destination $package -Force
} finally {
    $resolvedStage = (Resolve-Path -LiteralPath $stage).ProviderPath
    if ([IO.Path]::GetDirectoryName($resolvedStage) -eq (Resolve-Path -LiteralPath $build).ProviderPath) {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
