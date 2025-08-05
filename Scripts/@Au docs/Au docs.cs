/*/
noWarnings CS8632;
testInternal Au.Editor,Au,Microsoft.CodeAnalysis,Microsoft.CodeAnalysis.CSharp,Microsoft.CodeAnalysis.Features,Microsoft.CodeAnalysis.CSharp.Features,Microsoft.CodeAnalysis.Workspaces,Microsoft.CodeAnalysis.CSharp.Workspaces;
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

//args = new[] { "/upload" };
var siteDir = @"C:\Temp\Au\DocFX\site";

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

void _Build() {
	print.clear();
	var time0 = perf.ms;
	
	bool testSmall = !true;
	bool cookbook = false, preprocess = false, postprocess = false, build = false, serve = false;
	//cookbook = true;
	//preprocess = true;
	//postprocess = true;
	//postprocess = serve = true;
	if (!(cookbook | preprocess | postprocess | build | serve)) preprocess = postprocess = build = serve = cookbook = true;
	bool onlyMetadata = !true;
	//preprocess = true; build = true; onlyMetadata = true;
	
	var sourceDir = testSmall ? @"C:\code\au\Test Projects\TestDocFX" : @"C:\code\au\Au";
	var sourceDirPreprocessed = @"C:\Temp\Au\DocFX\source";
	var docDir = testSmall ? @"C:\code\au\Test Projects\TestDocFX\docfx_project" : @"C:\code\au\Other\DocFX\_doc";
	var siteDirTemp = siteDir + "-temp";
	
	if (cookbook) {
		AuDocs.Cookbook(docDir);
		print.it("DONE cookbook");
	}
	
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
		r = run.console(o => { print.it(o); }, docfx, "metadata");
		if (r != 0) { print.it("docfx metadata", r); return; }
		if (onlyMetadata) { print.it("metadata ok"); return; }
		r = run.console(o => { print.it(o); }, docfx, $@"build");
		if (r != 0) { print.it("docfx build", r); return; }
		//print.it("build ok");
		postprocess |= serve;
		filesystem.delete(Directory.EnumerateFiles(docDir + @"\api", "*.yml")); //garbage for VS search
		filesystem.delete(docDir + @"\api\.manifest");
	}
	
	if (postprocess) {
		d.Postprocess(siteDirTemp, siteDir);
		print.it("DONE postprocessing");
		if (!testSmall) print.it($"<><script Au docs.cs|/upload>Upload Au docs...<>");
	}
	
	print.it((perf.ms - time0) / 1000d);
	
	if (serve) {
		//r = run.console(o => { print.it(o); }, docfx, $"serve ""{siteDir}""");
		//if (r != 0) { print.it("docfx serve", r); return; } //no, it prints -1 when process killed
		run.it(docfx, $@"serve ""{siteDir}""", flags: RFlags.InheritAdmin, dirEtc: new() { WindowState = ProcessWindowStyle.Hidden });
	}
	
	//print.scrollToTop();
}

void _KillDocfxProcesses() {
	foreach (var v in process.getProcessIds("docfx.exe")) process.terminate(v);
}
