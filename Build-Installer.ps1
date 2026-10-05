param(
    [string]$CompilerPath,
    [string]$FrameworkPath,
    [string]$OutputDirectory,
    [switch]$NoDownload,
    [switch]$SkipAppBuild
)
$ErrorActionPreference = 'Stop'
$appRoot = $PSScriptRoot
$frameworkHash = '0a3a390c47e639d0f7fc65b21195fee6b7f65b066f80f70c60fab191d14b7e40'
$frameworkUrl = 'https://download.microsoft.com/download/f/3/a/f3a6af84-da23-40a5-8d1c-49cc10c8e76f/NDP48-x86-x64-AllOS-ENU.exe'
if (-not $CompilerPath) {
    $candidateRoots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles)
    foreach ($candidateRoot in $candidateRoots) {
        foreach ($version in @('6', '7')) {
            if ($candidateRoot) {
                $candidate = Join-Path $candidateRoot "Inno Setup $version\ISCC.exe"
                if (Test-Path -LiteralPath $candidate) { $CompilerPath = $candidate; break }
            }
        }
        if ($CompilerPath) { break }
    }
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath)) {
    throw 'Установите Inno Setup 6.7.3 или укажите -CompilerPath с полным путём к ISCC.exe.'
}
if (-not $FrameworkPath) {
    $dependencyDirectory = Join-Path $appRoot 'installer\dependencies'
    $FrameworkPath = Join-Path $dependencyDirectory 'NDP48-x86-x64-AllOS-ENU.exe'
}
$FrameworkPath = [IO.Path]::GetFullPath($FrameworkPath)
if (-not (Test-Path -LiteralPath $FrameworkPath)) {
    if ($NoDownload) { throw 'Нет офлайн-пакета .NET Framework; укажите -FrameworkPath.' }
    New-Item -ItemType Directory -Path (Split-Path -Parent $FrameworkPath) -Force | Out-Null
    $partial = $FrameworkPath + '.download'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Write-Output 'Загрузка официального офлайн-пакета .NET Framework 4.8…'
    Invoke-WebRequest -Uri $frameworkUrl -OutFile $partial -UseBasicParsing
    if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash.ToLowerInvariant() -ne $frameworkHash) {
        throw "Не совпала контрольная сумма загрузки: $partial. Сборка остановлена."
    }
    Move-Item -LiteralPath $partial -Destination $FrameworkPath -Force
}
if ((Get-FileHash -LiteralPath $FrameworkPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $frameworkHash) {
    throw 'Пакет .NET Framework отличается от проверенного официального файла. Сборка остановлена.'
}
$signature = Get-AuthenticodeSignature -LiteralPath $FrameworkPath
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation(,|$)') {
    throw 'Не удалось подтвердить цифровую подпись Microsoft у пакета .NET Framework.'
}
if (-not $SkipAppBuild) { & (Join-Path $appRoot 'Build.ps1') }
$appPath = Join-Path $appRoot 'ThirdPerson.exe'
if (-not (Test-Path -LiteralPath $appPath)) { throw 'Нет собранного ThirdPerson.exe.' }
$appVersion = (Get-Item -LiteralPath $appPath).VersionInfo.FileVersion
if ($appVersion -notmatch '^(\d+\.\d+\.\d+)\.\d+$') { throw 'Неизвестный формат версии приложения.' }
$appVersion = $Matches[1]
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $appRoot 'dist' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $CompilerPath "/DAppRoot=$appRoot" "/DAppVersion=$appVersion" "/DFrameworkPath=$FrameworkPath" "/DOutputPath=$OutputDirectory" (Join-Path $appRoot 'installer\ThirdPerson.iss')
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки установщика.' }
$setupPath = Join-Path $OutputDirectory "ThirdPerson-Setup-$appVersion.exe"
$setupHash = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$setupHash  $([IO.Path]::GetFileName($setupPath))" | Set-Content -LiteralPath (Join-Path $OutputDirectory "ThirdPerson-Setup-$appVersion.sha256") -Encoding ASCII
Write-Output "Готово: $setupPath"
