#define MyAppName "LibreAutomate"
#define MyAppNameShort "LibreAutomate"
#define MyAppVersion "1.14.0"
#define MyAppPublisher "Gintaras Didžgalvis"
#define MyAppURL "https://www.libreautomate.com/"
#define MyAppExeName "Au.Editor.exe"

[Setup]
//note: the " C#" here is inherited from the old LA full name "LibreAutomate C#". Can't change AppId easily. Docs: not used for display anywhere.
AppId=LibreAutomate C#
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
ArchitecturesAllowed=x64 arm64
ArchitecturesInstallIn64BitMode=x64 arm64
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
#if true
Source: "Au.Editor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Editor-arm.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Editor.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Editor.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Task-x64.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Task-arm.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Controls.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Controls.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Net4.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "AxMSTSCLib.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "MSTSCLib.dll"; DestDir: "{app}"; Flags: ignoreversion

Source: "64\Au.AppHost.exe"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "64\ARM\Au.AppHost.exe"; DestDir: "{app}\64\ARM"; Flags: ignoreversion
Source: "32\Au.AppHost.exe"; DestDir: "{app}\32"; Flags: ignoreversion

Source: "64\AuCpp.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "64\ARM\AuCpp.dll"; DestDir: "{app}\64\ARM"; Flags: ignoreversion
Source: "32\AuCpp.dll"; DestDir: "{app}\32"; Flags: ignoreversion

Source: "64\Au.DllHost.exe"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "64\ARM\Au.DllHost.exe"; DestDir: "{app}\64\ARM"; Flags: ignoreversion
Source: "32\Au.DllHost.exe"; DestDir: "{app}\32"; Flags: ignoreversion

Source: "64\Scintilla.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "64\ARM\Scintilla.dll"; DestDir: "{app}\64\ARM"; Flags: ignoreversion

Source: "32\7za.exe"; DestDir: "{app}\32"; Flags: ignoreversion

Source: "Debugger\*.dll"; DestDir: "{app}\Debugger"; Flags: ignoreversion recursesubdirs
Source: "Debugger\*.exe"; DestDir: "{app}\Debugger"; Flags: ignoreversion recursesubdirs

Source: "Roslyn\*.dll"; DestDir: "{app}\Roslyn"; Flags: ignoreversion

Source: "Default\*"; DestDir: "{app}\Default"; Flags: ignoreversion
Source: "Default\Workspace\files\*"; DestDir: "{app}\Default\Workspace\files"; Flags: ignoreversion recursesubdirs
Source: "Default\Workspace\files.xml"; DestDir: "{app}\Default\Workspace"; Flags: ignoreversion
Source: "Default\Themes\*"; DestDir: "{app}\Default\Themes"; Flags: ignoreversion
Source: "Templates\files\*"; DestDir: "{app}\Templates\files"; Flags: ignoreversion recursesubdirs
Source: "Templates\files.xml"; DestDir: "{app}\Templates"; Flags: ignoreversion

Source: "doc.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "ref.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "winapi.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "icons.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "cookbook.db"; DestDir: "{app}"; Flags: ignoreversion

Source: "default.exe.manifest"; DestDir: "{app}"; Flags: ignoreversion
Source: "xrefmap.yml"; DestDir: "{app}"; Flags: ignoreversion
#endif

//CONSIDER: don't include big not frequently updated files. Auto-download on demand.
//	All .db except cookbook, maybe Roslyn folder. Makes smaller: 40 MB -> 5 MB.

[Dirs]
Name: "{commonappdata}\LibreAutomate"; Flags: uninsalwaysuninstall; Permissions: authusers-modify
Name: "{app}\Git"; Flags: uninsalwaysuninstall; Permissions: authusers-modify
Name: "{app}\SDK"; Flags: uninsalwaysuninstall; Permissions: authusers-modify
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

//FUTURE: remove this code
Type: files; Name: "{app}\Au.Task.exe"
Type: files; Name: "{autoprograms}\LibreAutomate C#.lnk"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Registry]
;register app path
Root: HKLM; Subkey: Software\Microsoft\Windows\CurrentVersion\App Paths\Au.Editor.exe; ValueType: string; ValueData: {app}\Au.Editor.exe; Flags: uninsdeletevalue uninsdeletekeyifempty

//FUTURE: remove this. It's to fix a result of a bug in the setup of LA 1.13.0, 2025/07/06, fixed in 1.13.1.
Root: HKLM; Subkey: Software\Microsoft\Windows\CurrentVersion\Uninstall\LibreAutomate_is1; Flags: deletekey

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

var
  IsDotNetInstalledCalled, IsDotNetInstalledSaidYes: Boolean;

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

