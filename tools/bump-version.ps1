param(
    [Parameter(Mandatory = $true)][string]$CsprojPath
)

$ErrorActionPreference = "Stop"

function Bump-Revision([string]$version) {
    $parts = $version.Split('.')
    if ($parts.Length -eq 4) {
        $rev = [int]$parts[3] + 1
        $parts[3] = $rev.ToString()
        return ($parts -join '.')
    }
    return $version
}

if (-not (Test-Path -LiteralPath $CsprojPath)) {
    Write-Error "csproj not found: $CsprojPath"
    exit 1
}

$content = Get-Content -LiteralPath $CsprojPath -Raw -Encoding UTF8

$asmMatch = [regex]::Match($content, '<AssemblyVersion>([^<]+)</AssemblyVersion>')
$fileMatch = [regex]::Match($content, '<FileVersion>([^<]+)</FileVersion>')

$newAsm = $null
$newFile = $null

if ($asmMatch.Success) {
    $newAsm = Bump-Revision $asmMatch.Groups[1].Value
    $content = $content.Replace("<AssemblyVersion>$($asmMatch.Groups[1].Value)</AssemblyVersion>", "<AssemblyVersion>$newAsm</AssemblyVersion>")
}
if ($fileMatch.Success) {
    $newFile = Bump-Revision $fileMatch.Groups[1].Value
    $content = $content.Replace("<FileVersion>$($fileMatch.Groups[1].Value)</FileVersion>", "<FileVersion>$newFile</FileVersion>")
}

Set-Content -LiteralPath $CsprojPath -Value $content -Encoding UTF8 -NoNewline

if ($newAsm) { Write-Output "AssemblyVersion -> $newAsm" }
if ($newFile) { Write-Output "FileVersion -> $newFile" }
