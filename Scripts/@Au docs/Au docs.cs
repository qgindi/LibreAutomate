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
var repoDir = @"C:\Users\dks\Source\repos\LibreAutomate";
var buildDir = repoDir + @"\_";
var apiDocDir = repoDir + @"\_\Docs";

try {
	if (args.Length == 0) {
		_Build();
	} else if (args[0] == "/upload") {
		AuDocs.CompressAndUpload(apiDocDir);
	}
}
catch (Exception e1) {
	print.it(e1);
	_KillDocfxProcesses();
}

/// Change the following file locations to reflect your local configuration
/// 	repoDir
/// 	docfxExeDir
/// 
/// Control the operations performed using the booleans cookbook, preprocess, docFxBuild, postprocess, serve.
/// For the final docFxBuild they should all be true except serve depending upon whether upload to a website is wanted.
/// 
/// Order of operations and source/destination of processed files
/// 	cookbook 	?	process cookbook from cookbookFilesDir --> processedCookbookDir
/// 	preprocess 	? 	preprocess docs from sourceDir --> preprocessedDir
/// 	docFxBuild 	? 	docFx run from preprocessedDir (and docFxDir?) --> docFxProcessedDir
/// 	postprocess ? 	postprocess docs from docFxProcessedDir --> apiDocDir
/// 	serve 		? 	run local docFx webserver on apiDocDir
/// 
void _Build() {
	print.clear();
	var time0 = perf.ms;
	
	bool testSmall = !true;
	bool onlyMetadata = !true;
	bool cookbook = false, preprocess = false, docFxBuild = false, postprocess = false, serve = false; 
	//cookbook = preprocess = docFxBuild = postprocess = serve = true;
	//cookbook = true;
	//preprocess = true;
	docFxBuild = true;
	postprocess = true;
	//serve = true;
	//preprocess = true; docFxBuild = true; onlyMetadata = true;
	
	var sourceDir = testSmall ? repoDir + @"\Test Projects\TestDocFX" : repoDir + @"\Au";
	// if change this, must also change in docfx.json file
	var preprocessedDir = repoDir + @"\Other\DocPreProcessed";
	var docfxExeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\.dotnet\tools"; 
	var docFxDir = testSmall ? repoDir + @"\Test Projects\TestDocFX\docfx_project" : repoDir + @"\Other\DocFX\_doc";
	// if change this, must also change in docfx.json file
	var docFxProcessedDir = repoDir + @"\Other\DocFxProcessed";
	
	var cookbookFilesDir = repoDir + @"\Cookbook\files";
	var processedCookbookDir = 	docFxDir + @"\cookbook\";
	
	bool cleanupFiles = !true;
	
	if (cookbook) {
		AuDocs.Cookbook(cookbookFilesDir, processedCookbookDir);
		print.it($"DONE cookbook {(perf.ms - time0) / 1000d}s");
	}
	
	var d = new AuDocs();
	if (preprocess) {
		d.Preprocess(sourceDir, preprocessedDir, testSmall);
		print.it($"DONE preprocessing {(perf.ms - time0) / 1000d}s");

	}
	
	var docFxExe = docfxExeDir + @"\docfx.exe";
	int r;
	if (docFxBuild || serve) {
		_KillDocfxProcesses();
		Environment.CurrentDirectory = docFxDir;
	}
	
	if (docFxBuild) {
		filesystem.delete(docFxProcessedDir);
		r = run.console(o => { print.it(o); }, docFxExe, "metadata");
		if (r != 0) { print.it("docfx metadata", r); return; }
		if (onlyMetadata) { print.it("metadata ok"); return; }
		r = run.console(o => { print.it(o); }, docFxExe, $@"build");
		if (r != 0) { print.it("docfx build", r); return; }
		print.it($"DONE docFx build {(perf.ms - time0) / 1000d}s");
		postprocess |= serve;
		filesystem.delete(Directory.EnumerateFiles(docFxDir + @"\api", "*.yml")); //garbage for VS search
}
	
	if (postprocess) {
		d.Postprocess(docFxProcessedDir, apiDocDir, buildDir);
		print.it($"DONE postprocessing {(perf.ms - time0) / 1000d}s");
		if (!testSmall) print.it($"<><script Au docs.cs|/upload>Upload Au docs...<>");
	}
	
	if (cleanupFiles) {
		if (docFxBuild) {
			filesystem.delete(Directory.GetFiles(processedCookbookDir));
			filesystem.delete(Directory.GetFiles(preprocessedDir));
		}
		if (postprocess) filesystem.delete(Directory.GetFiles(docFxProcessedDir));
		print.it("$DONE cleanup {(perf.ms - time0) / 1000d}s");
	}
	
	if (serve) {
		//r = run.console(o => { print.it(o); }, docfx, $"serve ""{apiDocDir}""");
		//if (r != 0) { print.it("docfx serve", r); return; } //no, it prints -1 when process killed
		run.it(docFxExe, $@"serve ""{apiDocDir}""", flags: RFlags.InheritAdmin, dirEtc: new() { WindowState = ProcessWindowStyle.Hidden });
		print.it($"DocFx Server started at default http://localhost:8080 {(perf.ms - time0) / 1000d}s");
	}
}

void _KillDocfxProcesses() {
	foreach (var v in process.getProcessIds("docfx.exe")) process.terminate(v);
}
