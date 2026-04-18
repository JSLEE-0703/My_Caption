param(
    [Parameter(Mandatory = $true)]
    [string]$MdxPath,

    [Parameter(Mandatory = $true)]
    [string]$OutPath,

    [Parameter(Mandatory = $true)]
    [string]$MdictExePath
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

$mdxResolved = Resolve-ExistingPath -PathValue $MdxPath -Label 'MDX file'
$mdictExeResolved = Resolve-ExistingPath -PathValue $MdictExePath -Label 'mdict executable'
$outFullPath = [System.IO.Path]::GetFullPath($OutPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($outFullPath)

if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
    throw "Output directory is invalid: $OutPath"
}

if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$tempDirectory = Join-Path $outputDirectory ([System.IO.Path]::GetFileNameWithoutExtension($outFullPath) + '_mdict_extract')
Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $tempDirectory | Out-Null

try {
    $env:PYTHONIOENCODING = 'utf-8'
    & $mdictExeResolved -x $mdxResolved -d $tempDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "mdict-utils extract failed with exit code $LASTEXITCODE."
    }

    $txtFiles = Get-ChildItem -LiteralPath $tempDirectory -Filter '*.txt' | Sort-Object Name
    if ($txtFiles.Count -eq 0) {
        throw "mdict-utils did not produce any .txt files."
    }

    $firstTxt = $txtFiles[0].FullName
    Copy-Item -LiteralPath $firstTxt -Destination $outFullPath -Force
}
finally {
    Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
