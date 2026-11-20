#Requires -Version 7.0
<#
.SYNOPSIS
    构建当前 x86 插件与资源核对证据，仅写项目产物；部署计划单独准备。
#>
param([string]$GamePath, [string]$ReleaseDirectory, [string]$BepInExPath)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/FileSafety.ps1"
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $GamePath) {
    $environment = Get-Content -LiteralPath (Join-Path $workspace 'local/game-environment.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $GamePath = $environment.game.installDirectory
}
$game = [IO.Path]::GetFullPath($GamePath)
$identity = Get-Content -LiteralPath (Join-Path $workspace 'deployment/game-fingerprint.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-GameIdentity $game $identity
$uiResources=Get-Content -Raw -Encoding UTF8 (Join-Path $workspace 'deployment/ui-resource-fingerprint.json') | ConvertFrom-Json
foreach($resource in $uiResources.files){Assert-Hash (Hash-Of (Child-Path $game $resource.path)) $resource.sha256 '已审界面资源'}
$project = Join-Path $workspace 'Pwaat.Accessibility.csproj'
$loader = if ($BepInExPath) { [IO.Path]::GetFullPath($BepInExPath) } else { Join-Path $workspace 'tools/bepinex/5.4.23.5-x86' }
$loaderEvidence = Get-Content -LiteralPath (Join-Path $workspace 'deployment/loader-package.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($reference in $loaderEvidence.buildReferences) {
    Assert-Hash (Hash-Of (Child-Path $loader $reference.path)) $reference.sha256 '加载器引用'
}
if (-not $ReleaseDirectory) { $ReleaseDirectory = Join-Path $workspace 'artifacts/menus' }
$release = Child-Path $workspace ([IO.Path]::GetRelativePath($workspace, [IO.Path]::GetFullPath($ReleaseDirectory)))
Assert-Separate $game $workspace
Assert-NoReparse $project
[xml]$projectXml = Get-Content -LiteralPath $project -Raw -Encoding UTF8
if ($projectXml.SelectNodes('//Target').Count) { throw '项目有自定义构建 Target，须先复核构建是否写入游戏。' }
$managed = Child-Path $game 'PWAAT_Data/Managed'
dotnet build $project --configuration Release --output $release "-p:GameManagedDirectory=$managed" "-p:BepInExDirectory=$loader" --nologo
if ($LASTEXITCODE -ne 0) { throw "构建失败，退出码 $LASTEXITCODE。" }
$dll = Child-Path $release 'Pwaat.Accessibility.dll'
if ((Read-PeMachine $dll) -ne 332) { throw '构建产物不是 x86 PE。' }
# Read managed resources as PE data; never execute or load an x86 assembly into this host.
function Read-EmbeddedText($Pe, $Metadata, [string]$Name) {
    foreach ($handle in $Metadata.ManifestResources) {
        $resource = $Metadata.GetManifestResource($handle)
        if ($Metadata.GetString($resource.Name) -cne $Name) { continue }
        if (-not $resource.Implementation.IsNil) { throw '构建资源必须内嵌。' }
        $block = $Pe.GetSectionData($Pe.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress)
        $reader = $block.GetReader()
        $reader.Offset = [int]$resource.Offset
        $length = $reader.ReadInt32()
        if ($length -lt 0 -or $length -gt $reader.RemainingBytes) { throw '构建资源长度无效。' }
        return [Text.UTF8Encoding]::new($false,$true).GetString($reader.ReadBytes($length))
    }
    throw "构建缺少内嵌资源：$Name"
}
Add-Type -AssemblyName System.Reflection.Metadata
$assemblyStream = [IO.File]::OpenRead($dll)
$pe = [Reflection.PortableExecutable.PEReader]::new($assemblyStream)
try {
    $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    [xml]$messages = Read-EmbeddedText $pe $metadata 'Pwaat.Accessibility.SpeechProbeMessages.xml'
    [xml]$glyphs = Read-EmbeddedText $pe $metadata 'Pwaat.Accessibility.ControlGlyphs.xml'
    [xml]$uiMessages = Read-EmbeddedText $pe $metadata 'Pwaat.Accessibility.UiMessages.xml'
    [xml]$artwork = Read-EmbeddedText $pe $metadata 'Pwaat.Accessibility.ArtworkDescriptions.xml'
    [xml]$imageText = Read-EmbeddedText $pe $metadata 'Pwaat.Accessibility.ImageText.xml'
    [xml]$investigationLabels = Read-EmbeddedText $pe $metadata 'Pwaat.Accessibility.InvestigationLabels.xml'
    [xml]$picturePointing = Read-EmbeddedText $pe $metadata 'Pwaat.Accessibility.PicturePointing.xml'
    [xml]$videoFrames = Read-EmbeddedText $pe $metadata 'Pwaat.Accessibility.VideoFrames.xml'
    if((Read-EmbeddedText $pe $metadata 'Pwaat.Accessibility.VideoFrames.xml') -cne (Get-Content -Raw -Encoding UTF8 (Join-Path $workspace 'resources/VideoFrames.xml'))){throw '录像时钟内嵌资源与源码不一致。'}
    $courtImageText = Read-EmbeddedText $pe $metadata 'Pwaat.Accessibility.CourtRecordImages.xml'
    [xml]$courtRecordImages = $courtImageText
    $apiHash = (Read-EmbeddedText $pe $metadata 'Pwaat.Accessibility.GameAssembly.sha256').Trim()
    foreach ($handle in $metadata.ManifestResources) {
        $resource = $metadata.GetManifestResource($handle)
        if ($metadata.GetString($resource.Name) -cin @('Pwaat.Accessibility.Tolk.sha256','Pwaat.Accessibility.ScienceGeometry.xml')) { throw '构建仍内嵌了私有旧资源。' }
    }
} finally { $pe.Dispose(); $assemblyStream.Dispose() }
foreach ($key in @('science.help','science.help-repeat','science.title','science.point','science.examined','science.no-points','science.point-unavailable','science.outside')) {
    $cultures = @($uiMessages.messages.message | Where-Object key -CEQ $key | ForEach-Object { [string]$_.culture })
    if (@(Compare-Object @('ja','en','fr','de','ko','zh-Hans','zh-Hant','pt-BR','es-419') $cultures).Count -or $cultures.Count -ne 9) { throw "科学搜查九语提示不完整：$key" }
}

[xml]$scienceLabels = Get-Content -Raw -Encoding UTF8 (Join-Path $workspace 'resources/SciencePointLabels.xml')
$scienceLabelSummary = Get-Content -Raw -Encoding UTF8 (Join-Path $workspace 'deployment/science-point-labels-summary.json') | ConvertFrom-Json
Assert-Hash (Hash-Of (Join-Path $workspace 'resources/SciencePointLabels.xml')) $scienceLabelSummary.resourceSha256 '科学搜查点名称资源'
if (@($scienceLabels.sciencePointLabels.point).Count -ne $scienceLabelSummary.points) { throw '科学搜查点名称数量不符。' }
foreach ($point in $scienceLabels.sciencePointLabels.point) {
    $cultures = @($point.text | ForEach-Object { [string]$_.culture })
    if ($cultures.Count -ne 9 -or @(Compare-Object @('ja','en','fr','de','ko','zh-Hans','zh-Hant','pt-BR','es-419') $cultures).Count) { throw '科学搜查点名称九语不完整。' }
    foreach ($text in $point.text) { if ([string]::IsNullOrWhiteSpace($text.InnerText)) { throw '科学搜查点名称为空。' } }
}
foreach ($source in $scienceLabels.sciencePointLabels.source) { Assert-Hash (Hash-Of (Child-Path $game $source.path)) $source.sha256 '科学搜查点名称模型来源' }

$expectedCultures = @('ja','en','fr','de','ko','zh-Hans','zh-Hant','pt-BR','es-419')
$actualCultures = @($messages.messages.message | ForEach-Object { [string]$_.culture })
if (@(Compare-Object $expectedCultures $actualCultures).Count -or @($actualCultures | Select-Object -Unique).Count -ne 9) { throw '九语测试资源不完整或重复。' }
foreach ($message in $messages.messages.message) { if ([string]::IsNullOrWhiteSpace($message.InnerText)) { throw '本地化测试文案为空。' } }
if (@($artwork.descriptions.image).Count -ne 850) { throw '图像口述映射总数不符。' }
$pictureKeys=@{}
foreach($row in $artwork.descriptions.image){
    $key=[string]$row.path+[string]$row.bundle+':'+[string]$row.sprite
    if($pictureKeys.ContainsKey($key)){throw "图像映射重复：$key"};$pictureKeys[$key]=$true
    $cultures=@($row.text | ForEach-Object {[string]$_.culture})
    if(@(Compare-Object $expectedCultures $cultures).Count -or $cultures.Count -ne 9){throw "图像九语不完整：$key"}
    foreach($text in $row.text){if([string]::IsNullOrWhiteSpace($text.InnerText)){throw "图像口述为空：$key"}}
}
if(@($imageText.images.image).Count -ne 210){throw '实际图片文字映射不完整。'}
Assert-Hash $apiHash ($identity.gameChecks | Where-Object path -EQ 'PWAAT_Data/Managed/Assembly-CSharp.dll').sha256 '内嵌游戏接口标识'
$labelSummary = Get-Content -Raw -Encoding UTF8 (Join-Path $workspace 'deployment/investigation-labels-summary.json') | ConvertFrom-Json
if (@($investigationLabels.investigationLabels.point).Count -ne $labelSummary.runtimeIdentities) { throw '场景调查名称映射未完整内嵌构建。' }
$courtSummary = Get-Content -Raw -Encoding UTF8 (Join-Path $workspace 'deployment/court-record-images-summary.json') | ConvertFrom-Json
if ($courtRecordImages.courtRecordImages.pending -eq 'true') { throw '法庭档案图片资源仍为开发占位，不能交付。' }
if (@($courtRecordImages.courtRecordImages.image).Count -ne $courtSummary.imageIdentities -or @($courtRecordImages.courtRecordImages.hint).Count -ne $courtSummary.hintIdentities) { throw '法庭档案图片／提示资源未完整打包。' }
$courtKeys = @{}
foreach ($image in $courtRecordImages.courtRecordImages.image) {
    $key = [string]$image.path + [string]$image.bundle + ':' + [string]$image.sprite
    if ($courtKeys.ContainsKey($key)) { throw "法庭档案图片身份重复：$key" }; $courtKeys[$key] = $true
    $cultures = @($image.description | ForEach-Object { [string]$_.culture })
    if (@(Compare-Object $expectedCultures $cultures).Count -or $cultures.Count -ne 9) { throw "法庭档案图片九语不完整：$key" }
}
foreach ($source in $courtRecordImages.courtRecordImages.source) { Assert-Hash (Hash-Of (Child-Path $game $source.path)) $source.sha256 '已审法庭档案图片来源' }
# File hash is checked separately because XML serialization normalizes whitespace.
Assert-Hash (Hash-Of (Join-Path $workspace 'resources/CourtRecordImages.xml')) $courtSummary.resourceSha256 '法庭档案编译资源'
$resourceHash = [Security.Cryptography.SHA256]::Create()
try { $embeddedCourtHash = [Convert]::ToHexString($resourceHash.ComputeHash([Text.Encoding]::UTF8.GetBytes($courtImageText))) } finally { $resourceHash.Dispose() }
Assert-Hash $embeddedCourtHash $courtSummary.resourceSha256 'DLL实际内嵌法庭档案资源'
$pictureSummary = Get-Content -Raw -Encoding UTF8 (Join-Path $workspace 'deployment/picture-pointing-summary.json') | ConvertFrom-Json
if (@($picturePointing.picturePointing.image).Count -ne $pictureSummary.imageIdentities) { throw '图片指证图像索引数量不符。' }
foreach ($image in $picturePointing.picturePointing.image) {
    $cultures = @($image.description | ForEach-Object { [string]$_.culture })
    if (@(Compare-Object $expectedCultures $cultures).Count -or $cultures.Count -ne 9) { throw "图片指证全景描述九语不完整：$($image.title)/$($image.background)/$($image.sprite)" }
    foreach ($region in $image.region) {
        $regionCultures = @($region.text | ForEach-Object { [string]$_.culture })
        if (@(Compare-Object $expectedCultures $regionCultures).Count -or $regionCultures.Count -ne 9) { throw "图片指证位置九语不完整：$($image.title)/$($image.background)/$($region.id)" }
        foreach ($coordinate in @('x0','y0','x1','y1','x','y')) {
            $parsed = 0.0
            if (-not [double]::TryParse([string]$region.rect.GetAttribute($coordinate),[Globalization.NumberStyles]::Float,[Globalization.CultureInfo]::InvariantCulture,[ref]$parsed)) { throw "图片指证坐标无效：$coordinate" }
        }
    }
}
if (@($picturePointing.picturePointing.seal).Count -ne $pictureSummary.seals) { throw '图片指证可见叠层未完整内嵌。' }
foreach ($source in $picturePointing.picturePointing.source) { Assert-Hash (Hash-Of (Child-Path $game ('PWAAT_Data/StreamingAssets/' + $source.path))) $source.sha256 '已审图片指证来源' }
$labelKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($point in $investigationLabels.investigationLabels.point) {
    if ($point.GetAttribute('match') -cnotmatch '^[0-9a-f]{64}$' -or -not $labelKeys.Add($point.GetAttribute('match')) -or $point.Attributes.Count -ne 1) { throw '调查名称必须仅保留唯一匹配摘要，不得内嵌原生坐标。' }
    $cultures = @($point.text | ForEach-Object { [string]$_.culture })
    if (-not $cultures.Count -or @($cultures | Select-Object -Unique).Count -ne $cultures.Count -or @($cultures | Where-Object { $_ -notin $expectedCultures }).Count) { throw '场景调查名称语言资源不完整或重复。' }
    foreach ($text in $point.text) { if ([string]::IsNullOrWhiteSpace($text.InnerText)) { throw '场景调查名称为空。' } }
}
foreach ($source in $investigationLabels.investigationLabels.source) { Assert-Hash (Hash-Of (Child-Path $game ('PWAAT_Data/StreamingAssets/' + $source.path))) $source.sha256 '已审场景调查图片' }
if (@($glyphs.glyphs.glyph).Count -ne 179) { throw '已核对键帽映射数量不符。' }
foreach ($key in @('vase-show.scene','vase-show.axis-0','vase-show.axis-1','vase-show.axis-2','vase-show.near','vase-show.aligned','vase-show.crooked','vase-show.ready','vase-show.angles','vase-show.help-repeat','vase-show.help-f3','vase-show.help','read_failed','start_unavailable','key.up','key.down','key.left','key.right','pad.a','pad.b','pad.y','pad.x','pad.lb','pad.rb','pad.lt','pad.rt','menu.navigation','key.capture','pad.menu','pad.view','pad.rs.press','pad.rs.down','pad.rs.up','image.original','image.description','picture-pointing.help','picture-pointing.outside','picture-pointing.unknown-picture','picture-pointing.unavailable','picture-pointing.title','picture-pointing.markers','picture-pointing.help-repeat','story.evidence-name','luminol.point','luminol.unrevealed','luminol.revealed','luminol.investigated','luminol.outside','luminol.left-scene','luminol.right-scene','luminol.no-points','luminol.point-unavailable','luminol.help-repeat','luminol.help','luminol.reaction','luminol.reveal-ready','luminol.reveal-ready-key','luminol.viewing','vase-puzzle.piece','vase-puzzle.scene','vase-puzzle.scene-single','vase-puzzle.success','vase-puzzle.failure','vase-puzzle.complete','vase-puzzle.help-repeat','vase-puzzle.help','vase-puzzle.angle','fingerprint.finger','fingerprint.outside','fingerprint.no-fingers','fingerprint.dusting','fingerprint.covered','fingerprint.revealed','fingerprint.progress','fingerprint.match','fingerprint.no-match','fingerprint.help-select','fingerprint.help-dust','fingerprint.help-repeat','fingerprint.scene-0','fingerprint.scene-1','fingerprint.scene-2','safe-password.title','safe-password.focus','safe-password.delete','safe-password.entered','safe-password.empty','safe-password.digit','safe-password.deleted','safe-password.help')) {
    $cultures = @($uiMessages.messages.message | Where-Object key -EQ $key | ForEach-Object { [string]$_.culture })
    if (@(Compare-Object $expectedCultures $cultures).Count) { throw "模组提示翻译不完整：$key" }
}
$videoGroups=@($uiMessages.messages.message|Where-Object {$_.key -like 'video.*'}|Group-Object key)
if($videoGroups.Count -ne 41){throw '录像九语提示键数不完整。'}
foreach($group in $videoGroups){if($group.Count -ne 9 -or @(Compare-Object $expectedCultures @($group.Group.culture)).Count){throw "录像九语提示不完整：$($group.Name)"}}
$nextClock=1
foreach($clock in $videoFrames.videoFrames.clock){if([int]$clock.first -ne $nextClock -or [int]$clock.last -lt [int]$clock.first){throw '录像时钟分段不连续。'};$nextClock=[int]$clock.last+1}
if($nextClock -ne 1421 -or @($videoFrames.videoFrames.clock).Count -ne 48){throw '录像时钟覆盖不完整。'}
$evidence = [ordered]@{
    buildId = [string]$projectXml.Project.PropertyGroup.InformationalVersion
    targetFramework = 'net35'
    platform = 'x86'
    sha256 = Hash-Of $dll
    tolkRuntime = 'compatible-x86-from-plugin-directory'
    probeMessageCultures = $actualCultures
    deployed = $false
    executed = $false
}
$evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $release 'build-evidence.json') -Encoding utf8NoBOM
Write-Output '构建和九语资源核对完成；未部署或执行游戏。'
