; Native bootstrapper: this EXE can start before .NET Framework is installed.
; Compile with Build-Installer.ps1. Test builds are isolated at compile time.
#ifndef AppVersion
  #define AppVersion "1.2.1"
#endif
#ifndef AppRoot
  #define AppRoot SourcePath + ".."
#endif
#ifndef FrameworkPath
  #define FrameworkPath SourcePath + "dependencies\NDP48-x86-x64-AllOS-ENU.exe"
#endif
#ifndef OutputPath
  #define OutputPath AppRoot + "\dist"
#endif
#ifndef FrameworkHash
  #define FrameworkHash "0a3a390c47e639d0f7fc65b21195fee6b7f65b066f80f70c60fab191d14b7e40"
#endif

[Setup]
#ifdef InstallerTest
AppId=ThirdPerson.InstallerTest
AppName=Третье лицо — тест установщика
DefaultDirName={#TestRoot}\app
PrivilegesRequired=lowest
CreateUninstallRegKey=no
OutputBaseFilename=ThirdPerson-Setup-Test
#else
AppId={{7EC5688D-706E-4C12-8F7D-01B37C90D5B3}
AppName=Третье лицо
DefaultDirName={autopf}\ThirdPerson
PrivilegesRequired=admin
OutputBaseFilename=ThirdPerson-Setup-{#AppVersion}
#endif
AppVersion={#AppVersion}
AppPublisher=Serj235322
AppPublisherURL=https://github.com/Serj235322/third-person-ru
AppSupportURL=https://github.com/Serj235322/third-person-ru/issues
AppUpdatesURL=https://github.com/Serj235322/third-person-ru/releases
DefaultGroupName=Третье лицо
DisableProgramGroupPage=yes
DisableDirPage=no
DisableWelcomePage=no
LicenseFile={#AppRoot}\LICENSE
UninstallDisplayIcon={app}\ThirdPerson.exe
UninstallDisplayName=Третье лицо {#AppVersion}
OutputDir={#OutputPath}
ArchitecturesAllowed=x86compatible
MinVersion=10.0.14393
Compression=lzma2/normal
SolidCompression=no
LZMANumBlockThreads=1
WizardStyle=modern
SetupLogging=yes
SetupMutex=ThirdPerson.Setup
CloseApplications=yes
CloseApplicationsFilter=ThirdPerson.exe
RestartApplications=no
AllowNoIcons=no
VersionInfoVersion={#AppVersion}.0
VersionInfoDescription=Установка приложения «Третье лицо»
SetupIconFile={#AppRoot}\assets\ThirdPerson.ico

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки:"

[Files]
; Framework is extracted only when needed and is never left in the app folder.
Source: "{#FrameworkPath}"; DestName: "NDP48-x86-x64-AllOS-ENU.exe"; Flags: dontcopy nocompression
Source: "{#AppRoot}\ThirdPerson.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppRoot}\ThirdPerson.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppRoot}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppRoot}\THIRD-PARTY.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppRoot}\docs\USER_GUIDE.md"; DestDir: "{app}"; DestName: "Инструкция.txt"; Flags: ignoreversion
Source: "{#AppRoot}\installer\COMPONENTS.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
#ifdef InstallerTest
Name: "{#TestRoot}\desktop\Третье лицо"; Filename: "{app}\ThirdPerson.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{#TestRoot}\menu\Третье лицо"; Filename: "{app}\ThirdPerson.exe"; WorkingDir: "{app}"
Name: "{#TestRoot}\menu\Удалить Третье лицо"; Filename: "{uninstallexe}"
#else
Name: "{commondesktop}\Третье лицо"; Filename: "{app}\ThirdPerson.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{group}\Третье лицо"; Filename: "{app}\ThirdPerson.exe"; WorkingDir: "{app}"
Name: "{group}\Удалить Третье лицо"; Filename: "{uninstallexe}"
#endif

[Run]
Filename: "{app}\ThirdPerson.exe"; Description: "Запустить «Третье лицо»"; Flags: nowait postinstall skipifsilent runasoriginaluser; Check: CanLaunchApp

[Code]
const
  RequiredRelease = 528040;
  FrameworkName = 'NDP48-x86-x64-AllOS-ENU.exe';
  FrameworkKey = 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full';
var
  ComponentPage: TInputOptionWizardPage;
  RuntimeNeedsRestart: Boolean;
  RuntimeInstalledThisRun: Boolean;

function SystemFrameworkInstalled: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM32, FrameworkKey, 'Release', Release) and
    (Release >= RequiredRelease);
  if (not Result) and IsWin64 then
    Result := RegQueryDWordValue(HKLM64, FrameworkKey, 'Release', Release) and
      (Release >= RequiredRelease);
end;

function FrameworkInstalled: Boolean;
#ifdef InstallerTest
var
  Release: Cardinal;
#endif
begin
#ifdef InstallerTest
  if ExpandConstant('{param:TestRealFramework|0}') = '1' then begin
    Result := SystemFrameworkInstalled;
    Exit;
  end;
  Release := StrToIntDef(ExpandConstant('{param:TestRelease|0}'), 0);
  Result := (Release >= RequiredRelease) or
    FileExists(ExpandConstant('{#TestRoot}\framework-installed.txt'));
#else
  Result := SystemFrameworkInstalled;
#endif
end;

function CanLaunchApp: Boolean;
begin
  Result := (not RuntimeNeedsRestart) and FrameworkInstalled;
end;

function InteractivePrerequisiteAllowed: Boolean;
begin
  Result := not WizardSilent;
#ifdef InstallerTest
  if ExpandConstant('{param:TestAllowSilent|1}') = '1' then Result := True;
#endif
end;

procedure InitializeWizard;
begin
  RuntimeNeedsRestart := False;
  RuntimeInstalledThisRun := False;
  if FrameworkInstalled then begin
    ComponentPage := CreateInputOptionPage(wpLicense, 'Необходимые компоненты',
      'Проверка .NET Framework',
      '.NET Framework 4.8 или новее уже установлен. Можно продолжить установку программы.', False, False);
    ComponentPage.Add('.NET Framework — готов к работе');
    ComponentPage.Values[0] := True;
    ComponentPage.CheckListBox.Enabled := False;
    Log('Framework present: prerequisite will be skipped.');
  end else begin
    ComponentPage := CreateInputOptionPage(wpLicense, 'Необходимые компоненты',
      'Проверка .NET Framework',
      'Для работы программы нужен Microsoft .NET Framework 4.8. Его официальный ' +
      'офлайн-пакет включён в установщик.' + #13#10#13#10 +
      'Перед установкой программы откроется мастер Microsoft: прочитайте и примите ' +
      'его лицензионные условия. Интернет для загрузки компонентов не нужен. ' +
      'Установка может занять несколько минут и потребовать перезагрузку.', False, False);
    ComponentPage.Add('Установить Microsoft .NET Framework 4.8');
    ComponentPage.Values[0] := True;
    Log('Framework missing: user consent is required.');
  end;
#ifdef InstallerTest
  if ExpandConstant('{param:TestDecline|0}') = '1' then
    ComponentPage.Values[0] := False;
#endif
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = ComponentPage.ID) and (not FrameworkInstalled) and
      (not ComponentPage.Values[0]) then begin
    Log('Framework installation declined.');
    if not WizardSilent then
      MsgBox('Без .NET Framework 4.8 программа не запустится. Чтобы продолжить, ' +
        'разрешите его установку; чтобы выйти, нажмите «Отмена».', mbInformation, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  RuntimePath, Parameters: String;
  ExitCode: Integer;
begin
  Result := '';
  NeedsRestart := False;
  if RuntimeInstalledThisRun or FrameworkInstalled then begin
    Log('Framework prerequisite satisfied.');
    Exit;
  end;
  if not ComponentPage.Values[0] then begin
    Result := 'Установка .NET Framework не разрешена.';
    Exit;
  end;
  // Silent deployment must not imply acceptance of Microsoft's license.
  if not InteractivePrerequisiteAllowed then begin
    Result := 'Не установлен .NET Framework 4.8. Запустите установщик в обычном ' +
      'режиме и примите условия Microsoft либо предварительно установите Framework.';
    Exit;
  end;
  try
    ExtractTemporaryFile(FrameworkName);
    RuntimePath := ExpandConstant('{tmp}\') + FrameworkName;
    if CompareText(GetSHA256OfFile(RuntimePath), '{#FrameworkHash}') <> 0 then begin
      Result := 'Не удалось проверить встроенный пакет .NET Framework. ' +
        'Скачайте установщик программы заново.';
      Exit;
    end;
    Parameters := '/norestart /ChainingPackage ThirdPerson';
#ifdef InstallerTest
    Parameters := Parameters + ' /TestRoot="{#TestRoot}" /TestExitCode=' +
      ExpandConstant('{param:TestExitCode|0}') + ' /TestNoRegistration=' +
      ExpandConstant('{param:TestNoRegistration|0}');
#endif
    WizardForm.StatusLabel.Caption := 'Установка Microsoft .NET Framework 4.8…';
    Log('Starting embedded Framework installer.');
    if not Exec(RuntimePath, Parameters, ExpandConstant('{tmp}'), SW_SHOWNORMAL,
        ewWaitUntilTerminated, ExitCode) then begin
      Result := 'Не удалось запустить установку .NET Framework. Код Windows: ' +
        IntToStr(ExitCode) + '. Повторите попытку.';
      Exit;
    end;
    Log('Framework installer exit code: ' + IntToStr(ExitCode));
    case ExitCode of
      0: begin
        if not FrameworkInstalled then begin
          Result := 'Мастер Microsoft завершился, но .NET Framework 4.8 не обнаружен. ' +
            'Проверьте установку компонента и повторите попытку.';
          Exit;
        end;
        RuntimeInstalledThisRun := True;
      end;
      1641, 3010: begin
        RuntimeInstalledThisRun := True;
        RuntimeNeedsRestart := True;
        Log('Restart required; app launch is suppressed until restart.');
      end;
      1602: Result := 'Установка .NET Framework отменена. Программа ещё не установлена. ' +
        'Нажмите «Установить», чтобы повторить попытку, или «Отмена», чтобы выйти.';
    else
      Result := 'Не удалось установить .NET Framework. Код Microsoft: ' + IntToStr(ExitCode) +
        '. Программа ещё не установлена. Проверьте свободное место и сообщения ' +
        'мастера Microsoft, затем повторите попытку.';
    end;
  except
    Result := 'Не удалось подготовить .NET Framework: ' + GetExceptionMessage;
  end;
end;

function NeedRestart: Boolean;
begin
  Result := RuntimeNeedsRestart;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    Log('Application installed; CanLaunchApp=' + IntToStr(Ord(CanLaunchApp)));
    if RuntimeNeedsRestart then
      WizardForm.FinishedLabel.Caption := 'Программа установлена. Для завершения ' +
        'установки .NET Framework перезагрузите компьютер, затем запустите ' +
        '«Третье лицо» с помощью ярлыка.';
  end;
end;
