$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Нужен .NET Framework 4.x с компилятором C#.' }
$appRoot = $PSScriptRoot
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $appRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
$binaryPath = Join-Path $appRoot 'ThirdPerson.exe'
$dictionaryPath = Join-Path $appRoot 'Verbs.tsv'
if (-not (Test-Path -LiteralPath $dictionaryPath)) { throw 'Отсутствует Verbs.tsv.' }
$agreementPath = Join-Path $appRoot 'Agreement.tsv'
if (-not (Test-Path -LiteralPath $agreementPath)) { throw 'Отсутствует Agreement.tsv.' }
& $compiler /nologo /target:winexe /platform:x86 /optimize+ /utf8output /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/resource:$dictionaryPath,ThirdPerson.Verbs.tsv" "/resource:$agreementPath,ThirdPerson.Agreement.tsv" "/out:$binaryPath" $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки.' }
Write-Output "Собрано: $binaryPath"
