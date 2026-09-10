using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;

using var sett = Settings.Load();
var b = new wpfBuilder(script.name).WinSize(1000, 600);

/*
<a {() => _Script(".cs")}></a>
<a {() => ScriptEditor.Open(".cs")}></a>
*/

var w = b.Window;

b.Row(-1).Add(C.CreateFlowDocumentControl(w, out var fd));
//wpfBuilder.formatTextOf(fd, $"""
b.FormatText($"""
The #links here run a script. Ctrl+click to open the script instead.

<a {() => _Script("LA version and resources.cs")}>#Change version</a> if need.

Review all TODO. Occasionally review TODO2, FUTURE, CONSIDER.
	TODO2 means "low priority TODO". If after a long time it still seems low priority, delete the comment or replace with TODO3.

May need to test something. Eg some changes could break some main features.
	Also test new and some main features on other OS. Test on ARM-64.

May need to undefine TRACE in some C++ projects.
	Usually not using TRACE in Release config, but sometimes may use it to test Release speed etc.
	Don't use TRACE in C# projects; it's defined by default in Debug and Release config.

May need to delete test code. Or use #if DEBUG, like in Tests.cs.
	Review perf and print.

May need to add new OS version to class osVersion. Usually once/year.

Build solution in config Release, all platforms.
	NOTE: use the "Rebuild solution" command, else VS may not update something.

Review menu <a {() => ScriptEditor.InvokeCommand("About")}>Help -> About</a>. May need to update C# version, used libraries.

<a {() => _Script("Au docs.cs")}>#Create docs</a>.
	Fix errors if need. More info in AuDocs.txt.
	Don't upload now.
	It also runs 2 other scripts that create files for local docs.
	
Tasks for AI features:
	<a {() => _Script("LA docs doc-ai.db.cs")}>#Create database for AI</a> (doc-ai.db).
	<a {() => _Script("Upload AI embeddings.cs")}>#Upload vectors to GitHub</a>. The script also creates missing vectors. Uploads only if modified.
	Optionally <a {() => _Script("GitHub push LA docs for AI.cs")}>#upload md files for AI to GitHub</a>.

May need to update some info in README.md for github. Eg the screenshot and "how to build".
	When an image updated, github still shows the old cached. Workaround: in the image link append #x and change x for each image version. If using CloudFlare (currently not): before run script "Purge Cloudflare CDN cache".

Review the <a {() => { _OpenVersionMd(); }}>md file of current version</a>.
	If it's still "future.md", rename ( RENAME!!! ) to "v1.x.md".
	Edit the release date.
	Note: the md file path and name format is used in `App.CheckForUpdates`.

<a {() => _Script("Create LA setup files.cs")}>#Create LA setup files</a>.
	The script creates 2 .lzma files containing LA files, and the setup .exe file (builds the setup project). All 3 files are in the same folder; opens it.
	Run the setup file to test. Let it run LA. Occasionally uninstall/install, not just upgrade.
	Test on other OS too. And on Windows arm64.
	The setup will use the .lzma files. If want to test downloading, run it later, after creating GitHub release, without the .lzma files in the folder.

<a {() => _Script("Virustotal false positives.cs")}>#Scan the setup file at virustotal.com</a>. The script also runs once/day.
	The create-setup script also creates file vt-1.zip. Sometimes scan it too.
	Never mind if there are 1-2 false positives from less known AV.
	When creating setup, I scanned it often. Randomly was Microsoft FP. Try to change something, eg change a string or toggle `#define USE_RESTARTMANAGER` in Util.cs.

Github commit all and push. The message must be like v1.2.3.
	Note: do it now. Need for upload to GitHub. Also uploads the "changes" doc.

<a {() => _Script("Create LA GitHub Release.cs")}>#Create LA GitHub Release</a>. The script uploads the setup .exe file and the .lzma files.

<a {() => _Script("Au docs.cs", "/upload")}>#Upload website</a> (created by the "#Create docs" task).

Review web pages (LA and Github).

Download and test the setup file.
	It will download/use/delete 1 or 2 .lzma files from the GitHub release, unless you first manually download them to the same folder as the setup file.
	Occasionally test it on all OS. Observe SmartScreen/antivirus behavior.

Sometimes test whether others can build and run the repo.
	Eg when some new files added to the `_` dir.
	Generated or downloaded files in `_` dir/subdirs must be gitignored, unless very small non-binary. Instead specify them in `GitBinaryFiles.cs` in project `BuildEvents`.

Release NuGet package:
	<a {() => _Script("Create NuGet package.cs")}>#Create NuGet package</a>. The script builds Au using a modified temp csproj, creates package and adds native dlls.
	Upload to NuGet. The script prints links.

<a {() => _Script("Update version.txt.cs")}>#Update version.txt</a>. The script updates _\version.txt and uploads to the LA website. Need it for users of older LA; now using GitHub instead.

""");

b.End();
#if WPF_PREVIEW //menu Edit > View > WPF preview
b.Window.Preview();
#else
b.WinSaved(sett.placement, o => { sett.placement = o; });
#endif
if (!b.ShowDialog()) return;
//print.it(text1.Text, combo1.SelectedIndex, c1.IsChecked == true);

void _Script([ParamString(PSFormat.CodeFile)] string name, params string[] args) {
	if (keys.gui.isCtrl) ScriptEditor.Open(name);
	else script.run(name, args);
}

void _OpenVersionMd() {
	var verXX = Version.Parse(Au_.Version).ToString(2);
	var dir = folders.Editor + @"..\Other\DocFX\_doc\changes\";
	var md = dir + $"v{verXX}.md";
	if (!File.Exists(md)) {
		if (!dialog.showOkCancel(null, $"Rename future.md to v{verXX}.md")) return;
		filesystem.rename(dir + "future.md", md);
	}
	run.itSafe(md, flags: RFlags.InheritAdmin);
	//bad: starts new VS process. The following does not help.
	//var w1 = wnd.find("Au - *Microsoft Visual Studio*", "HwndWrapper[DefaultDomain;*");
	//if (w1.Is0) run.itSafe(md, flags: RFlags.InheritAdmin);
	//else run.itSafe(w1.ProgramPath, md, flags: RFlags.InheritAdmin);
}

record class Settings : JSettings {
	public static readonly string File = folders.ThisAppDocuments + $"{script.name}.json";
	
	public static Settings Load() => Load<Settings>(File);
	
	public string placement;
}

static class C {
	public static FlowDocumentScrollViewer CreateFlowDocumentControl(Window w, out FlowDocument d) {
		d = new FlowDocument {
			FontFamily = w.FontFamily,
			FontSize = w.FontSize,
			Background = SystemColors.WindowBrush,
			PagePadding = new(3, 0, 1, 0),
			TextAlignment = TextAlignment.Left,
			//PageWidth = 600, //to disable word wrap. There are no better ways.
		};
		TextOptions.SetTextFormattingMode(d, TextFormattingMode.Display);
		
		var c = new FlowDocumentScrollViewer {
			Document = d,
			FocusVisualStyle = null,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			IsInactiveSelectionHighlightEnabled = true,
			BorderThickness = new(1),
			BorderBrush = SystemColors.ControlDarkBrush,
		};
		return c;
	}
}
