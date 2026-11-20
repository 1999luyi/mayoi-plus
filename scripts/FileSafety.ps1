#Requires -Version 7.0
# Shared path and hash checks. No file mutation occurs in this helper.
function Assert-NoReparse([string]$Path) {
    $probe=[IO.Path]::GetFullPath($Path)
    while($probe) {
        if((Test-Path -LiteralPath $probe) -and
            ((Get-Item -LiteralPath $probe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "拒绝重解析路径：$probe"
        }
        $probe=[IO.Path]::GetDirectoryName($probe)
    }
}
function Child-Path([string]$Root,[string]$Relative) {
    if([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative) -or
        $Relative.Contains(':') -or ($Relative -split '[\\/]') -contains '..') { throw "清单须为范围内相对路径：$Relative" }
    $fullRoot=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Root))
    $full=[IO.Path]::GetFullPath([IO.Path]::Combine($fullRoot,$Relative))
    if(-not $full.StartsWith($fullRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw "路径越界：$Relative" }
    Assert-NoReparse $full
    return $full
}
function Hash-Of([string]$Path) {
    if(-not(Test-Path -LiteralPath $Path -PathType Leaf)){return $null}
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}
function Assert-Hash([string]$Actual,[string]$Expected,[string]$Label) {
    if($Expected -notmatch '^[a-fA-F0-9]{64}$' -or $Actual -ne $Expected) { throw "$Label 指纹不符或清单未填写。" }
}
function Assert-Separate([string]$First,[string]$Second) {
    $a=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($First))
    $b=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Second))
    if($a -eq $b -or $a.StartsWith($b+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
        $b.StartsWith($a+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw '游戏、发行与备份目录必须相互独立。' }
}


function Read-PeMachine([string]$Path) {
    Assert-NoReparse $Path
    $stream = [IO.File]::OpenRead($Path)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5a4d) { throw '不是 PE 文件。' }
        $stream.Position = 0x3c
        $offset = $reader.ReadInt32()
        if ($offset -lt 0 -or $offset -gt $stream.Length - 6) { throw 'PE 头位置无效。' }
        $stream.Position = $offset
        if ($reader.ReadUInt32() -ne 0x00004550) { throw 'PE 签名无效。' }
        return [int]$reader.ReadUInt16()
    } finally { $reader.Dispose() }
}
function Assert-GameIdentity([string]$Game, $Identity) {
    Assert-NoReparse $Game
    $executable = Child-Path $Game $Identity.gameExecutable
    Assert-Hash (Hash-Of $executable) $Identity.expectedGameSha256 '游戏程序'
    if ($Identity.gameMachine -and (Read-PeMachine $executable) -ne $Identity.gameMachine) { throw '游戏 PE 架构不符。' }
    foreach ($check in @($Identity.gameChecks) + @($Identity.requiredFiles)) {
        if ($null -eq $check) { continue }
        Assert-Hash (Hash-Of (Child-Path $Game $check.path)) $check.sha256 '游戏关键文件'
    }
}
function Assert-NoRunningExecutable([string]$Target, [object[]]$Running) {
    $targetFull = [IO.Path]::GetFullPath($Target)
    foreach ($process in $Running) {
        if ([string]::IsNullOrWhiteSpace([string]$process.Path)) { throw "无法核对进程 $($process.Id) 的程序路径，停止部署或恢复。" }
        $runningFull = [IO.Path]::GetFullPath([string]$process.Path)
        if ([string]::Equals($runningFull, $targetFull, [StringComparison]::OrdinalIgnoreCase)) {
            throw "请先正常退出目标目录的游戏 $targetFull，再部署或恢复。"
        }
    }
}
function Assert-GameStopped([string]$Executable, [string]$GameDirectory) {
    $name = [IO.Path]::GetFileNameWithoutExtension($Executable)
    $matching = @(Get-Process -Name $name -ErrorAction SilentlyContinue)
    if (-not $matching.Count) { return }
    if (-not $GameDirectory) { throw "请先正常退出 $name，再部署或恢复。" }
    $running = [Collections.Generic.List[object]]::new()
    foreach ($process in $matching) {
        $processPath = $null
        try { $processPath = $process.Path } catch { }
        if ([string]::IsNullOrWhiteSpace($processPath)) {
            $native = Get-CimInstance Win32_Process -Filter ("ProcessId=" + $process.Id) -ErrorAction Stop
            if (-not $native) { continue }
            $processPath = $native.ExecutablePath
        }
        $running.Add([pscustomobject]@{ Id = $process.Id; Path = $processPath })
    }
    Assert-NoRunningExecutable (Child-Path $GameDirectory $Executable) $running.ToArray()
}
function Assert-ProxyRegistry {
    foreach ($view in @([Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64)) {
        $registry = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
        $key = $null
        try {
            $key = $registry.OpenSubKey('SYSTEM\CurrentControlSet\Control\Session Manager\KnownDLLs')
            if (-not $key) { throw '无法核对 KnownDLLs。' }
            foreach ($name in $key.GetValueNames()) {
                if ($name -match '^winhttp(\.dll)?$' -or [string]$key.GetValue($name) -match '^winhttp\.dll$') {
                    throw 'WINHTTP 在 KnownDLLs 中，停止代理部署。'
                }
            }
        } finally { if ($key) { $key.Dispose() }; $registry.Dispose() }
    }
}

function Assert-NoUnownedPlugins([string]$Game, $Entries) {
    if (-not @($Entries | Where-Object { $_.shared -and $_.target -ieq 'winhttp.dll' }).Count) { return }
    $plugins = Child-Path $Game 'BepInEx/plugins'
    if (-not (Test-Path -LiteralPath $plugins -PathType Container)) { return }
    $owned = @($Entries | ForEach-Object { Child-Path $Game $_.target })
    foreach ($plugin in Get-ChildItem -LiteralPath $plugins -Filter '*.dll' -Recurse -File) {
        Assert-NoReparse $plugin.FullName
        if ($plugin.FullName -notin $owned) { throw '仍有其他插件使用共享加载器；请先恢复本项目插件或核对共享归属。' }
    }
}
