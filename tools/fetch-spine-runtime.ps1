<#
.SYNOPSIS
    获取 Spine 运行时（C# / MonoGame 版）源码到 GflChibiDesktopLegacy/SpineLibrary/。

.DESCRIPTION
    Spine Runtimes 受 Esoteric Software 的 Spine Runtimes Software License 约束，
    本项目仓库**不包含**其源码，也不会随仓库分发（已加入 .gitignore）。
    使用运行时需要你自行持有有效的 Spine 授权，并自行获取运行时代码。

    本脚本提供两种获取方式：
      1) -FromDir <已有副本目录>：直接从你本地已有的（已改造过的）副本复制，不做任何转换。**推荐**。
         本项目内的运行时代码被改造过（命名空间由 Spine 改为 Spine2_1_25），
         如果你手上有旧副本或备份，用这个方式最稳妥。
      2) 默认方式：从官方仓库下载指定版本并复制到本项目，同时把命名空间改写为 Spine2_1_25。

    获取到的文件不会被 git 跟踪（见 .gitignore）。

.PARAMETER Ref
    官方仓库的 tag/分支，默认 2.1.25（与本项目使用的版本一致）。

.PARAMETER FromDir
    已存在的运行时代码目录（应包含 Skeleton.cs 等），从该目录复制而不改写命名空间。

.PARAMETER TargetDir
    目标目录，默认为仓库内的 GflChibiDesktopLegacy/SpineLibrary/spine-runtimes-2.1.25。

.PARAMETER Force
    目标目录已存在文件时也覆盖。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/fetch-spine-runtime.ps1 -FromDir D:\backup\spine-runtimes-2.1.25

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/fetch-spine-runtime.ps1 -Ref 2.1.25
#>
[CmdletBinding(DefaultParameterSetName = 'Download')]
param(
    [Parameter(ParameterSetName = 'Download')]
    [string]$Ref = '2.1.25',

    [Parameter(Mandatory = $true, ParameterSetName = 'Local')]
    [string]$FromDir,

    [string]$TargetDir,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# 期望的运行时文件清单（相对路径）。上游 spine-csharp/spine-xna 的文件会被归位到这些路径。
$expected = @(
    'Animation.cs', 'AnimationState.cs', 'AnimationStateData.cs', 'Atlas.cs',
    'Bone.cs', 'BoneData.cs', 'Event.cs', 'EventData.cs',
    'IkConstraint.cs', 'IkConstraintData.cs', 'Json.cs',
    'Skeleton.cs', 'SkeletonBinary.cs', 'SkeletonBounds.cs',
    'SkeletonData.cs', 'SkeletonJson.cs', 'Skin.cs', 'Slot.cs', 'SlotData.cs',
    'Attachments/AtlasAttachmentLoader.cs', 'Attachments/Attachment.cs',
    'Attachments/AttachmentLoader.cs', 'Attachments/AttachmentType.cs',
    'Attachments/BoundingBoxAttachment.cs', 'Attachments/MeshAttachment.cs',
    'Attachments/RegionAttachment.cs', 'Attachments/SkinnedMeshAttachment.cs',
    'XnaLoader/MeshBatcher.cs', 'XnaLoader/SkeletonMeshRenderer.cs',
    'XnaLoader/XnaTextureLoader.cs'
)

# 仓库根目录（本脚本位于 tools/ 下）
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $TargetDir) {
    $TargetDir = Join-Path $repoRoot 'GflChibiDesktopLegacy/SpineLibrary/spine-runtimes-2.1.25'
}
$TargetDir = [System.IO.Path]::GetFullPath($TargetDir)

Write-Host "Spine 运行时获取工具" -ForegroundColor Cyan
Write-Host "  目标目录: $TargetDir"
Write-Host ""
Write-Host "提示：Spine Runtimes 受 Esoteric Software 许可约束，需自行持有有效 Spine 授权。" -ForegroundColor Yellow
Write-Host ""

