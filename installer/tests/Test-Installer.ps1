param(
    [Parameter(Mandatory=$true)][string]$CompilerPath,
    [Parameter(Mandatory=$true)][string]$TestRoot
)
$ErrorActionPreference = 'Stop'
$appRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$testBase = [IO.Path]::GetFullPath($TestRoot)
# Every run receives its own subdirectory. No recursive delete of user paths.
$runRoot = Join-Path $testBase ('run-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$report = Join-Path $runRoot 'test-report.txt'
$script:assertions = 0
function Assert-Check([bool]$Condition, [string]$Description) {
    if (-not $Condition) { throw "FAILED: $Description" }
    $script:assertions++
    Add-Content -LiteralPath $report -Value "PASS: $Description" -Encoding UTF8
}
function Invoke-TestProcess([string]$File, [string[]]$Arguments) {
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw "Timeout: $File" }
    return $process.ExitCode
}
function Uninstall-TestApp([string]$AppDirectory, [string]$Name) {
    $uninstaller = Join-Path $AppDirectory 'unins000.exe'
    $uninstallLog = Join-Path $runRoot ($Name + '-uninstall.log')
    $exitCode = Invoke-TestProcess $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="{0}"' -f $uninstallLog))
    Assert-Check ($exitCode -eq 0) "$Name uninstaller returns success"
    # Inno's temporary uninstaller removes its original files as it exits.
    for ($retry = 0; $retry -lt 20 -and (Test-Path -LiteralPath (Join-Path $AppDirectory 'ThirdPerson.exe')); $retry++) {
        Start-Sleep -Milliseconds 100
    }
    Assert-Check (-not (Test-Path -LiteralPath (Join-Path $AppDirectory 'ThirdPerson.exe'))) "$Name app removed"
    Assert-Check (-not (Test-Path -LiteralPath $desktopLink)) "$Name desktop shortcut removed"
    Assert-Check (-not (Test-Path -LiteralPath $menuLink)) "$Name menu shortcut removed"
}
$csharp = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$stubPath = Join-Path $runRoot 'FrameworkStub.exe'
& $csharp /nologo /target:exe /platform:x86 "/out:$stubPath" (Join-Path $PSScriptRoot 'FrameworkStub.cs')
if ($LASTEXITCODE -ne 0) { throw 'Could not compile test process.' }
$stubHash = (Get-FileHash -LiteralPath $stubPath -Algorithm SHA256).Hash.ToLowerInvariant()
$issPath = Join-Path $appRoot 'installer\ThirdPerson.iss'
& $CompilerPath /Q '/DInstallerTest' "/DTestRoot=$runRoot" "/DAppRoot=$appRoot" "/DFrameworkPath=$stubPath" "/DFrameworkHash=$stubHash" "/DOutputPath=$runRoot" $issPath
if ($LASTEXITCODE -ne 0) { throw 'Could not compile isolated test installer.' }
$setupPath = Join-Path $runRoot 'ThirdPerson-Setup-Test.exe'
$desktopLink = Join-Path $runRoot 'desktop\Третье лицо.lnk'
$menuLink = Join-Path $runRoot 'menu\Третье лицо.lnk'
$marker = Join-Path $runRoot 'framework-installed.txt'
$calls = Join-Path $runRoot 'framework-calls.txt'
$originalHash = (Get-FileHash -LiteralPath (Join-Path $appRoot 'ThirdPerson.exe')).Hash
$shortcutReader = New-Object -ComObject WScript.Shell
$cases = @(
    @{ Name='host-framework-registry'; Release=0; Code=0; Success=$true; Called=$false; Restart=$false; Extra='/TestRealFramework=1' },
    @{ Name='existing-48'; Release=528040; Code=0; Success=$true; Called=$false; Restart=$false },
    @{ Name='existing-481'; Release=533320; Code=0; Success=$true; Called=$false; Restart=$false },
    @{ Name='no-desktop-shortcut'; Release=528040; Code=0; Success=$true; Called=$false; Restart=$false; NoDesktop=$true },
    @{ Name='older-framework'; Release=528039; Code=0; Success=$true; Called=$true; Restart=$false },
    @{ Name='missing-success'; Release=0; Code=0; Success=$true; Called=$true; Restart=$false },
    @{ Name='cancelled'; Release=0; Code=1602; Success=$false; Called=$true; Restart=$false },
    @{ Name='failed'; Release=0; Code=1603; Success=$false; Called=$true; Restart=$false },
    @{ Name='unsupported-runtime'; Release=0; Code=5100; Success=$false; Called=$true; Restart=$false },
    @{ Name='reboot-3010'; Release=0; Code=3010; Success=$true; Called=$true; Restart=$true },
    @{ Name='reboot-1641'; Release=0; Code=1641; Success=$true; Called=$true; Restart=$true },
    @{ Name='success-without-framework'; Release=0; Code=0; Success=$false; Called=$true; Restart=$false; Extra='/TestNoRegistration=1' },
    @{ Name='declined'; Release=0; Code=0; Success=$false; Called=$false; Restart=$false; Extra='/TestDecline=1' },
    @{ Name='silent-missing-blocked'; Release=0; Code=0; Success=$false; Called=$false; Restart=$false; Extra='/TestAllowSilent=0' }
)
foreach ($case in $cases) {
    foreach ($temporaryPath in @($marker, $calls)) {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath }
    }
    $name = $case.Name
    $appDirectory = Join-Path $runRoot $name
    $setupLog = Join-Path $runRoot ($name + '.log')
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/RESTARTEXITCODE=3010', '/TASKS=desktopicon',
        ('/DIR="{0}"' -f $appDirectory), ('/LOG="{0}"' -f $setupLog),
        ('/TestRelease={0}' -f $case.Release), ('/TestExitCode={0}' -f $case.Code))
    if ($case.Extra) { $arguments += $case.Extra }
    if ($case.NoDesktop) {
        $arguments = @($arguments | Where-Object { $_ -notlike '/TASKS=*' }) + '/TASKS=""'
    }
    if ($name -eq 'existing-48') {
        $arguments = @($arguments | Where-Object { $_ -notlike '/TASKS=*' })
    }
    $exitCode = Invoke-TestProcess $setupPath $arguments
    $log = Get-Content -LiteralPath $setupLog -Raw
    Assert-Check ((Test-Path -LiteralPath $calls) -eq $case.Called) "$name prerequisite execution"
    if ($case.Success) {
        $expectedExitCode = 0
        if ($case.Restart) { $expectedExitCode = 3010 }
        Assert-Check ($exitCode -eq $expectedExitCode) "$name setup exit code $expectedExitCode"
        $installedApp = Join-Path $appDirectory 'ThirdPerson.exe'
        Assert-Check ((Get-FileHash -LiteralPath $installedApp).Hash -eq $originalHash) "$name exact application payload"
        foreach ($file in @('LICENSE', 'THIRD-PARTY.txt', 'COMPONENTS.txt', 'Инструкция.txt', 'unins000.exe', 'ThirdPerson.exe.config')) {
            Assert-Check (Test-Path -LiteralPath (Join-Path $appDirectory $file)) "$name installed $file"
        }
        foreach ($link in @($desktopLink, $menuLink)) {
            if ($case.NoDesktop -and $link -eq $desktopLink) {
                Assert-Check (-not (Test-Path -LiteralPath $link)) "$name optional desktop shortcut omitted"
                continue
            }
            Assert-Check (Test-Path -LiteralPath $link) "$name shortcut exists"
            $shortcut = $shortcutReader.CreateShortcut($link)
            Assert-Check ($shortcut.TargetPath -eq $installedApp) "$name shortcut points to app"
            Assert-Check ($shortcut.WorkingDirectory -eq $appDirectory) "$name shortcut working directory"
        }
        Assert-Check (-not (Test-Path -LiteralPath (Join-Path $appDirectory 'NDP48-x86-x64-AllOS-ENU.exe'))) "$name framework package not installed in app folder"
        $expectedLaunch = 1
        if ($case.Restart) { $expectedLaunch = 0 }
        Assert-Check ($log.Contains("CanLaunchApp=$expectedLaunch")) "$name app launch gating"
        if ($name -eq 'missing-success') {
            $userFile = Join-Path $appDirectory 'user-document.txt'
            'User content must survive update and uninstall.' | Set-Content -LiteralPath $userFile
            $rerunArguments = $arguments | Where-Object { $_ -notlike '/LOG=*' }
            $rerunArguments += ('/LOG="{0}"' -f (Join-Path $runRoot 'reinstall.log'))
            $reinstallCode = Invoke-TestProcess $setupPath $rerunArguments
            Assert-Check ($reinstallCode -eq 0) 'reinstall success'
            Assert-Check ((Get-Content -LiteralPath $calls).Count -eq 1) 'reinstall does not repeat Framework'
            Assert-Check (Test-Path -LiteralPath $userFile) 'reinstall preserves user file'
            $engineReport = Join-Path $runRoot 'installed-engine-test.txt'
            $engineCode = Invoke-TestProcess $installedApp @('--self-test', ('"{0}"' -f $engineReport))
            Assert-Check ($engineCode -eq 0) 'installed application engine tests'
            $dpiReport = Join-Path $runRoot 'installed-dpi-test.txt'
            $dpiCode = Invoke-TestProcess $installedApp @('--dpi-test', ('"{0}"' -f $dpiReport))
            Assert-Check ($dpiCode -eq 0) 'installed application DPI configuration and layout'
        }
        Uninstall-TestApp $appDirectory $name
        if ($name -eq 'missing-success') {
            Assert-Check (Test-Path -LiteralPath $userFile) 'uninstall preserves untracked user file'
        }
    } else {
        Assert-Check ($exitCode -ne 0) "$name setup rejects failure"
        Assert-Check (-not (Test-Path -LiteralPath (Join-Path $appDirectory 'ThirdPerson.exe'))) "$name app not installed"
        Assert-Check (-not (Test-Path -LiteralPath $desktopLink)) "$name no desktop shortcut"
        Assert-Check (-not (Test-Path -LiteralPath $menuLink)) "$name no menu shortcut"
    }
    Write-Output "PASS: $name"
}
# A separate compile deliberately pins the wrong hash: no stub may execute.
if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
if (Test-Path -LiteralPath $calls) { Remove-Item -LiteralPath $calls }
& $CompilerPath /Q '/DInstallerTest' "/DTestRoot=$runRoot" "/DAppRoot=$appRoot" "/DFrameworkPath=$stubPath" ('/DFrameworkHash=' + ('0' * 64)) "/DOutputPath=$runRoot" $issPath
if ($LASTEXITCODE -ne 0) { throw 'Could not compile hash-rejection test.' }
$hashDirectory = Join-Path $runRoot 'wrong-hash'
$hashCode = Invoke-TestProcess $setupPath @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/DIR="{0}"' -f $hashDirectory), '/TestRelease=0')
Assert-Check ($hashCode -ne 0) 'hash mismatch blocks installation'
Assert-Check (-not (Test-Path -LiteralPath $calls)) 'hash mismatch blocks process execution'
Assert-Check (-not (Test-Path -LiteralPath (Join-Path $hashDirectory 'ThirdPerson.exe'))) 'hash mismatch blocks app copy'
Add-Content -LiteralPath $report -Value "PASSED: $script:assertions assertions; 13 prerequisite scenarios plus host registry and optional shortcut; isolated files and shortcuts."
Write-Output "PASSED: $script:assertions assertions. Report: $report"
