#define AppName "SKKOA Studio"
#ifndef AppVersion
#define AppVersion "0.1.2"
#endif
#define AppPublisher "Team ToyoTech"
#define AppExeName "SkkoaStudio.exe"
#ifndef SourceDir
#define SourceDir "..\..\..\editor\artifacts\SKKOA-Studio-win-x64"
#endif
#ifndef OutputDir
#define OutputDir "..\..\output"
#endif

[Setup]
AppId={{9F19F548-DB5B-4E07-9F89-7A5F8D259F5A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://skkoa.toyotech.dev/
AppSupportURL=https://skkoa.toyotech.dev/docs/
AppUpdatesURL=https://skkoa.toyotech.dev/download/studio/
DefaultDirName={autopf}\SKKOA Studio
DefaultGroupName=SKKOA Studio
DisableProgramGroupPage=no
OutputDir={#OutputDir}
OutputBaseFilename=SKKOA-Studio-Setup-x64
SetupIconFile=..\..\assets\icons\skkoa-installer.ico
WizardImageFile=..\..\assets\wizard\skkoa-wizard.bmp
WizardSmallImageFile=..\..\assets\wizard\skkoa-wizard-small.bmp
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\assets\icons\skkoa.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UsePreviousPrivileges=no
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startmenu"; Description: "Create Start Menu shortcuts"; GroupDescription: "Shortcuts:"; Flags: checkedonce
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "associatekoa"; Description: "Associate .koa files with SKKOA Studio"; GroupDescription: "File associations:"; Flags: checkedonce
Name: "associateskkoaproj"; Description: "Associate .skkoaproj files with SKKOA Studio"; GroupDescription: "File associations:"; Flags: checkedonce
Name: "addpath"; Description: "Add bundled SKKOA compiler, NASM, and GCC to PATH"; GroupDescription: "Command line:"; Check: NeedsAddPath
Name: "resetsettings"; Description: "Reset existing SKKOA Studio user settings"; GroupDescription: "Settings:"
Name: "launchafterinstall"; Description: "Launch SKKOA Studio after setup"; GroupDescription: "Finish:"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SKKOA Studio"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\assets\icons\skkoa.ico"; Tasks: startmenu
Name: "{group}\Uninstall SKKOA Studio"; Filename: "{uninstallexe}"; Tasks: startmenu
Name: "{autodesktop}\SKKOA Studio"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\assets\icons\skkoa.ico"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Classes\.koa"; ValueType: string; ValueName: ""; ValueData: "SKKOAStudio.koa"; Flags: uninsdeletevalue; Tasks: associatekoa
Root: HKA; Subkey: "Software\Classes\SKKOAStudio.koa"; ValueType: string; ValueName: ""; ValueData: "SKKOA Source File"; Flags: uninsdeletekey; Tasks: associatekoa
Root: HKA; Subkey: "Software\Classes\SKKOAStudio.koa\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\assets\icons\skkoa-file.ico"; Tasks: associatekoa
Root: HKA; Subkey: "Software\Classes\SKKOAStudio.koa\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: associatekoa
Root: HKA; Subkey: "Software\Classes\.skkoaproj"; ValueType: string; ValueName: ""; ValueData: "SKKOAStudio.project"; Flags: uninsdeletevalue; Tasks: associateskkoaproj
Root: HKA; Subkey: "Software\Classes\SKKOAStudio.project"; ValueType: string; ValueName: ""; ValueData: "SKKOA Studio Project"; Flags: uninsdeletekey; Tasks: associateskkoaproj
Root: HKA; Subkey: "Software\Classes\SKKOAStudio.project\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\assets\icons\skkoa.ico"; Tasks: associateskkoaproj
Root: HKA; Subkey: "Software\Classes\SKKOAStudio.project\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: associateskkoaproj

[InstallDelete]
Type: files; Name: "{userappdata}\SKKOA Studio\settings.json"; Tasks: resetsettings

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch SKKOA Studio"; Flags: nowait postinstall skipifsilent; Tasks: launchafterinstall; Check: InstalledAppExists

[Code]
const
  PrimaryColor = $FF59A2;
  DarkBackground = $111111;
  DarkSurface = $1F1B1B;
  DarkSurfaceAlt = $2A2424;
  DarkBorder = $3A3333;
  DarkText = $F2F2F2;
  MutedText = $B3A9A9;
  DwmUseImmersiveDarkMode = 20;
  DwmBorderColor = 34;
  DwmCaptionColor = 35;
  DwmTextColor = 36;
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{9F19F548-DB5B-4E07-9F89-7A5F8D259F5A}_is1';
  SystemEnvironmentKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';

function DwmSetWindowAttribute(hWnd: Integer; dwAttribute: Integer; var pvAttribute: Integer; cbAttribute: Integer): Integer;
external 'DwmSetWindowAttribute@dwmapi.dll stdcall delayload';

function InstalledAppExists(): Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\{#AppExeName}'));
  if not Result then
    Log('Installed application was not found: ' + ExpandConstant('{app}\{#AppExeName}'));
end;

function TryGetExistingUninstaller(var UninstallString: string): Boolean;
begin
  Result :=
    RegQueryStringValue(HKCU, UninstallKey, 'QuietUninstallString', UninstallString) or
    RegQueryStringValue(HKCU, UninstallKey, 'UninstallString', UninstallString) or
    RegQueryStringValue(HKLM, UninstallKey, 'QuietUninstallString', UninstallString) or
    RegQueryStringValue(HKLM, UninstallKey, 'UninstallString', UninstallString);
end;

function SplitCommandLine(CommandLine: string; var FileName: string; var Params: string): Boolean;
var
  Work: string;
  QuoteEnd: Integer;
  SpacePos: Integer;
begin
  Work := Trim(CommandLine);
  FileName := '';
  Params := '';

  if Work = '' then begin
    Result := False;
    exit;
  end;

  if Copy(Work, 1, 1) = '"' then begin
    Delete(Work, 1, 1);
    QuoteEnd := Pos('"', Work);
    if QuoteEnd = 0 then begin
      FileName := Work;
    end else begin
      FileName := Copy(Work, 1, QuoteEnd - 1);
      Params := Trim(Copy(Work, QuoteEnd + 1, Length(Work)));
    end;
  end else begin
    SpacePos := Pos(' ', Work);
    if SpacePos = 0 then begin
      FileName := Work;
    end else begin
      FileName := Copy(Work, 1, SpacePos - 1);
      Params := Trim(Copy(Work, SpacePos + 1, Length(Work)));
    end;
  end;

  FileName := RemoveQuotes(Trim(FileName));
  Result := FileName <> '';
end;

function RunExistingUninstaller(UninstallCommand: string): Boolean;
var
  FileName: string;
  Params: string;
  ResultCode: Integer;
begin
  Result := False;
  if not SplitCommandLine(UninstallCommand, FileName, Params) then begin
    Log('Could not parse existing uninstaller command: ' + UninstallCommand);
    exit;
  end;

  if not FileExists(FileName) then begin
    Log('Existing uninstaller executable was not found: ' + FileName);
    exit;
  end;

  if (Pos('/SILENT', Uppercase(Params)) = 0) and (Pos('/VERYSILENT', Uppercase(Params)) = 0) then
    Params := Trim(Params + ' /SILENT');
  if Pos('/NORESTART', Uppercase(Params)) = 0 then
    Params := Trim(Params + ' /NORESTART');

  Log('Running existing uninstaller: ' + FileName + ' ' + Params);
  Result := Exec(FileName, Params, '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
  if not Result then begin
    Log('Existing uninstaller could not be started.');
    exit;
  end;

  if ResultCode <> 0 then begin
    Log('Existing uninstaller returned exit code ' + IntToStr(ResultCode) + '.');
    Result := False;
  end;
end;

function InitializeSetup(): Boolean;
var
  ExistingUninstaller: string;
  Choice: Integer;
begin
  Result := True;
  if TryGetExistingUninstaller(ExistingUninstaller) then begin
    Choice := MsgBox(
      'An existing SKKOA Studio installation was found.' + #13#10 + #13#10 +
      'Yes: update or reinstall' + #13#10 +
      'No: run the uninstaller' + #13#10 +
      'Cancel: exit setup',
      mbConfirmation,
      MB_YESNOCANCEL);
    if Choice = IDNO then begin
      if not RunExistingUninstaller(ExistingUninstaller) then
        MsgBox('The existing SKKOA Studio uninstaller could not be completed. Close SKKOA Studio and try again.', mbError, MB_OK);
      Result := False;
    end else if Choice = IDCANCEL then begin
      Result := False;
    end;
  end;
end;

function NormalizePathForCompare(Value: string): string;
begin
  Result := RemoveQuotes(Trim(Value));
  while (Length(Result) > 3) and (Copy(Result, Length(Result), 1) = '\') do
    Delete(Result, Length(Result), 1);
  Result := Uppercase(Result);
end;

function PathContainsEntry(CurrentPath: string; Entry: string): Boolean;
var
  Rest: string;
  Segment: string;
  SeparatorPos: Integer;
  Target: string;
begin
  Result := False;
  Target := NormalizePathForCompare(Entry);
  Rest := CurrentPath;

  while Rest <> '' do begin
    SeparatorPos := Pos(';', Rest);
    if SeparatorPos = 0 then begin
      Segment := Rest;
      Rest := '';
    end else begin
      Segment := Copy(Rest, 1, SeparatorPos - 1);
      Delete(Rest, 1, SeparatorPos);
    end;

    if NormalizePathForCompare(Segment) = Target then begin
      Result := True;
      exit;
    end;
  end;
end;

function RemovePathEntry(CurrentPath: string; Entry: string): string;
var
  Rest: string;
  Segment: string;
  SeparatorPos: Integer;
  Target: string;
begin
  Result := '';
  Target := NormalizePathForCompare(Entry);
  Rest := CurrentPath;

  while Rest <> '' do begin
    SeparatorPos := Pos(';', Rest);
    if SeparatorPos = 0 then begin
      Segment := Rest;
      Rest := '';
    end else begin
      Segment := Copy(Rest, 1, SeparatorPos - 1);
      Delete(Rest, 1, SeparatorPos);
    end;

    if (Trim(Segment) <> '') and (NormalizePathForCompare(Segment) <> Target) then begin
      if Result = '' then
        Result := Trim(Segment)
      else
        Result := Result + ';' + Trim(Segment);
    end;
  end;
end;

function QueryEnvironmentPath(var CurrentPath: string): Boolean;
begin
  if IsAdminInstallMode then
    Result := RegQueryStringValue(HKLM, SystemEnvironmentKey, 'Path', CurrentPath)
  else
    Result := RegQueryStringValue(HKCU, 'Environment', 'Path', CurrentPath);
end;

function WriteEnvironmentPath(CurrentPath: string): Boolean;
begin
  if IsAdminInstallMode then
    Result := RegWriteStringValue(HKLM, SystemEnvironmentKey, 'Path', CurrentPath)
  else
    Result := RegWriteStringValue(HKCU, 'Environment', 'Path', CurrentPath);

  if not Result then
    Log('Failed to write environment PATH.');
end;

function NeedsAddPath(): Boolean;
var
  CurrentPath: string;
  CompilerPath: string;
  MingwPath: string;
  UsrPath: string;
begin
  CompilerPath := ExpandConstant('{app}\tools\skkoa');
  MingwPath := ExpandConstant('{app}\tools\skkoa\toolchain\msys64\mingw64\bin');
  UsrPath := ExpandConstant('{app}\tools\skkoa\toolchain\msys64\usr\bin');
  if not QueryEnvironmentPath(CurrentPath) then
    CurrentPath := '';
  Result :=
    not PathContainsEntry(CurrentPath, CompilerPath) or
    not PathContainsEntry(CurrentPath, MingwPath) or
    not PathContainsEntry(CurrentPath, UsrPath);
end;

function AddPathEntry(CurrentPath: string; Entry: string): string;
begin
  Result := CurrentPath;
  if not PathContainsEntry(Result, Entry) then begin
    if Trim(Result) = '' then
      Result := Entry
    else
      Result := Result + ';' + Entry;
  end;
end;

function AddCompilerToUserPath(): Boolean;
var
  CurrentPath: string;
  CompilerPath: string;
  MingwPath: string;
  UsrPath: string;
begin
  Result := True;
  CompilerPath := ExpandConstant('{app}\tools\skkoa');
  MingwPath := ExpandConstant('{app}\tools\skkoa\toolchain\msys64\mingw64\bin');
  UsrPath := ExpandConstant('{app}\tools\skkoa\toolchain\msys64\usr\bin');
  if not QueryEnvironmentPath(CurrentPath) then
    CurrentPath := '';
  CurrentPath := AddPathEntry(CurrentPath, CompilerPath);
  CurrentPath := AddPathEntry(CurrentPath, MingwPath);
  CurrentPath := AddPathEntry(CurrentPath, UsrPath);
  Result := WriteEnvironmentPath(CurrentPath);
end;

function RemoveCompilerFromUserPath(): Boolean;
var
  CurrentPath: string;
  CompilerPath: string;
  MingwPath: string;
  UsrPath: string;
  NewPath: string;
begin
  Result := True;
  CompilerPath := ExpandConstant('{app}\tools\skkoa');
  MingwPath := ExpandConstant('{app}\tools\skkoa\toolchain\msys64\mingw64\bin');
  UsrPath := ExpandConstant('{app}\tools\skkoa\toolchain\msys64\usr\bin');
  if QueryEnvironmentPath(CurrentPath) then begin
    NewPath := RemovePathEntry(CurrentPath, CompilerPath);
    NewPath := RemovePathEntry(NewPath, MingwPath);
    NewPath := RemovePathEntry(NewPath, UsrPath);
    if NewPath <> CurrentPath then
      Result := WriteEnvironmentPath(NewPath);
  end;
end;

procedure ApplyDarkWindowFrame();
var
  HWnd: Integer;
  Enabled: Integer;
  CaptionColor: Integer;
  BorderColor: Integer;
  TextColor: Integer;
begin
  try
    HWnd := StrToInt(ExpandConstant('{wizardhwnd}'));
    Enabled := 1;
    CaptionColor := DarkBackground;
    BorderColor := DarkBackground;
    TextColor := DarkText;
    DwmSetWindowAttribute(HWnd, DwmUseImmersiveDarkMode, Enabled, 4);
    DwmSetWindowAttribute(HWnd, DwmCaptionColor, CaptionColor, 4);
    DwmSetWindowAttribute(HWnd, DwmBorderColor, BorderColor, 4);
    DwmSetWindowAttribute(HWnd, DwmTextColor, TextColor, 4);
  except
    Log('Could not apply dark DWM frame to the installer window.');
  end;
end;

procedure ApplyWizardTheme();
begin
  WizardForm.Color := DarkBackground;
  WizardForm.Font.Color := DarkText;
  WizardForm.MainPanel.Color := DarkBackground;
  WizardForm.WelcomePage.Color := DarkBackground;
  WizardForm.InnerPage.Color := DarkBackground;
  WizardForm.SelectDirPage.Color := DarkBackground;
  WizardForm.SelectTasksPage.Color := DarkBackground;
  WizardForm.ReadyPage.Color := DarkBackground;
  WizardForm.InstallingPage.Color := DarkBackground;
  WizardForm.FinishedPage.Color := DarkBackground;

  WizardForm.PageNameLabel.Font.Color := PrimaryColor;
  WizardForm.PageDescriptionLabel.Font.Color := MutedText;
  WizardForm.NextButton.Font.Color := PrimaryColor;
  WizardForm.BackButton.Font.Color := PrimaryColor;
  WizardForm.CancelButton.Font.Color := PrimaryColor;
  WizardForm.WelcomeLabel1.Font.Color := PrimaryColor;
  WizardForm.WelcomeLabel2.Font.Color := DarkText;
  WizardForm.FinishedHeadingLabel.Font.Color := PrimaryColor;
  WizardForm.FinishedLabel.Font.Color := DarkText;
  WizardForm.SelectDirLabel.Font.Color := DarkText;
  WizardForm.SelectTasksLabel.Font.Color := DarkText;
  WizardForm.ReadyLabel.Font.Color := DarkText;
  WizardForm.DiskSpaceLabel.Font.Color := MutedText;
  WizardForm.StatusLabel.Font.Color := DarkText;
  WizardForm.FilenameLabel.Font.Color := MutedText;

  WizardForm.DirEdit.Color := DarkSurface;
  WizardForm.DirEdit.Font.Color := DarkText;
  WizardForm.GroupEdit.Color := DarkSurface;
  WizardForm.GroupEdit.Font.Color := DarkText;
  WizardForm.TasksList.Color := DarkSurface;
  WizardForm.TasksList.Font.Color := DarkText;
  WizardForm.ReadyMemo.Color := DarkSurface;
  WizardForm.ReadyMemo.Font.Color := DarkText;

  WizardForm.WizardBitmapImage.Visible := True;
  WizardForm.WizardBitmapImage2.Visible := True;
  WizardForm.WizardSmallBitmapImage.Visible := True;
  ApplyDarkWindowFrame();
end;

procedure InitializeWizard();
begin
  ApplyWizardTheme();
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  ApplyWizardTheme();
end;

procedure ValidateInstalledFiles();
begin
  if not FileExists(ExpandConstant('{app}\{#AppExeName}')) then
    MsgBox('SKKOA Studio was installed, but the main executable is missing. Rebuild the editor artifact and run setup again.', mbError, MB_OK);

  if not FileExists(ExpandConstant('{app}\SkkoaStudio.Updater.exe')) then
    MsgBox('SKKOA Studio was installed, but the updater is missing. In-app updates will not work until the installer artifact is rebuilt.', mbError, MB_OK);

  if not FileExists(ExpandConstant('{app}\tools\skkoa\skkoa.exe')) then
    MsgBox('SKKOA Studio was installed, but the bundled compiler is missing. Compile and Run will not work until the installer artifact is rebuilt.', mbError, MB_OK);

  if not FileExists(ExpandConstant('{app}\tools\skkoa\skkoa.cmd')) then
    MsgBox('SKKOA Studio was installed, but the bundled compiler launcher is missing. Command-line use may not work until the installer artifact is rebuilt.', mbError, MB_OK);

  if not FileExists(ExpandConstant('{app}\tools\skkoa\toolchain\msys64\mingw64\bin\gcc.exe')) then
    MsgBox('SKKOA Studio was installed, but bundled GCC is missing. Compile and Run will not work until the installer artifact is rebuilt.', mbError, MB_OK);

  if not FileExists(ExpandConstant('{app}\tools\skkoa\toolchain\msys64\mingw64\bin\g++.exe')) then
    MsgBox('SKKOA Studio was installed, but bundled G++ is missing. Compile and Run will not work until the installer artifact is rebuilt.', mbError, MB_OK);

  if not FileExists(ExpandConstant('{app}\tools\skkoa\toolchain\msys64\mingw64\bin\nasm.exe')) then
    MsgBox('SKKOA Studio was installed, but bundled NASM is missing. Compile and Run will not work until the installer artifact is rebuilt.', mbError, MB_OK);

  if not FileExists(ExpandConstant('{app}\tools\skkoa\toolchain\msys64\usr\bin\bash.exe')) then
    MsgBox('SKKOA Studio was installed, but bundled MSYS2 runtime tools are missing. Compile and Run may not work until the installer artifact is rebuilt.', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    ValidateInstalledFiles();
    if WizardIsTaskSelected('addpath') and not AddCompilerToUserPath() then
      MsgBox('SKKOA Studio was installed, but the compiler/toolchain PATH entries could not be updated. You can still use the IDE because it uses the bundled toolchain internally.', mbError, MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then begin
    if not RemoveCompilerFromUserPath() then
      Log('Could not remove SKKOA compiler/toolchain paths from the environment.');
  end;
end;
