/*/
role exeProgram;
define IDE_LA,NO_GLOBAL,NO_DEFAULT_CHARSET_UNICODE;
noWarnings 1701,1702,8002,419;
testInternal Au.Controls,Au,Microsoft.CodeAnalysis,Microsoft.CodeAnalysis.CSharp,Microsoft.CodeAnalysis.Features,Microsoft.CodeAnalysis.CSharp.Features,Microsoft.CodeAnalysis.Workspaces,Microsoft.CodeAnalysis.CSharp.Workspaces;
postBuild _postbuild.cs /$(outputPath);
outputPath %folders.Workspace%\exe\Au.Editor;
icon resources\ico;
manifest resources\Au.manifest;
sign resources\Au.snk;
miscFlags 1;
noRef *\_\Au.dll;
pr \Au.sln\@Au\Au.cs;
pr \Au.sln\@Au.Controls\Au.Controls.cs;
r Roslyn\Microsoft.CodeAnalysis.dll;
r Roslyn\Microsoft.CodeAnalysis.CSharp.dll;
r Roslyn\Microsoft.CodeAnalysis.Features.dll;
r Roslyn\Microsoft.CodeAnalysis.CSharp.Features.dll;
r Roslyn\Microsoft.CodeAnalysis.Workspaces.dll;
r Roslyn\Microsoft.CodeAnalysis.CSharp.Workspaces.dll;
resource App\App-resources.xaml /embedded;
resource resources\ci /path;
resource resources\Images /path;
resource Tools\Keys.txt /path;
resource Tools\Regex.txt /path;
file Default;
/*/

/*
*/