function Copy-RuntimeFiles {
    param([string]$SourceRoot)

    $copied = 0
    $missing = @()
    foreach ($rel in $expected) {
        $src = Join-Path $SourceRoot ($rel -replace '/', '\')
        if (-not (Test-Path $src)) {
            $missing += $rel
            continue
        }
        $dst = Join-Path $TargetDir ($rel -replace '/', '\')
        $dstDir = Split-Path -Parent $dst
        if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
        Copy-Item -LiteralPath $src -Destination $dst -Force
        $copied++
    }
    return [pscustomobject]@{ Copied = $copied; Missing = $missing }
}

if ($PSCmdlet.ParameterSetName -eq 'Local') {
    # 方式一：从已有副本复制（不做命名空间改写）
    $srcRoot = [System.IO.Path]::GetFullPath($FromDir)
    if (-not (Test-Path $srcRoot)) { throw "源目录不存在：$srcRoot" }

    # 允许传入的目录里再嵌套一层（例如指向 SpineLibrary 或 spine-runtimes-2.1.25 均可）
    if (-not (Test-Path (Join-Path $srcRoot 'Skeleton.cs'))) {
        $nested = Get-ChildItem -Path $srcRoot -Recurse -Filter 'Skeleton.cs' -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($nested) {
            $srcRoot = $nested.DirectoryName
            Write-Host "自动定位到源目录：$srcRoot"
        }
    }

    if (-not $Force -and (Test-Path $TargetDir) -and (Get-ChildItem $TargetDir -Recurse -File -ErrorAction SilentlyContinue)) {
        Write-Host "目标目录已有文件，未指定 -Force 时不覆盖。" -ForegroundColor Yellow
        exit 0
    }

    if (-not (Test-Path $TargetDir)) { New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null }
    $r = Copy-RuntimeFiles -SourceRoot $srcRoot
    Write-Host ("已复制 {0}/{1} 个文件。" -f $r.Copied, $expected.Count) -ForegroundColor Green
    if ($r.Missing.Count -gt 0) {
        Write-Host ("缺失 {0} 个文件：{1}" -f $r.Missing.Count, ($r.Missing -join ', ')) -ForegroundColor Yellow
    }
    Write-Host "完成。这些文件不会被 git 跟踪。"
    exit 0
}

# 方式二：从官方仓库下载
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("spine-fetch-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

try {
    # ref 是 tag 还是分支不好判断，两种都试
    $urls = @(
        "https://codeload.github.com/EsotericSoftware/spine-runtimes/tar.gz/refs/tags/$Ref",
        "https://codeload.github.com/EsotericSoftware/spine-runtimes/tar.gz/refs/heads/$Ref"
    )
    $tar = Join-Path $tmp 'spine.tar.gz'
    $downloaded = $false
    foreach ($u in $urls) {
        Write-Host "尝试下载：$u"
        try {
            Invoke-WebRequest -Uri $u -OutFile $tar -UseBasicParsing -TimeoutSec 120
            $downloaded = $true
            break
        } catch {
            Write-Host ("  失败：" + $_.Exception.Message) -ForegroundColor DarkYellow
        }
    }
    if (-not $downloaded) {
        throw "下载失败。请检查网络，或改用 -FromDir 从本地已有副本复制。"
    }

    Write-Host "解压中..."
    $extract = Join-Path $tmp 'x'
    New-Item -ItemType Directory -Path $extract -Force | Out-Null
    tar -xzf $tar -C $extract
    if ($LASTEXITCODE -ne 0) { throw "解压失败（tar 退出码 $LASTEXITCODE）" }

    # 在解压结果中按文件名定位每个期望文件；同名冲突时优先 spine-csharp / spine-xna
    $all = Get-ChildItem -Path $extract -Recurse -File -Filter '*.cs'
    if (-not (Test-Path $TargetDir)) { New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null }

    $copied = 0
    $missing = @()
    foreach ($rel in $expected) {
        $name = [System.IO.Path]::GetFileName($rel)
        $isXna = $rel.StartsWith('XnaLoader/')
        $prefer = if ($isXna) { 'spine-xna' } else { 'spine-csharp' }

        $cands = $all | Where-Object { $_.Name -eq $name }
        if (-not $cands) { $missing += $rel; continue }

        $pick = ($cands | Where-Object { $_.FullName -match [regex]::Escape($prefer) } | Select-Object -First 1)
        if (-not $pick) { $pick = $cands | Select-Object -First 1 }

        $dst = Join-Path $TargetDir ($rel -replace '/', '\')
        $dstDir = Split-Path -Parent $dst
        if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }

        # 上游命名空间是 Spine，本项目使用 Spine2_1_25（避免与其它 spine 版本冲突）
        $text = Get-Content -LiteralPath $pick.FullName -Raw
        $text = $text -replace 'namespace Spine\b', 'namespace Spine2_1_25'
        $text = $text -replace 'using Spine;', 'using Spine2_1_25;'
        Set-Content -LiteralPath $dst -Value $text -NoNewline -Encoding UTF8
        $copied++
    }

    Write-Host ("已写入 {0}/{1} 个文件。" -f $copied, $expected.Count) -ForegroundColor Green
    if ($missing.Count -gt 0) {
        Write-Host ("未能定位 {0} 个文件：{1}" -f $missing.Count, ($missing -join ', ')) -ForegroundColor Yellow
        Write-Host "该版本的上游目录结构可能不同，请改用 -FromDir 从本地已有副本复制。" -ForegroundColor Yellow
    }
    Write-Host "完成。这些文件不会被 git 跟踪。"
}
finally {
    if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
}
