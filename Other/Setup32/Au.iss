#define MyAppName "LibreAutomate C#"
#define MyAppNameShort "LibreAutomate"
#define MyAppVersion "0.11.0"
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
SourceDir=..\..\_
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
Source: "Setup32.dll"; DestDir: "{app}"; Flags: ignoreversion

Source: "Au.Editor.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Editor.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Task.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Controls.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Controls.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Net4.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Au.Net4.exe.config"; DestDir: "{app}"; Flags: ignoreversion

Source: "Roslyn\*.dll"; DestDir: "{app}\Roslyn"; Flags: ignoreversion
Source: "64\Au.AppHost.exe"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "64\AuCpp.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "64\Scintilla.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "64\sqlite3.dll"; DestDir: "{app}\64"; Flags: ignoreversion
Source: "32\Au.AppHost.exe"; DestDir: "{app}\32"; Flags: ignoreversion
Source: "32\AuCpp.dll"; DestDir: "{app}\32"; Flags: ignoreversion
Source: "32\sqlite3.dll"; DestDir: "{app}\32"; Flags: ignoreversion

Source: "Default\*"; DestDir: "{app}\Default"; Excludes: ".*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Templates\files\*"; DestDir: "{app}\Templates\files"; Flags: ignoreversion recursesubdirs
Source: "Templates\files.xml"; DestDir: "{app}\Templates"; Flags: ignoreversion
Source: "Cookbook\files\*"; DestDir: "{app}\Cookbook\files"; Excludes: "-*"; Flags: ignoreversion recursesubdirs
Source: "Cookbook\files.xml"; DestDir: "{app}\Cookbook"; Flags: ignoreversion

Source: "default.exe.manifest"; DestDir: "{app}"; Flags: ignoreversion
Source: "doc.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "ref.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "winapi.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "icons.db"; DestDir: "{app}"; Flags: ignoreversion
Source: "xrefmap.yml"; DestDir: "{app}"; Flags: ignoreversion
Source: "dotnet_ref.txt"; DestDir: "{app}"; Flags: ignoreversion

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
Type: filesandordirs; Name: "{app}\Cookbook"
Type: filesandordirs; Name: "{commonprograms}\C# Uiscripter"
Type: filesandordirs; Name: "{commonprograms}\Automaticode C#"
Type: filesandordirs; Name: "{commonprograms}\Autepad C#"
Type: filesandordirs; Name: "{commonprograms}\Derobotize Me C#"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
;Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
;Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Registry]
;register app path
Root: HKLM; Subkey: Software\Microsoft\Windows\CurrentVersion\App Paths\Au.Editor.exe; ValueType: string; ValueData: {app}\Au.Editor.exe; Flags: uninsdeletevalue uninsdeletekeyifempty

;rejected: set environment variable Au.Path, to find unmanaged dlls used by Au.dll when it is used in various scripting environments etc that copy it somewhere (they don't copy unmanaged dlls)
; Rarely used. Overwrites mine. Let users learn it, then they can use the dlls on any computer without this program installed.
;Root: HKLM; Subkey: SYSTEM\CurrentControlSet\Control\Session Manager\Environment; ValueType: string; ValueData: {app}; Flags: uninsdeletevalue

[Tasks]
Name: "NetDownload"; Description: "Open the .NET download webpage"; Check: NeedDotnet
;note: don't use the automatic .NET Runtime installation script: https://www.codeproject.com/Articles/20868/Inno-Setup-Dependency-Installer
;  It is too fragile etc. The URL may vanish. Always uses a hardcoded version (need to edit everytime), even if a compatible version is installed. No SDK. Once the downloaded .NET setup program crashed. Entire setup fails if the script fails, eg because of .NET connection.

[Run]
Filename: "https://dotnet.microsoft.com/en-us/download/dotnet/6.0"; Flags: nowait shellexec skipifsilent; Tasks: NetDownload
Filename: "{app}\{#MyAppExeName}"; Flags: nowait postinstall skipifsilent; Description: "{cm:LaunchProgram,{#MyAppName}}"

[Code]
var _needNet: Boolean;

procedure Cpp_Install(step: Integer; dir: String);
external 'Cpp_Install@files:Setup32.dll cdecl setuponly delayload';

procedure Cpp_Uninstall(step: Integer);
external 'Cpp_Uninstall@{app}\Setup32.dll cdecl uninstallonly delayload';

function Cpp_NeedDotnet(): Boolean;
external 'Cpp_NeedDotnet@files:Setup32.dll cdecl setuponly delayload';

function NeedDotnet(): Boolean;
begin
	Result := Cpp_NeedDotnet(); //can't call directly from Tasks
	_needNet := Result;
end;

function InitializeSetup(): Boolean;
begin
  Cpp_Install(0, '');
  Result:=true;
end;

function InitializeUninstall(): Boolean;
begin
  Cpp_Uninstall(0);
  Result:=true;
end;

procedure _UninstallOld(dir: String);
var
  ResultCode: Integer;
begin
	dir:=ExpandConstant('{pf}\') + dir;
	if SameText(dir, ExpandConstant('{app}')) then exit;
	if not FileExists(dir + '\Au.Editor.exe') then exit;
	if not FileExists(dir + '\unins000.exe') then exit;
	Exec(dir + '\unins000.exe', '/SILENT /SUPPRESSMSGBOXES /NORESTART', '', 0, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
//var
//  s1: String;
begin
//  s1:=Format('%d', [CurStep]);
//  MsgBox(s1, mbInformation, MB_OK);
  
  case CurStep of
    ssInstall:
    begin
			//The preview program name changed several times. Uninstall previous preview version if its directory is one of the old names.
			_UninstallOld('Uiscripter');
			_UninstallOld('Automaticode');
			_UninstallOld('Autepad');
			_UninstallOld('Derobotizer');
			
      //Cpp_Install(1, ExpandConstant('{app}\'));
    end;
    ssPostInstall:
    begin
      Cpp_Install(2, ExpandConstant('{app}\'));
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  s1: String;
begin
//  s1:=Format('%d', [CurUninstallStep]);
//  MsgBox(s1, mbInformation, MB_OK);
  
  case CurUninstallStep of
    usUninstall:
    begin
      Cpp_Uninstall(1);
      UnloadDLL(ExpandConstant('{app}\Setup32.dll'));
    end;
    usPostUninstall:
    begin
      s1:=ExpandConstant('{app}');
      if DirExists(s1) and not RemoveDir(s1) then begin RestartReplace(s1, ''); end;
    end;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
var page: TWizardPage;
var txt: TNewStaticText;
begin
  //MsgBox(Format('%d', [CurPageID]), mbInformation, MB_OK);
  
  case CurPageID of
    wpSelectTasks:
    begin
			if _needNet then begin
				//replace the default Tasks page static text. It's better than using [Tasks] GroupDescription for it.
				page := PageFromID(CurPageID);
				txt := TNewStaticText(page.Surface.Controls[1]); //Controls[0] is the checkgroup
				txt.AutoSize := True;
				txt.Caption := 'You will need to download and install .NET 6 SDK x64 (~200 MB). Or .NET 6 Desktop Runtime x64 (~60 MB) now and SDK later (when/if will need).';
			end;
    end;
  end;
end;
