/*/
role exeProgram
define IDE_LA,NO_GLOBAL,NO_DEFAULT_CHARSET_UNICODE
noWarnings 8002,419
testInternal Microsoft.CodeAnalysis,Microsoft.CodeAnalysis.CSharp,Microsoft.CodeAnalysis.Features,Microsoft.CodeAnalysis.CSharp.Features,Microsoft.CodeAnalysis.Workspaces,Microsoft.CodeAnalysis.CSharp.Workspaces
preBuild .\_prePostBuild.cs
postBuild .\_prePostBuild.cs /post $(outputPath)
outputPath %folders.Workspace%\..\Au.Editor
icon resources\ico
manifest resources\Au.manifest
sign resources\Au.snk
miscFlags 1
noRef *\Au.dll
pr ..\@Au\Au.cs
pr ..\@Au.Controls\Au.Controls.cs
r Roslyn\Microsoft.CodeAnalysis.dll /noCopy
r Roslyn\Microsoft.CodeAnalysis.CSharp.dll /noCopy
r Roslyn\Microsoft.CodeAnalysis.Features.dll /noCopy
r Roslyn\Microsoft.CodeAnalysis.CSharp.Features.dll /noCopy
r Roslyn\Microsoft.CodeAnalysis.Workspaces.dll /alias=CAW|noCopy
r Roslyn\Microsoft.CodeAnalysis.CSharp.Workspaces.dll /noCopy
r AxMSTSCLib.dll
r MSTSCLib.dll
r NuGet.Configuration.dll
r NuGet.Versioning.dll
nuget webview\Microsoft.Web.WebView2
resource app\app-resources.xaml /path
resource resources\ci /path
file .\Default
/*/
