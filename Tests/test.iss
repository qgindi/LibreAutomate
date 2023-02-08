#define MyAppName "Test"
#define MyAppNameShort "Test"
#define MyAppVersion "0.12.0"
#define MyAppPublisher "Gintaras Didžgalvis"
#define MyAppURL "https://www.libreautomate.com/"
#define MyAppExeName "Test.exe"

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
OutputBaseFilename=TestSetup
Compression=lzma/normal
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
MinVersion=0,6.1sp1
DisableProgramGroupPage=yes
UsePreviousGroup=False
DisableDirPage=no
;PrivilegesRequired=lowest

[Code]

function IsDotNetInstalled(): Boolean;
var
  args: string;
  fileName: string;
  output: AnsiString;
  resultCode: Integer;
begin
  Result := false;
  fileName := ExpandConstant('{tmp}\dotnet.txt');
  args := '/C dotnet --list-runtimes > "' + fileName + '" 2>&1';
  if Exec(ExpandConstant('{cmd}'), args, '', SW_HIDE, ewWaitUntilTerminated, resultCode) and (resultCode = 0) then
  begin
    if LoadStringFromFile(fileName, output) then Result := Pos('Microsoft.WindowsDesktop.App 6.', output) > 0;
  end;
  DeleteFile(fileName);
end;

function InstallDotNet(): Boolean;
//function InstallDotNet(sdk: Boolean): Boolean;
var
  DownloadPage: TDownloadWizardPage;
  url, setupFile, info1, info2: string;
  ResultCode: Integer;
begin
  Result := true;
//   if sdk then begin //rejected. Downloads 200MB, installs ~800 MB (and slow). Probably most users uninstall the app or never use NuGet, and the SDK would stay there unused.
//     info1 := 'Installing .NET 6 SDK x64';
//     info2 := 'Optional. Adds some advanced features (NuGet). If stopped or failed now, you can download and install it later. Size ~200 MB.';
//     url := 'https://aka.ms/dotnet/6.0/dotnet-sdk-win-x64.exe';
//   end else begin
    info1 := 'Installing .NET 6 Desktop Runtime x64';
    info2 := 'If stopped or failed now, will need to download/install it later. Size ~65 MB.';
    url := 'https://aka.ms/dotnet/6.0/windowsdesktop-runtime-win-x64.exe';
    //Unofficial URL of the latest .NET 6 version. Found in answers only. The runtime download page provides only URL for that version; eg 6.0.13, but not 6.0.*.
//  end;
  setupFile := ExtractFileName(url);
  
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

// function InitializeSetup(): Boolean;
// begin
//   Test();
//   Result:=false;
// end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  case CurStep of
    ssPostInstall:
    begin
      if not IsDotNetInstalled() then InstallDotNet();
    end;
  end;
end;

// procedure CurPageChanged(CurPageID: Integer);
// var page: TWizardPage;
// var txt: TNewStaticText;
// begin
//   //MsgBox(Format('%d', [CurPageID]), mbInformation, MB_OK);
//   
//   case CurPageID of
//     wpSelectTasks:
//     begin
// 			if _needNet then begin
// 				//replace the default Tasks page static text. It's better than using [Tasks] GroupDescription for it.
// 				page := PageFromID(CurPageID);
// 				txt := TNewStaticText(page.Surface.Controls[1]); //Controls[0] is the checkgroup
// 				txt.AutoSize := True;
// 				txt.Caption := 'You will need to download and install .NET 6 SDK x64 (~200 MB). Or .NET 6 Desktop Runtime x64 (~60 MB) now and SDK later (when/if will need).';
// 			end;
//     end;
//   end;
// end;