function _IsDotNetInstalled(): Boolean;
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
    if LoadStringFromFile(fileName, output) then Result := Pos('Microsoft.WindowsDesktop.App 9.', output) > 0;
  end;
  DeleteFile(fileName);
	
	//tested: If installed dotnet for multiple platforms (eg ARM64, x64, x86), only the runtime of the OS platform adds its path to PATH. It is what we need.
end;

function IsDotNetInstalled(): Boolean;
begin
  if IsDotNetInstalledCalled then
  begin
    Result := IsDotNetInstalledSaidYes;
	end else begin
		Result := _IsDotNetInstalled;
		IsDotNetInstalledSaidYes := Result;
		IsDotNetInstalledCalled := True;
  end;
end;

function InstallDotNet(): Boolean;
var
  ResultCode: Integer;
  page: TDownloadWizardPage;
  url, setupFileName, setupFilePath, txtFileName: string;
  urls: TArrayOfString;
begin
  Result := true;
  
  page := CreateDownloadPage('Installing .NET 9 Desktop Runtime', 'If stopped or failed now, download/install it manually. Size ~60 MB.', nil);
  page.Clear;
  page.Msg1Label.Caption := 'Getting the download URL of the current version.';
	txtFileName := 'net-9-url.txt';
  page.Add('https://www.libreautomate.com/download/' + txtFileName, txtFileName, '');
  page.Show;
	try
		page.Download;
		//Get the download URL of the latest .NET Desktop Runtime.
		//  Info: Script "Check for new .NET version" runs every day. If a new .NET build version available, updates the URLs here and in https://www.libreautomate.com/download/net-x-url.txt.
		//	Bad: may be inaccessible and hang, eg in China. Never mind: instead can exec curl with timeout.
		LoadStringsFromFile(ExpandConstant('{tmp}\' + txtFileName), urls);
	except
		//MsgBox('Failed: ' + 'https://www.libreautomate.com/download/' + txtFileName, mbInformation, MB_OK);
	end;
  
  //If the above failed, use these hardcoded URLs.
  //  Script "Check for new .NET version" runs every day. If a new .NET build version available, updates these strings here.
  if (Length(urls) < 2) then
  begin
    SetLength(urls, 2);
    urls[0] := 'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/9.0.8/windowsdesktop-runtime-9.0.8-win-x64.exe';
    urls[1] := 'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/9.0.8/windowsdesktop-runtime-9.0.8-win-arm64.exe';
  end;
	
	if IsArm64 then url := urls[1] else url := urls[0];
  setupFileName := ExtractFileName(url);
    
  page.Clear;
	page.Msg1Label.Caption := 'Downloading.';
  page.Add(url, setupFileName, '');
	try
		page.Download;
		
		page.Msg1Label.Caption := 'Executing the .NET installer.';
		page.Msg2Label.Caption := setupFileName;
		page.ProgressBar.Style := npbstMarquee;
		page.AbortButton.Hide;
		
		setupFilePath := ExpandConstant('{tmp}\' + setupFileName);
		ResultCode:=-111;
		Result := Exec(setupFilePath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
		//never mind: if the .NET installer in UI mode shows "Modify Setup" (eg if dotnet dir deleted), in silent mode does nothing and returns 0.
		//	Could call _IsDotNetInstalled and retry in UI mode, but now it does not work because this process uses old PATH env var (without dotnet path).
		//info: in Windows Sandbox .NET 9.0.3+ setup hangs for ~10 minutes. Same with UI. Used to work normally. See https://github.com/microsoft/Windows-Sandbox/issues/68
		DeleteFile(setupFilePath);
	except
		if page.AbortedByUser then Log('Aborted by user.') else Log(GetExceptionMessage);
		Result := false;
	finally
		page.Hide;
	end;
end;

procedure InstallExeForCurrentArch(fileName: String);
var
  s, s64: String;
begin
  if IsArm64 then
  begin
		s := ExpandConstant('{app}\') + fileName + '.exe';
		s64 := ExpandConstant('{app}\') + fileName + '-x64.exe';
		DeleteFile(s64);
    RenameFile(s, s64);
    RenameFile(ExpandConstant('{app}\') + fileName + '-arm.exe', s);
		//info: rename both, not rename+delete. Need both x64 and ARM64 files for portable LA.
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
      //Cpp_Install(2, ExpandConstant('{app}\'));
      InstallExeForCurrentArch('Au.Editor');
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

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpReady then
  begin
    if not IsDotNetInstalled then
    begin
      WizardForm.ReadyMemo.Lines.Add('');
      WizardForm.ReadyMemo.Lines.Add('Additional tasks:');
      WizardForm.ReadyMemo.Lines.Add('      Install .NET 9 Desktop Runtime (~60 MB download)');
    end;
  end;
end;