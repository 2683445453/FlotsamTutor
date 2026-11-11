param(
    [ValidateSet('Install', 'Uninstall')]
    [string]$Mode = 'Install',
    [string]$GameDirectory
)

$ErrorActionPreference = 'Stop'
$expectedHashes = @(
    '5B5C9F730ED71D5F66C0267064FC57667D2FF5644A018E9189B140CE025C1CD8',
    '24830B47DB1BC737C107425D131DA4A6F36715BF19416AF1E4A1212875E9E698'
)

function Test-GameDirectory([string]$Path) {
    $Path -and (Test-Path -LiteralPath ([IO.Path]::Combine($Path, 'Flotsam.exe')) -PathType Leaf -ErrorAction SilentlyContinue) -and
        (Test-Path -LiteralPath ([IO.Path]::Combine($Path, 'Flotsam_Data/Managed/Assembly-CSharp.dll')) -PathType Leaf -ErrorAction SilentlyContinue)
}

function Find-GameDirectory {
    if (Test-GameDirectory $PSScriptRoot) { return $PSScriptRoot }

    $epicManifests = Join-Path $env:ProgramData 'Epic/EpicGamesLauncher/Data/Manifests'
    $candidates = @(
        Get-ChildItem -LiteralPath $epicManifests -Filter '*.item' -File -ErrorAction SilentlyContinue |
            ForEach-Object {
                try {
                    $manifest = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
                    if ($manifest.LaunchExecutable -match '(^|[\\/])Flotsam\.exe$') {
                        $manifest.InstallLocation
                    }
                } catch { Write-Verbose "无法读取 Epic 清单 $($_.Name)" }
            }
        $steam = Get-ItemPropertyValue -Path 'HKCU:\Software\Valve\Steam' -Name SteamPath -ErrorAction SilentlyContinue
        if ($steam) {
            [IO.Path]::Combine($steam, 'steamapps/common/Flotsam')
            $libraries = [IO.Path]::Combine($steam, 'steamapps/libraryfolders.vdf')
            if (Test-Path -LiteralPath $libraries) {
                foreach ($entry in (Select-String -LiteralPath $libraries -Pattern '^\s*"path"\s+"([^"]+)"')) {
                    [IO.Path]::Combine($entry.Matches[0].Groups[1].Value.Replace('\\','\'), 'steamapps/common/Flotsam')
                }
            }
        }
        Get-PSDrive -PSProvider FileSystem | ForEach-Object {
            Join-Path $_.Root 'SteamLibrary/steamapps/common/Flotsam'
        }
    )
    $found = @($candidates | Where-Object { Test-GameDirectory $_ } |
        ForEach-Object { (Resolve-Path -LiteralPath $_).ProviderPath } | Sort-Object -Unique)
    if ($found.Count -eq 1) { return $found[0] }

    Add-Type -AssemblyName System.Windows.Forms
    $picker = New-Object System.Windows.Forms.FolderBrowserDialog
    $picker.Description = '请选择包含 Flotsam.exe 的游戏目录'
    $picker.ShowNewFolderButton = $false
    try {
        if ($picker.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { return $picker.SelectedPath }
    } finally { $picker.Dispose() }
}

try {
    if (Get-Process Flotsam -ErrorAction SilentlyContinue) { throw '请先退出游戏，再运行安装或卸载。' }
    if (!$GameDirectory) { $GameDirectory = Find-GameDirectory }
    if (!$GameDirectory) { exit 0 }
    if (!(Test-GameDirectory $GameDirectory)) { throw '所选目录中未找到 Flotsam.exe 和游戏数据。' }
    $game = (Resolve-Path -LiteralPath $GameDirectory).ProviderPath
    $target = Join-Path $game 'BepInEx/plugins/FlotsamTutor/Flotsam.Tutor.dll'
    if ($Mode -eq 'Uninstall') {
        if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target }
        Write-Host "已卸载 Flotsam Tutor。BepInEx 可继续供其他模组使用。`n$game"
        exit 0
    }

    $gameAssembly = Join-Path $game 'Flotsam_Data/Managed/Assembly-CSharp.dll'
    if ((Get-FileHash -LiteralPath $gameAssembly -Algorithm SHA256).Hash -notin $expectedHashes) {
        throw '此游戏版本不兼容。当前发行包适用于已验证的 Flotsam 1.0.1f9、1.0.1f10 构建。'
    }
    $plugin = Join-Path $PSScriptRoot 'payload/Flotsam.Tutor.dll'
    $loader = Join-Path $PSScriptRoot 'payload/loader'
    if (!(Test-Path -LiteralPath $plugin -PathType Leaf) -or
        !(Test-Path -LiteralPath (Join-Path $loader 'winhttp.dll') -PathType Leaf)) {
        throw '请完整解压发行包后再运行 Install.cmd。'
    }
    $proxy = Join-Path $game 'winhttp.dll'
    $config = Join-Path $game 'doorstop_config.ini'
    $bepin = Join-Path $game 'BepInEx/core/BepInEx.dll'
    $existing = Test-Path -LiteralPath $bepin -PathType Leaf
    if ($existing) {
        $version = [version](Get-Item -LiteralPath $bepin).VersionInfo.FileVersion
        if ($version.Major -ne 5 -or $version -lt [version]'5.4.23.6' -or
            !(Test-Path -LiteralPath $proxy -PathType Leaf) -or
            !(Test-Path -LiteralPath $config -PathType Leaf)) {
            throw '检测到其他版本或不完整的 BepInEx。请先准备可用的 BepInEx 5.4.23.6 或更高的 5.x 版本。'
        }
        $configuration = Get-Content -LiteralPath $config -Raw
        if ($configuration -notmatch '(?mi)^\s*enabled\s*=\s*true\s*$' -or
            $configuration -notmatch '(?mi)^\s*target_assembly\s*=\s*BepInEx[/\\]core[/\\]BepInEx\.Preloader\.dll\s*$') {
            throw '现有 Doorstop 配置未启用标准 BepInEx 入口，安装器保留了原配置。'
        }
    } elseif (Test-Path -LiteralPath $proxy) {
        throw '游戏目录已有 winhttp.dll，安装器保留了现有加载器。'
    }

    $files = if (!$existing) { @(Get-ChildItem -LiteralPath $loader -File -Recurse -Force) } else { @() }
    foreach ($file in $files) {
        $destination = Join-Path $game $file.FullName.Substring($loader.Length + 1)
        if (Test-Path -LiteralPath $destination) { throw "现有文件冲突：$destination" }
    }

    $copied = New-Object 'System.Collections.Generic.List[string]'
    try {
        foreach ($file in $files) {
            $destination = Join-Path $game $file.FullName.Substring($loader.Length + 1)
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            [IO.File]::Copy($file.FullName, $destination, $false)
            $copied.Add($destination)
        }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        [IO.File]::Copy($plugin, $target, $true)
    } catch {
        foreach ($file in $copied) { Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue }
        throw
    }

    Write-Host "Flotsam Tutor 安装完成。下次从 Epic 或 Steam 启动游戏时自动加载。`n$game"
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
