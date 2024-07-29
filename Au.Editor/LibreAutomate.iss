#define MyAppName "LibreAutomate C#"
#define MyAppNameShort "LibreAutomate"
#define MyAppVersion "1.4.0"
#define MyAppPublisher "Gintaras Didžgalvis"
#define MyAppURL "https://www.libreautomate.com/"
#define MyAppExeName "Au.Editor.exe"

[Setup]
AppId={#MyAppName}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
UninstallDisplayName={#MyAppName}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={commonpf}\{#MyAppNameShort}
SourceDir=..\_
OutputDir=.
OutputBaseFilename=LibreAutomateSetup
Compression=lzma/normal
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
MinVersion=0,6.1sp1
DisableProgramGroupPage=yes
AppMutex=Au.Editor.Mutex.m3gVxcTJN02pDrHiQ00aSQ
UsePreviousGroup=False
DisableDirPage=no
SetupIconFile=..\Au.Editor\resources\ico\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "Au.Editor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Editor.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Editor.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Task.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Controls.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Controls.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Net4.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Net4.exe.config"; DestDir: "{app}"; Flags: ignoreversion

Source: "Roslyn\*.dll"; DestDir: "{app}\Roslyn"; Flags: ignoreversion
Source: "Roslyn\*.exe"; DestDir: "{app}\Roslyn"; Flags: ignoreversion
Source: "64\Au.AppHost.exe"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "64\AuCpp.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "64\sqlite3.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "64\Scintilla.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "32\Au.AppHost.exe"; DestDir: "{app}\32"; Flags: ignoreversion
Source: "32\AuCpp.dll"; DestDir: "{app}\32"; Flags: ignoreversion
Source: "32\sqlite3.dll"; DestDir: "{app}\32"; Flags: ignoreversion
Source: "32\7za.exe"; DestDir: "{app}\32"; Flags: ignoreversion

Source: "Default\*"; DestDir: "{app}\Default"; Flags: ignoreversion
Source: "Default\Workspace\files\*"; DestDir: "{app}\Default\Workspace\files"; Flags: ignoreversion recursesubdirs
Source: "Default\Workspace\files.xml"; DestDir: "{app}\Default\Workspace"; Flags: ignoreversion
Source: "Default\Themes\*"; DestDir: "{app}\Default\Themes"; Flags: ignoreversion
Source: "Templates\files\*"; DestDir: "{app}\Templates\files"; Flags: ignoreversion recursesubdirs
Source: "Templates\files.xml"; DestDir: "{app}\Templates"; Flags: ignoreversion

Source: "default.exe.manifest"; DestDir: "{app}"; Flags: ignoreversion
Source: "doc.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "ref.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "winapi.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "icons.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "cookbook.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "xrefmap.yml"; DestDir: "{app}"; Flags: ignoreversion

;CONSIDER: don't include big not frequently updated files. Auto-download on demand.
;All .db except cookbook, Roslyn, both sqlite3.dll, 7za.exe. Makes smaller: 25 MB -> 3.7 MB (5 MB just without .db and Roslyn).

[Dirs]
Name: "{app}\Roslyn\.exeProgram"; Attribs: hidden
;why Inno stops here when debugging?

[InstallDelete]
;Type: files; Name: "{app}\file.ext"
;Type: filesandordirs; Name: "{app}\dir"
Type: filesandordirs; Name: "{app}\Roslyn"
Type: filesandordirs; Name: "{app}\64"
Type: filesandordirs; Name: "{app}\32"
Type: filesandordirs; Name: "{app}\Default"
Type: filesandordirs; Name: "{app}\Templates"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Registry]
;register app path
Root: HKLM; Subkey: Software\Microsoft\Windows\CurrentVersion\App Paths\Au.Editor.exe; ValueType: string; ValueData: {app}\Au.Editor.exe; Flags: uninsdeletevalue uninsdeletekeyifempty

;rejected: set environment variable Au.Path, to find unmanaged dlls used by Au.dll when it is used in various scripting environments etc that copy it somewhere (they don't copy unmanaged dlls)
; Rarely used. Overwrites mine. Let users learn it, then they can use the dlls on any computer without this program installed.
;Root: HKLM; Subkey: SYSTEM\CurrentControlSet\Control\Session Manager\Environment; ValueType: string; ValueData: {app}; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Flags: nowait postinstall skipifsilent; Description: "{cm:LaunchProgram,{#MyAppName}}"

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/delete /tn \Au\Au.Editor /f"; Flags: nowait skipifdoesntexist runhidden; RunOnceId: "schtasks-delete"

;rejected because of AV false positives.
;[Files]
;Source: "Setup32.dll"; DestDir: "{app}"; Flags: ignoreversion

[Code]

//procedure Cpp_Install(step: Integer; dir: String);
//external 'Cpp_Install@files:Setup32.dll cdecl setuponly delayload';

//procedure Cpp_Uninstall(step: Integer);
//external 'Cpp_Uninstall@{app}\Setup32.dll cdecl uninstallonly delayload';

function FindWindowEx(w1, w2: Integer; cn, name: String): Integer;
external 'FindWindowExW@user32.dll';

function SendMessageTimeout(w, msg, wp, lp, flags, timeout: Integer; var res: Integer): Integer;
external 'SendMessageTimeoutW@user32.dll';

const nmax = 25000;

//The same as Cpp_Unload. Maybe possible to call it, but difficult and may trigger AV FP.
procedure UnloadAuCppDll(unins: Boolean);
var
  a :Array of Integer;
  i, n, w, res :Integer;
  silent: Boolean;
begin
  //if not FileExists(ExpandConstant('{app}\64\AuCpp.dll')) then exit; //error, app still not set
  
  if unins then silent:=UninstallSilent() else silent:=WizardSilent();
  
  SetArrayLength(a, nmax);
  
  //close agent windows
  for i:=0 to nmax-1 do begin
    w:=FindWindowEx(-3, w, 'AuCpp_IPA_1', ''); //HWND_MESSAGE //if '', Pascal passes null, not ""
    if w=0 then break;
    a[i]:=w;
    inc(n);
  end;
  if n>0 then begin
    for i:=0 to n-1 do SendMessageTimeout(a[i], 16, 0, 0, 2, 5000, res); //WM_CLOSE, SMTO_ABORTIFHUNG
    if silent then Sleep(n * 50);
  end;
  //Log(IntToStr(i));
  
  //unload from processes where loaded by the clipboard hook
  if silent then SendMessageTimeout($ffff, 0, 0, 0, 2, 500, res) else SendBroadcastNotifyMessage(0, 0, 0); //HWND_BROADCAST, SMTO_ABORTIFHUNG
  n:=0; w:=0;
  for i:=0 to nmax-1 do begin
    w:=FindWindowEx(-3, w, '', ''); //HWND_MESSAGE
    if w=0 then break;
    a[i]:=w;
    inc(n);
  end;
  if silent then for i:=0 to n-1 do SendMessageTimeout(a[i], 0, 0, 0, 2, 500, res)
  else for i:=0 to n-1 do SendNotifyMessage(a[i], 0, 0, 0);
  if silent then Sleep(500);
end;

function IsDotNetInstalled(): Boolean;
var
  args: string;
  fileName: string;
  output: AnsiString;
  resultCode: Integer;
begin
  Result := false;
  //exit;
  fileName := ExpandConstant('{tmp}\dotnet.txt');
  args := '/C dotnet --list-runtimes > "' + fileName + '" 2>&1';
  if Exec(ExpandConstant('{cmd}'), args, '', SW_HIDE, ewWaitUntilTerminated, resultCode) and (resultCode = 0) then
  begin
    if LoadStringFromFile(fileName, output) then Result := Pos('Microsoft.WindowsDesktop.App 8.', output) > 0;
  end;
  DeleteFile(fileName);
end;

function InstallDotNet(): Boolean;
//function InstallDotNet(sdk: Boolean): Boolean; //rejected. Downloads 200MB, installs ~800 MB (and slow). Probably most users uninstall the app or never use NuGet, and the SDK would stay there unused.
var
  ResultCode: Integer;
  DownloadPage: TDownloadWizardPage;
  url, setupFile, info1, info2: string;
  a1: TArrayOfString;
begin
  //Get the download URL of the latest .NET Desktop Runtime.
  //  Info: Script "Check for new .NET version" runs every day. If a new .NET version available, updates https://www.libreautomate.com/download/net-url.txt.
  try
    DownloadTemporaryFile('https://www.libreautomate.com/download/net-url.txt', 'net-url.txt', '', nil);
    if LoadStringsFromFile(ExpandConstant('{tmp}\net-url.txt'), a1) then url := a1[0];
    //Log(url);
  except
    Log(GetExceptionMessage);
  end;
  
  //If the above failed, use this hardcoded URL. This URL is updated for each new .NET 8.0.x version.
  //  Info: Script "Check for new .NET version" runs every day. If a new .NET version available, updates this string in this .iss file.
  if Length(url) = 0 then url := 'https://download.visualstudio.microsoft.com/download/pr/bb581716-4cca-466e-9857-512e2371734b/5fe261422a7305171866fd7812d0976f/windowsdesktop-runtime-8.0.7-win-x64.exe';
  
  //rejected. It's a legacy undocumented URL. Very slow in some countries, eg China, because does not use CDN.
  //url := 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';
  //Unofficial URL of the latest .NET 8 version. Found in https://github.com/dotnet/installer/issues/11040
  //The official URL (in the runtime download page) is only for that patch version (8.0.x).
  //For preview use (not tested): https://aka.ms/dotnet/8.0/preview/windowsdesktop-runtime-win-x64.exe
    
  Result := true;
  setupFile := ExtractFileName(url);
  info1 := 'Installing .NET 8 Desktop Runtime x64';
  info2 := 'If stopped or failed now, will need to download/install it later. Size ~55 MB.';
  
  DownloadPage := CreateDownloadPage(info1, info2, nil);
  DownloadPage.Clear;
  DownloadPage.Add(url, setupFile, '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
      setupFile := ExpandConstant('{tmp}\' + setupFile);
      Result := Exec(setupFile, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
      //Result := Exec(setupFile, '/install /passive /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0); //bad for /VERYSILENT
      DeleteFile(setupFile);
    except
      if DownloadPage.AbortedByUser then Log('Aborted by user.') else Log(GetExceptionMessage);
      Result := false;
    end;
  finally
    DownloadPage.Hide;
  end;
end;

function InitializeSetup(): Boolean;
begin
  //Cpp_Install(0, '');
  UnloadAuCppDll(false);
  Result:=true;
end;

function InitializeUninstall(): Boolean;
begin
  //Cpp_Uninstall(0);
  UnloadAuCppDll(true);
  Result:=true;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  case CurStep of
    ssInstall:
    begin
      //Cpp_Install(1, ExpandConstant('{app}\'));
    end;
     ssPostInstall:
     begin
//       Cpp_Install(2, ExpandConstant('{app}\'));
       if not IsDotNetInstalled() then InstallDotNet();
     end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  s1: String;
begin
  case CurUninstallStep of
//     usUninstall:
//     begin
//       Cpp_Uninstall(1);
//       UnloadDLL(ExpandConstant('{app}\Setup32.dll'));
//     end;
    usPostUninstall:
    begin
      s1:=ExpandConstant('{app}');
      if DirExists(s1) and not RemoveDir(s1) then begin RestartReplace(s1, ''); end;
    end;
  end;
end;
