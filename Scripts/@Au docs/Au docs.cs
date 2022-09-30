/*/
role editorExtension;
define SCRIPT;
noWarnings CS8632;
testInternal Au.Editor,Au.Controls,Au,Microsoft.CodeAnalysis,Microsoft.CodeAnalysis.CSharp,Microsoft.CodeAnalysis.Features,Microsoft.CodeAnalysis.CSharp.Features,Microsoft.CodeAnalysis.Workspaces,Microsoft.CodeAnalysis.CSharp.Workspaces;
r Au.Controls.dll;
r Au.Editor.dll;
r Roslyn\Microsoft.CodeAnalysis.dll;
r Roslyn\Microsoft.CodeAnalysis.CSharp.dll;
r Roslyn\Microsoft.CodeAnalysis.Features.dll;
r Roslyn\Microsoft.CodeAnalysis.CSharp.Features.dll;
r Roslyn\Microsoft.CodeAnalysis.Workspaces.dll;
r Roslyn\Microsoft.CodeAnalysis.CSharp.Workspaces.dll;
nuget -\Markdig;
nuget -\WeCantSpell.Hunspell;
/*/

var siteDir = @"C:\Temp\Au\DocFX\site";

Task.Run(() => {
	try {
		if (args.Length == 0) {
			_Build();
		} else if (args[0] == "/upload") {
			AuDocs.CompressAndUpload(siteDir);
		}
	}
	catch (Exception e1) {
		print.it(e1);
		_KillDocfxProcesses();
	}
});

void _Build() {
	print.clear();
	var time0 = perf.ms;
	
	bool testSmall = !true;
	bool preprocess = false, postprocess = false, build = false, serve = false;
	//preprocess = true;
	//postprocess = true;
	//postprocess = serve = true;
	preprocess = postprocess = build = serve = true;
	bool onlyMetadata = !true;
	//preprocess = true; build = true; onlyMetadata = true;
	
	var sourceDir = testSmall ? @"C:\code\au\Test Projects\TestDocFX" : @"C:\code\au\Au";
	var sourceDirPreprocessed = @"C:\Temp\Au\DocFX\source";
	var docDir = testSmall ? @"C:\code\au\Test Projects\TestDocFX\docfx_project" : @"C:\code\au\Other\DocFX\_doc";
	var siteDirTemp = siteDir + "-temp";
	
	var d = new AuDocs();
	if (preprocess) {
		d.Preprocess(sourceDir, sourceDirPreprocessed, testSmall);
		print.it("DONE preprocessing");
	}
	
	var docfx = folders.Downloads + @"docfx\docfx.exe";
	int r;
	if (build || serve) {
		_KillDocfxProcesses();
		Environment.CurrentDirectory = docDir;
	}
	
	if (build) {
		filesystem.delete(siteDirTemp);
		using var sdkwa = new SdkWorkaround();
		r = run.console(o => { print.it(o); }, docfx, "metadata");
		if (r != 0) { print.it("docfx metadata", r); return; }
		if (onlyMetadata) { print.it("metadata ok"); return; }
		r = run.console(o => { print.it(o); }, docfx, "build");
		if (r != 0) { print.it("docfx build", r); return; }
		//print.it("build ok");
		postprocess |= serve;
		filesystem.delete(Directory.EnumerateFiles(docDir + @"\api", "*.yml")); //garbage for VS search
	}
	
	if (postprocess) {
		d.Postprocess(siteDirTemp, siteDir);
		print.it("DONE postprocessing");
		if(!testSmall) print.it($"<><script Au docs.cs|/upload>Upload Au docs...<>");
	}
	
	print.it((perf.ms - time0) / 1000d);
	
	if (serve) {
		//r = run.console(o => { print.it(o); }, docfx, $"serve ""{siteDir}""");
		//if (r != 0) { print.it("docfx serve", r); return; } //no, it prints -1 when process killed
		run.it(docfx, $@"serve ""{siteDir}""", flags: RFlags.InheritAdmin, dirEtc: new() { WindowState = ProcessWindowStyle.Hidden });
	}
}

void _KillDocfxProcesses() {
	foreach (var v in process.getProcessIds("docfx.exe")) process.terminate(v);
}

/// <summary>
/// Workaround for: if Au uses .NET 6 and is installed a .NET 7 SDK, cannot resolve cref of .NET types etc. Prints many warnings.
/// </summary>
class SdkWorkaround : IDisposable {
	FEFile[] _a;
	public SdkWorkaround() {
		_a = filesystem.enumDirectories(folders.NetRuntimeBS + @"..\..\..\sdk").Where(o => o.Name.ToInt() > 6).ToArray();
		foreach (var v in _a) {
			filesystem.rename(v.FullPath, "@" + v.Name);
		}
	}
	
	public void Dispose() {
		foreach (var v in _a) {
			filesystem.rename(v.FullPath.RxReplace(@"\\([^\\]+)$", @"\@$1"), v.Name);
		}
	}
}
