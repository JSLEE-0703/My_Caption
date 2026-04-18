param(
    [Parameter(Mandatory = $true)]
    [string]$MdxPath,

    [string]$MddPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputJsonPath,

    [Parameter(Mandatory = $true)]
    [string]$ExtractorPath,

    [string]$ExtractorArguments = '"{mdx}" "{out}"',

    [string]$SourceDictionary,

    [switch]$KeepIntermediate
)

$ErrorActionPreference = 'Stop'

function Resolve-ExistingPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        throw "$Label is empty."
    }

    if (-not (Test-Path -LiteralPath $PathValue)) {
        throw "$Label not found: $PathValue"
    }

    return (Resolve-Path -LiteralPath $PathValue).Path
}

function Split-ArgumentString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArgumentString
    )

    $arguments = New-Object System.Collections.Generic.List[string]
    $builder = New-Object System.Text.StringBuilder
    $inQuotes = $false

    for ($i = 0; $i -lt $ArgumentString.Length; $i++) {
        $character = $ArgumentString[$i]

        if ($character -eq '"') {
            $inQuotes = -not $inQuotes
            continue
        }

        if ([char]::IsWhiteSpace($character) -and -not $inQuotes) {
            if ($builder.Length -gt 0) {
                $arguments.Add($builder.ToString())
                [void]$builder.Clear()
            }

            continue
        }

        [void]$builder.Append($character)
    }

    if ($builder.Length -gt 0) {
        $arguments.Add($builder.ToString())
    }

    return ,$arguments.ToArray()
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$mdxPath = Resolve-ExistingPath -PathValue $MdxPath -Label 'MDX file'
$extractorPath = Resolve-ExistingPath -PathValue $ExtractorPath -Label 'Extractor'
$mddResolved = $null
if (-not [string]::IsNullOrWhiteSpace($MddPath)) {
    $mddResolved = Resolve-ExistingPath -PathValue $MddPath -Label 'MDD file'
}

$outputFullPath = [System.IO.Path]::GetFullPath($OutputJsonPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($outputFullPath)
if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
    throw "Output directory is invalid: $OutputJsonPath"
}

if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$tempRoot = Join-Path $outputDirectory '_mdx_import'
if (-not (Test-Path -LiteralPath $tempRoot)) {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
}

$intermediatePath = Join-Path $tempRoot ([System.IO.Path]::GetFileNameWithoutExtension($outputFullPath) + '.jsonl')
$normalizerProject = Join-Path $workspaceRoot 'tools\MdxImportNormalizer\MdxImportNormalizer.csproj'
$normalizerExe = Join-Path $workspaceRoot 'tools\MdxImportNormalizer\bin\Debug\MdxImportNormalizer.exe'
$msbuildPath = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'

if (-not (Test-Path -LiteralPath $normalizerProject)) {
    throw "Normalizer project not found: $normalizerProject"
}

if (-not (Test-Path -LiteralPath $msbuildPath)) {
    throw "MSBuild not found: $msbuildPath"
}

Write-Host "Building MDX normalizer..."
& $msbuildPath $normalizerProject /t:Build /p:Configuration=Debug /p:Platform=AnyCPU | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build normalizer."
}

if (-not (Test-Path -LiteralPath $normalizerExe)) {
    throw "Normalizer executable not found after build: $normalizerExe"
}

$template = $ExtractorArguments
$template = $template.Replace('{mdx}', $mdxPath)
$template = $template.Replace('{out}', $intermediatePath)
$mddTokenValue = ''
if ($null -ne $mddResolved) {
    $mddTokenValue = $mddResolved
}
$template = $template.Replace('{mdd}', $mddTokenValue)
$extractorArgumentList = Split-ArgumentString -ArgumentString $template

Write-Host "Running external extractor..."
& $extractorPath @extractorArgumentList
if ($LASTEXITCODE -ne 0) {
    throw "Extractor failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $intermediatePath)) {
    throw "Extractor did not produce the expected intermediate file: $intermediatePath"
}

if ([string]::IsNullOrWhiteSpace($SourceDictionary)) {
    $SourceDictionary = [System.IO.Path]::GetFileNameWithoutExtension($mdxPath)
}

Write-Host "Normalizing extracted entries..."
& $normalizerExe `
    --input $intermediatePath `
    --output $outputFullPath `
    --source-dictionary $SourceDictionary `
    --source-format mdx

if ($LASTEXITCODE -ne 0) {
    throw "Normalizer failed with exit code $LASTEXITCODE."
}

if (-not $KeepIntermediate) {
    Remove-Item -LiteralPath $intermediatePath -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $tempRoot) {
        $remaining = Get-ChildItem -LiteralPath $tempRoot -Force -ErrorAction SilentlyContinue
        if ($remaining.Count -eq 0) {
            Remove-Item -LiteralPath $tempRoot -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host "Dictionary JSON generated at: $outputFullPath"
