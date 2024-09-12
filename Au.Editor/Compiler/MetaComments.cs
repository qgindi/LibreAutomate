using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

//CONSIDER: c https://..../file.cs
//	Problem: security. These files could contain malicious code.
//	It seems nuget supports source files, not only compiled assemblies: https://stackoverflow.com/questions/52880687/how-to-share-source-code-via-nuget-packages-for-use-in-net-core-projects

namespace Au.Compiler;

//This XML doc is outdated. Most info is in the Properties dialog.
/// <summary>
/// Extracts C# file/project settings, references, etc from meta comments in C# code.
/// </summary>
/// <remarks>
/// To compile C# code, often need various settings, more files, etc. In Visual Studio you can set it in project Properties and Solution Explorer. In Au you can set it in C# code as meta comments.
/// Meta comments is a block of comments that starts and ends with <c>/*/</c>. Must be at the start of C# code. Before can be only comments, empty lines, spaces and tabs. Example:
/// <code><![CDATA[
/// /*/ option1 value1; option2 value2; option2 value3 /*/
/// ]]></code>
/// Options and values must match case, except filenames/paths. No "enclosing", no escaping.
/// Some options can be several times with different values, for example to specify several references.
/// When compiling multiple files (project, or using option 'c'), only the main file can contain all options. Other files can contain only 'r', 'c', 'com', 'nuget', 'resource', 'file'.
/// All available options are in the examples below. Here a|b|c means a or b or c. The //comments are not allowed in real meta comments.
/// </remarks>
/// <example>
/// <h3>References</h3>
/// <code><![CDATA[
/// r Assembly //assembly reference. With or without ".dll". Must be in folders.ThisApp.
/// r C:\X\Y\Assembly.dll //assembly reference using full path. If relative path, must be in folders.ThisApp.
/// r Alias=Assembly //assembly reference that can be used with C# keyword 'extern alias'.
/// ]]></code>
/// Don't need to add Au.dll and .NET runtime assemblies.
/// 
/// <h3>Other C# files to compile together</h3>
/// <code><![CDATA[
/// c file.cs //a class file in this C# file's folder
/// c folder\file.cs //path relative to this C# file's folder
/// c .\folder\file.cs //the same as above
/// c ..\folder\file.cs //path relative to the parent folder
/// c \folder\file.cs //path relative to the workspace folder
/// ]]></code>
/// The file must be in this workspace. Or it can be a link (in workspace) to an external file. The same is true with most other options.
/// If folder, compiles all its descendant class files.
/// 
/// <h3>References to libraries created in this workspace</h3>
/// <code><![CDATA[
/// pr \folder\file.cs
/// ]]></code>
/// Compiles the .cs file or its project and uses the output dll file like with option r. It is like a "project reference" in Visual Studio.
/// 
/// <h3>References to COM interop assemblies (.NET assemblies converted from COM type libraries)</h3>
/// <code><![CDATA[
/// com Accessibility 1.1 44782f49.dll
/// ]]></code>
/// How this different from option r:
/// 1. If not full path, must be in @"%folders.Workspace%\.interop".
/// 2. The interop assembly is used only when compiling, not at run time. It contains only metadata, not code. The compiler copies used parts of metadata to the output assembly. The real code is in native COM dll, which at run time must be registered as COM component and must match the bitness (64-bit or 32-bit) of the process that uses it. 
/// 
/// <h3>Files to add to managed resources</h3>
/// <code><![CDATA[
/// resource file.png  //file as stream. Can be filename or relative path, like with 'c'.
/// resource file.ext /byte[]  //file as byte[]
/// resource file.txt /string  //text file as string
/// resource file.csv /strings  //CSV file containing multiple strings as 2-column CSV (name, value)
/// resource file.png /embedded  //file as embedded resource stream
/// resource folder  //all files in folder, as streams
/// resource folder /byte[]  //all files in folder, as byte[]
/// resource folder /string  //all files in folder, file as strings
/// resource folder /embedded  //all files in folder, as embedded resource streams
/// ]]></code>
/// More info in .cs of the Properties window.
/// 
/// <h3>Other files</h3>
/// <code><![CDATA[
/// file file.png
/// file file.dll /output_subfolder
/// ]]></code>
/// 
/// <h3>Settings used when compiling</h3>
/// <code><![CDATA[
/// optimize false|true //if false (default), don't optimize code; this is known as "Debug configuration". If true, optimizes code; then low-level code is faster, but can be difficult to debug; this is known as "Release configuration".
/// define SYMBOL1,SYMBOL2,d:DEBUG_ONLY,r:RELEASE_ONLY //define preprocessor symbols that can be used with #if etc. If no optimize true, DEBUG and TRACE are added implicitly.
/// warningLevel 1 //compiler warning level.
/// noWarnings 3009,162 //don't show these compiler warnings
/// testInternal Assembly1,Assembly2 //access internal symbols of specified assemblies, like with InternalsVisibleToAttribute
/// preBuild file /arguments //run this script before compiling. More info below.
/// postBuild file /arguments //run this script after compiled successfully. More info below.
/// ]]></code>
/// About options 'preBuild' and 'postBuild':
/// The script must have meta option role editorExtension. It runs in compiler's thread. Compiler waits and does not respond during that time. To stop compilation, let the script throw an exception.
/// The script has parameter (variable) string[] args. If there is no /arguments, args[0] is the output assembly file, full path. Else args contains the specified arguments, parsed like a command line. In arguments you can use these variables:
/// $(outputFile) -  the output assembly file, full path; $(sourceFile) - the C# file, full path; $(source) - path of the C# file in workspace, eg "\folder\file.cs"; $(outputPath) - meta option 'outputPath', default ""; $(optimize) - meta option 'optimize', default "false".
/// 
/// <h3>Settings used to run the compiled script</h3>
/// <code><![CDATA[
/// ifRunning warn_restart|warn|cancel_restart|cancel|wait_restart|wait|run_restart|run|restart|end|end_restart //what to do if this script is already running. Default: warn_restart. More info below.
/// uac inherit|user|admin //UAC integrity level (IL) of the task process. Default: inherit. More info below.
/// bit32 false|true //if true, the task process is 32-bit even on 64-bit OS. It can use 32-bit and AnyCPU dlls, but not 64-bit dlls. Default: false.
/// ]]></code>
/// Here word "task" is used for "script that is running or should start".
/// Options 'ifRunning' and 'uac' are applied only when the task is started from editor process, not when it runs as independent exe program.
/// 
/// About ifRunning:
/// When trying to start this script, what to do if it is already running. Values:
/// warn - print warning and don't run.
/// cancel - don't run.
/// wait - run later, when that task ends.
/// run - run simultaneously.
/// restart - end it and run.
/// end - end it and don't run.
/// If ends with _restart, the Run button/menu will restart. Useful for quick edit-test.
/// 
/// About uac:
/// inherit (default) - the task process has the same UAC integrity level (IL) as the editor process.
/// user - Medium IL, like most applications. The task cannot automate high IL process windows, write some files, change some settings, etc.
/// admin - High IL, aka "administrator", "elevated". The task has many rights, but cannot automate some apps through COM, etc.
/// 
/// <h3>Settings used to create assembly file</h3>
/// <code><![CDATA[
/// role miniProgram|exeProgram|editorExtension|classLibrary|classFile //purpose of this C# file. Also the type of the output assembly file (exe, dll, none). Default: miniProgram for scripts, classFile for class files. More info below.
/// outputPath path //create output files (.exe, .dll, etc) in this directory. Used with role exeProgram and classLibrary. Can be full path or relative path like with 'c'. Default for exeProgram: %folders.Workspace%\exe\filename. Default for classLibrary: %folders.Workspace%\dll.
/// console false|true //let the program run with console
/// icon file.ico //icon of the .exe file. Can be filename or relative path, like with 'c'.
/// manifest file.manifest //manifest file of the .exe file. Can be filename or relative path, like with 'c'.
/// (rejected) resFile file.res //file containing native resources to add to the .exe/.dll file. Can be filename or relative path, like with 'c'.
/// sign file.snk //sign the output assembly with a strong name using this .snk file. Can be filename or relative path, like with 'c'. 
/// xmlDoc false|true //create XML documentation file from XML comments. Creates in the 'outputPath' directory.
/// ]]></code>
/// 
/// About role:
/// If role is 'exeProgram' or 'classLibrary', creates .exe or .dll file, named like this C# file, in 'outputPath' directory.
/// If role is 'miniProgram' (default for scripts) or 'editorExtension', creates a temporary assembly file in subfolder ".compiled" of the workspace folder.
/// If role is 'classFile' (default for class files) does not create any output files from this C# file. Its purpose is to be compiled together with other C# code files.
/// If role is 'editorExtension', the task runs in the main UI thread of the editor process. Rarely used. Can be used to create editor extensions. The user cannot see and end the task. Creates memory leaks when executing recompiled assemblies (eg after editing the script), because old assembly versions cannot be unloaded until process exits.
/// 
/// Full path can be used with 'r', 'com', 'outputPath'. It can start with an environment variable or special folder name, like <c>%folders.ThisAppDocuments%\file.exe</c>.
/// Files used with other options ('c', 'resource' etc) must be in this workspace.
/// 
/// About native resources:
/// (rejected) If option 'resFile' specified, adds resources from the file, and cannot add other resources; error if also specified 'icon' or 'manifest'.
/// If 'manifest' and 'resFile' not specified when creating .exe file, adds manifest from file "default.exe.manifest" in the main Au folder.
/// If 'resFile' not specified when creating .exe or .dll file, adds version resource, with values from attributes such as [assembly: AssemblyVersion("...")]; see how it is in Visual Studio projects, in file Properties\AssemblyInfo.cs.
/// </example>
class MetaComments {
	/// <summary>
	/// Name of the main C# file, without ".cs".
	/// </summary>
	public string Name { get; private set; }
	
	/// <summary>
	/// The compilation entry file. Probably not <c>CodeFiles[0]</c>.
	/// </summary>
	public MCCodeFile MainFile { get; private set; }
	
	/// <summary>
	/// All C# files of this compilation.
	/// The order is optimized for compilation and does not match the natural order:
	/// - If there is global.cs, it is the first, followed by its descendant meta c files, total <see cref="GlobalCount"/>.
	/// - Then main file, preceded by its descendant meta c files.
	/// - Then project files, each preceded by its descendant meta c files.
	/// </summary>
	public List<MCCodeFile> CodeFiles { get; private set; }
	
	/// <summary>
	/// Count of global files, ie global.cs and its meta c descendants. They are at the start of <see cref="CodeFiles"/>.
	/// Note: if main file is a descendant of global.cs, it and its descendants are not included.
	/// </summary>
	public int GlobalCount { get; private set; }
	
	/// <summary>
	/// Meta option 'optimize'.
	/// Default: false.
	/// </summary>
	public bool Optimize { get; private set; }
	
	/// <summary>
	/// Meta option 'define' + default preprocessor symbols.
	/// </summary>
	public string[] Defines { get; private set; }
	List<string> _defines;
	static readonly string[] s_defaultDefines = ["NET8_0", "NET8_0_OR_GREATER", "NET7_0_OR_GREATER", "NET6_0_OR_GREATER", "NET5_0_OR_GREATER", "NETCOREAPP3_1_OR_GREATER", "NETCOREAPP3_0_OR_GREATER", "NETCOREAPP", "NET", "WINDOWS", "WINDOWS7_0_OR_GREATER"]; //and no WINDOWS10_0_17763_0_OR_GREATER etc (see VS intellisense at #if Ctrl+Space)
	
	/// <summary>
	/// Meta option 'warningLevel'.
	/// Default: <see cref="DefaultWarningLevel"/> (default 6).
	/// </summary>
	public int WarningLevel { get; private set; }
	
	public const int DefaultWarningLevel = 6; //wave 7 adds 1 warning ("The type name only contains lower-cased ascii characters"), not useful
	
	/// <summary>
	/// Meta option 'noWarnings'.
	/// Default: <see cref="DefaultNoWarnings"/>.
	/// </summary>
	public List<string> NoWarnings { get; private set; }
	
	/// <summary>
	/// Gets or sets default meta option 'noWarnings' value. Initially CS1701,CS1702.
	/// </summary>
	public static string[] DefaultNoWarnings { get; set; } = ["CS1701", "CS1702"];
	//CS1701,CS1702: from default VS project properties.
	
	/// <summary>
	/// Meta 'testInternal'.
	/// Default: null.
	/// </summary>
	public string[] TestInternal { get; private set; }
	
	///// <summary>
	///// Meta option 'config'.
	///// </summary>
	//public FileNode ConfigFile { get; private set; }
	
	/// <summary>
	/// All meta errors of all files. Includes meta syntax errors, file 'not found' errors, exceptions.
	/// null if flag <b>ForCodeInfo</b> or <b>ForFindReferences</b>.
	/// </summary>
	public ErrBuilder Errors { get; private set; }
	
	/// <summary>
	/// Default references and unique references added through meta options 'r', 'com', 'nuget' and 'pr' in all C# files of this compilation.
	/// Use References.<see cref="MetaReferences.Refs"/>.
	/// </summary>
	public MetaReferences References { get; private set; }
	
	/// <summary>
	/// Project main files added through meta option 'pr'.
	/// null if none.
	/// </summary>
	public List<(FileNode f, MetaComments m)> ProjectReferences { get; private set; }
	
	///// <summary>
	///// Gets all unique project references of this and pr descendants.
	///// </summary>
	///// <returns></returns>
	//public List<(FileNode f, MetaComments m)> ProjectReferencesOfCompilation() {
	//	if (ProjectReferences == null || !ProjectReferences.Any(o => o.m.ProjectReferences != null)) return ProjectReferences;
	//	var a = new List<(FileNode f, MetaComments m)>(ProjectReferences);
	//	foreach (var v in ProjectReferences) {
	//		//undone
	//	}
	//	return a;
	//}
	
	/// <summary>
	/// Meta nuget, like @"-\PackageName".
	/// </summary>
	public List<(string package, string alias)> NugetPackages { get; private set; }
	
	/// <summary>
	/// If there are meta nuget, returns the root element of the auto-loaded XML file that contains a list of installed NuGet packages and their files. Else null.
	/// </summary>
	public XElement NugetXmlRoot => _xnuget;
	XElement _xnuget;
	
	/// <summary>
	/// Unique resource files added through meta option 'resource' in all C# files of this compilation.
	/// null if none.
	/// </summary>
	public List<MCFileAndString> Resources { get; private set; }
	
	/// <summary>
	/// Unique files added through meta option 'file' in all C# files of this compilation.
	/// null if none.
	/// </summary>
	public List<MCFileAndString> OtherFiles { get; private set; }
	
	/// <summary>
	/// Meta option 'preBuild'.
	/// </summary>
	public MCFileAndString PreBuild { get; private set; }
	
	/// <summary>
	/// Meta option 'postBuild'.
	/// </summary>
	public MCFileAndString PostBuild { get; private set; }
	
	/// <summary>
	/// Meta option 'ifRunning'.
	/// Default: warn_restart (warn and don't run, but restart if from editor).
	/// </summary>
	public MCIfRunning IfRunning { get; private set; }
	
	/// <summary>
	/// Meta option 'uac'.
	/// Default: inherit.
	/// </summary>
	public MCUac Uac { get; private set; }
	
	/// <summary>
	/// Meta option 'startFaster'.
	/// Default: false.
	/// </summary>
	public bool StartFaster { get; private set; }
	
	/// <summary>
	/// Meta option 'bit32'.
	/// Default: false.
	/// </summary>
	public bool Bit32 { get; private set; }
	
	/// <summary>
	/// Meta option 'console'.
	/// Default: false.
	/// </summary>
	public bool Console { get; private set; }
	
	/// <summary>
	/// Meta option 'icon'.
	/// </summary>
	public FileNode IconFile { get; private set; }
	
	/// <summary>
	/// Meta option 'manifest'.
	/// </summary>
	public FileNode ManifestFile { get; private set; }
	
	//rejected
	///// <summary>
	///// Meta option 'res'.
	///// </summary>
	//public FileNode ResFile { get; private set; }
	
	/// <summary>
	/// Meta option 'outputPath'.
	/// Default: null.
	/// </summary>
	public string OutputPath { get; private set; }
	
	/// <summary>
	/// Meta option 'role'.
	/// Default: miniProgram if script, else classFile.
	/// In WPF preview mode it's always miniProgram.
	/// </summary>
	public MCRole Role { get; private set; }
	
	/// <summary>
	/// Gets default meta option 'role' value. It is miniProgram if isScript, else classFile.
	/// </summary>
	public static MCRole DefaultRole(bool isScript) => isScript ? MCRole.miniProgram : MCRole.classFile;
	
	/// <summary>
	/// Same As <b>Role</b>, but unchanged in WPF preview mode.
	/// </summary>
	public MCRole UnchangedRole { get; private set; }
	
	/// <summary>
	/// Meta option 'sign'.
	/// </summary>
	public FileNode SignFile { get; private set; }
	
	/// <summary>
	/// Meta 'xmlDoc'.
	/// Default: false.
	/// </summary>
	public bool XmlDoc { get; private set; }
	
	public NullableContextOptions Nullable { get; private set; }
	
	/// <summary>
	/// Which options are specified.
	/// </summary>
	public MCSpecified Specified { get; private set; }
	
	/// <summary>
	/// If there is meta, gets character positions before the starting /*/ and after the ending /*/. Else default.
	/// </summary>
	public StartEnd MetaRange { get; private set; }
	
	/// <summary>
	/// Meta 'miscFlags'.
	/// <br/>• 1 - don't add XAML icons from code strings to resources.
	/// </summary>
	public int MiscFlags { get; private set; }
	
	readonly MCFlags _flags;
	readonly Dictionary<FileNode, string> _fileTextCache;
	
	/// <param name="fileTextCache">
	/// If not null, tries to get file text from it; if not found, loads and adds to it.
	/// Use to avoid loading text of same file many times when processing multiple files. Eg for each file need global.cs and its meta c.
	/// </param>
	public MetaComments(MCFlags flags, Dictionary<FileNode, string> fileTextCache = null) {
		_flags = flags;
		_fileTextCache = fileTextCache;
	}
	
	/// <summary>
	/// Extracts meta comments from all C# files of this compilation, including project files and files added through meta option 'c'.
	/// Call once.
	/// </summary>
	/// <returns>false if there are errors, except with flag <b>ForCodeInfo</b> or <b>ForFindReferences</b>. Then use <see cref="Errors"/>.</returns>
	/// <param name="f">Main C# file. If projFolder not null, must be the main file of the project.</param>
	/// <param name="projFolder">Project folder of the main file, or null if it is not in a project.</param>
	public bool Parse(FileNode f, FileNode projFolder) {
		if (!_flags.Has(MCFlags.ForCodeInfo)) Errors = new ErrBuilder();
		if (_flags.Has(MCFlags.Publish)) Optimize = true;
		
		if (_ParseFile(f, true, false)) {
			if (projFolder != null) {
				foreach (var ff in projFolder.EnumProjectClassFiles(f)) _ParseFile(ff, false, false);
			}
			
			//print.it(GlobalCount, CodeFiles);
			
			//define d:DEBUG_ONLY, r:RELEASE_ONLY
			for (int i = _defines.Count; --i >= 0;) {
				var s = _defines[i]; if (s.Length < 3 || s[1] != ':') continue;
				bool? del = s[0] switch { 'r' => !Optimize, 'd' => Optimize, _ => null };
				if (del == true) _defines.RemoveAt(i); else if (del == false) _defines[i] = s[2..];
			}
			
			if (!Optimize) {
				if (!_defines.Contains("DEBUG")) _defines.Add("DEBUG");
				if (!_defines.Contains("TRACE")) _defines.Add("TRACE");
			}
			//if(Role == MCRole.exeProgram && !_defines.Contains("EXE")) _defines.Add("EXE"); //rejected
			Defines = [.. s_defaultDefines, .. _defines];
			
			if (_flags.Has(MCFlags.ForFindReferences)) return true;
			
			_f = MainFile;
			_metaRange = MetaRange;
			_FinalCheckOptions();
		}
		
		if (Errors?.ErrorCount > 0) {
			if (_flags.Has(MCFlags.PrintErrors)) Errors.PrintAll();
			return false;
		}
		
		if (_flags.Has(MCFlags.Publish) && Role == MCRole.classLibrary) {
			OutputPath = folders.ThisAppTemp + @"publish\pr\" + Name;
		}
		
		return true;
	}
	
	/// <summary>
	/// Gets meta comments from a C# file and its meta c descendants.
	/// </summary>
	bool _ParseFile(FileNode f, bool isMain, bool isC, bool isGlobalSc = false) {
		if (!isMain && _CodeFilesContains(f)) return false;
		
		string code = null;
		if (_fileTextCache == null || !_fileTextCache.TryGetValue(f, out code)) {
			if (f.GetCurrentText(out code, silent: true).error is string es1) { Errors?.AddError(f, es1); return false; }
			_fileTextCache?.Add(f, code);
		}
		
		bool isScript = f.IsScript;
		var cf = new MCCodeFile(f, code, isMain, isC);
		
		if (isMain) {
			MainFile = cf;
			
			Name = pathname.getNameNoExt(f.Name);
			
			WarningLevel = DefaultWarningLevel;
			NoWarnings = DefaultNoWarnings != null ? new(DefaultNoWarnings) : new();
			_defines = new();
			Role = DefaultRole(isScript);
			
			CodeFiles = new();
			References = new();
			NugetPackages = new();
		}
		
		CodeFiles.Add(cf);
		int nc = CodeFiles.Count;
		var fPrev = _f; _f = cf;
		
		//add global.cs
		if (isMain) {
			var model = f.Model;
			var glob = model.Find("global.cs", FNFind.Class); //fast, uses dictionary
			if (glob != null) {
				if (glob == f) isGlobalSc = true;
				else _ParseFile(glob, false, true, isGlobalSc: true);
			} else if (!model.NoGlobalCs_) {
				model.NoGlobalCs_ = true;
				Panels.Output.Scintilla.AaTags.AddLinkTag("+restoreGlobal", _ => App.Model.AddMissingDefaultFiles(globalCs: true));
				if (model.FoundMultiple == null) print.warning("Missing class file \"global.cs\". <+restoreGlobal>Restore<>.", -1, "<>");
				else print.warning("Cannot use class file 'global.cs', because multiple exist.", -1);
			}
		}
		
		var meta = _metaRange = FindMetaComments(code);
		if (meta.end > 0) {
			if (isMain) MetaRange = meta;
			foreach (var t in EnumOptions(code, meta)) {
				//var p1 = perf.local();
				_ParseOption(t.Name, t.Value, t.nameStart, t.valueStart);
				//p1.Next(); var t1 = p1.TimeTotal; if(t1 > 5) print.it(t1, t.Name(), t.Value());
			}
		}
		
		if (isMain) {
			this.UnchangedRole = this.Role;
			if (_flags.Has(MCFlags.WpfPreview)) {
				this.Role = MCRole.miniProgram;
				this.IfRunning = MCIfRunning.run;
				_defines.Add("WPF_PREVIEW");
				this.Uac = default;
				//this.StartFaster = true;
				this.Bit32 = false;
				this.Console = false;
				this.Optimize = false;
				this.OutputPath = null;
				this.PreBuild = default;
				this.PostBuild = default;
				this.XmlDoc = false;
				this.Nullable = default;
			}
		}
		
		//let at first compile "global.cs" and meta c files. Why:
		//	1. If they have same classes etc or assembly/module attributes, it's better to show error in current file.
		//	2. If they have module initializers, it's better to call them first.
		if (isGlobalSc) {
			GlobalCount = CodeFiles.Count - (isMain ? 0 : 1);
		} else if (CodeFiles.Count > nc) {
			CodeFiles.RemoveAt(nc - 1);
			CodeFiles.Add(cf);
		}
		_f = fPrev;
		
		return true;
	}
	
	MCCodeFile _f; //current
	StartEnd _metaRange; //current
	
	void _ParseOption(string name, string value, int iName, int iValue) {
		if (name is null) return; //disabled
		
		//print.it(name, value);
		_nameFrom = iName; _nameTo = iName + name.Length;
		_valueFrom = iValue; _valueTo = iValue + value.Length;
		
		if (value.Length == 0) { _ErrorV("value cannot be empty"); return; }
		bool forCodeInfo = _flags.Has(MCFlags.ForCodeInfo),
			forFindRef = _flags.Has(MCFlags.ForFindReferences);
		
		switch (name) {
		case "r":
		case "com":
		case "pr":
		case "nuget":
			if (!MetaReferences.ParseAlias_(value, out var s1, out var alias, supportOldSyntax: true)) {
				_ErrorV("invalid string");
			} else if (name[0] == 'n') {
				_NuGet(s1, alias);
			} else {
				if (name[0] == 'p') {
					if (alias != null) { _ErrorV("pr alias not supported"); return; } //could support, but who will use it
					//Specified |= EMSpecified.pr;
					if (!_PR(ref s1) || forCodeInfo) return;
				}
				
				try {
					//var p1 = perf.local();
					if (!References.Resolve(s1, alias, name[0] == 'c', false)) {
						_ErrorV("reference assembly not found: " + s1); //TODO3: need more info, or link to Help
					}
					//p1.NW('r');
				}
				catch (Exception e) {
					_ErrorV("exception: " + e.Message); //unlikely. If bad format, will be error later, without position info.
				}
			}
			return;
		case "c":
			if (_GetFile(value, FNFind.Any) is FileNode ff) {
				if (ff.IsFolder) {
					foreach (var v in ff.Descendants()) if (v.IsClass) _ParseFile(v, false, true);
				} else {
					if (ff.IsClass) _ParseFile(ff, false, true);
					else _ErrorV("must be a class file");
				}
			}
			return;
		case "file":
			if (!forFindRef) {
				var fs1 = _GetFileAndString(value, FNFind.Any);
				if (!forCodeInfo && fs1.f != null) {
					OtherFiles ??= new();
					if (!OtherFiles.Exists(o => o == fs1)) OtherFiles.Add(fs1);
				}
			}
			return;
		case "miscFlags":
			if (!forCodeInfo) MiscFlags = value.ToInt();
			return;
		case "noRef" when _f.isMain:
			References.RemoveFromRefs(value);
			return;
		}
		if (_flags.Has(MCFlags.OnlyRef)) return;
		
		if (name is "resource") {
			if (!forFindRef) {
				//if (value.Ends(" /resources")) { //add following resources in value.resources instead of in AssemblyName.g.resources. //rejected. Rarely used. Would need more code, because meta resource can be in multiple files.
				//	if (!forCodeInfo) (Resources ??= new()).Add(new(null, value[..^11]));
				//	return;
				//}
				var fs1 = _GetFileAndString(value, FNFind.Any);
				if (!forCodeInfo && fs1.f != null) {
					Resources ??= new();
					if (!Resources.Exists(o => o == fs1)) Resources.Add(fs1);
				}
			}
			return;
		}
		
		if (!_f.isMain) {
			if (!forFindRef) {
				//In class files compiled as not main silently ignore all options if the first option is role other than class.
				//	It allows to test a class file without a test script etc.
				//	How: In meta define symbol X. Then #if X, enable executable code that uses the class.
				if (name is "role") {
					if (_f.allowAnyMeta_ = _Enum(out MCRole ro1, value) && ro1 != MCRole.classFile) return;
				} else if (_f.allowAnyMeta_) {
					if (name is "optimize" or "define" or "warningLevel" or "noWarnings" or "testInternal" or "preBuild" or "postBuild" or "outputPath" or "ifRunning" or "uac" or "bit32" or "console" or "manifest" or "icon" or "sign" or "xmlDoc") return;
					_ErrorN("unknown meta comment option");
				}
				
				_ErrorN($"in this file only these options can be used: r, pr, nuget, com, c, resource, file. Others only in the main file of the compilation - {MainFile.f.Name}. <help editor/Class files, projects>More info<>.");
			}
			return;
		}
		
		switch (name) {
		case "role":
			_Specified(MCSpecified.role);
			if (_Enum(out MCRole ro, value)) {
				Role = ro;
				if (MainFile.f.IsScript && (ro == MCRole.classFile || Role == MCRole.classLibrary)) _ErrorV("role classFile and classLibrary can be only in class files");
			}
			return;
		case "optimize":
			_Specified(MCSpecified.optimize);
			if (_TrueFalse(out bool optim, value) && !_flags.Has(MCFlags.Publish)) Optimize = optim;
			return;
		case "define":
			_Specified(MCSpecified.define);
			_defines.AddRange(value.Split_(','));
			return;
		case "testInternal":
			_Specified(MCSpecified.testInternal);
			TestInternal = value.Split_(',');
			return;
		case "sign":
			_Specified(MCSpecified.sign);
			SignFile = _GetFile(value, FNFind.File);
			return;
		}
		if (forFindRef) return;
		
		switch (name) {
		case "warningLevel":
			_Specified(MCSpecified.warningLevel);
			int wl = value.ToInt();
			if (wl >= 0 && wl <= 9999) WarningLevel = wl;
			else _ErrorV("must be 0 - 9999");
			break;
		case "noWarnings":
			_Specified(MCSpecified.noWarnings);
			NoWarnings.AddRange(value.Split_(','));
			break;
		case "preBuild":
			_Specified(MCSpecified.preBuild);
			PreBuild = _GetFileAndString(value, FNFind.CodeFile);
			break;
		case "postBuild":
			_Specified(MCSpecified.postBuild);
			PostBuild = _GetFileAndString(value, FNFind.CodeFile);
			break;
		case "outputPath":
			_Specified(MCSpecified.outputPath);
			if (!forCodeInfo) OutputPath = _GetOutPath(value);
			break;
		case "ifRunning":
			_Specified(MCSpecified.ifRunning);
			if (_Enum(out MCIfRunning ifR, value)) IfRunning = ifR;
			break;
		case "uac":
			_Specified(MCSpecified.uac);
			if (_Enum(out MCUac uac, value)) Uac = uac;
			break;
		case "startFaster": //undocumented. Likely will be removed in the future.
			_Specified(MCSpecified.startFaster);
			if (_TrueFalse(out bool startFaster, value)) StartFaster = startFaster;
			break;
		case "bit32":
			_Specified(MCSpecified.bit32);
			if (_TrueFalse(out bool is32, value)) Bit32 = is32;
			break;
		case "console":
			_Specified(MCSpecified.console);
			if (_TrueFalse(out bool con, value)) Console = con;
			break;
		case "manifest":
			_Specified(MCSpecified.manifest);
			ManifestFile = _GetFile(value, FNFind.File);
			break;
		case "icon":
			_Specified(MCSpecified.icon);
			IconFile = _GetFile(value, FNFind.Any);
			break;
		//case "resFile":
		//	_Specified(EMSpecified.resFile);
		//	ResFile = _GetFile(value);
		//	break;
		case "xmlDoc":
			_Specified(MCSpecified.xmlDoc);
			if (_TrueFalse(out bool xmlDOc, value)) XmlDoc = xmlDOc;
			break;
		case "nullable":
			_Specified(MCSpecified.nullable);
			if (_Enum(out NullableContextOptions nco, value)) Nullable = nco;
			break;
		default:
			_ErrorN("unknown meta comment option");
			break;
		}
	}
	
	#region util
	
	int _nameFrom, _nameTo, _valueFrom, _valueTo;
	
	bool _Error(string s, int from, int to) {
		if (Errors != null) {
			Errors.AddError(_f.f, _f.code, from, "error in meta: " + s);
		} else if (_flags.Has(MCFlags.ForCodeInfoInEditor) && _f.f == Panels.Editor.ActiveDoc.EFile) {
			CodeInfo._diag.AddMetaError(_metaRange, from, to, s);
		}
		return false;
	}
	
	/// <summary>Error in name.</summary>
	bool _ErrorN(string s) => _Error(s, _nameFrom, _nameTo);
	
	/// <summary>Error in value.</summary>
	bool _ErrorV(string s) => _Error(s, _valueFrom, _valueTo);
	
	/// <summary>Error in unknown place.</summary>
	bool _ErrorM(string s) => _Error(s, _metaRange.start, _metaRange.start + 3);
	
	void _Specified(MCSpecified what) {
		if (Specified.Has(what)) _ErrorN("this meta comment option is already specified");
		Specified |= what;
	}
	
	bool _TrueFalse(out bool b, string s) {
		b = false;
		switch (s) {
		case "true" or "!false": b = true; break;
		case "false" or "!true": break;
		default: return _ErrorV("must be true or false or !true or !false");
		}
		return true;
	}
	
	unsafe bool _Enum<T>(out T result, string s) where T : unmanaged, Enum {
		Debug.Assert(sizeof(T) == 4);
		bool R = _Enum2(typeof(T), out int v, s);
		result = Unsafe.As<int, T>(ref v);
		return R;
	}
	bool _Enum2(Type t, out int result, string s) {
		result = default;
		if (!s_enumCache.TryGetValue(t, out var r)) {
			var a = t.GetFields(BindingFlags.Public | BindingFlags.Static);
			int n = a.Length; foreach (var v in a) if (v.Name.Starts('_')) n--;
			r = new (string, int)[n];
			for (int i = 0, j = 0; i < a.Length; i++) {
				var sn = a[i].Name;
				if (!sn.Starts('_')) {
					if (char.IsUpper(sn[0])) sn = char.ToLowerInvariant(sn[0]) + sn[1..];
					r[j++] = (sn, (int)a[i].GetRawConstantValue());
				}
			}
			s_enumCache[t] = r;
		}
		foreach (var v in r) if (v.name == s) { result = v.value; return true; }
		return _ErrorV("must be one of:\n" + string.Join(", ", r.Select(o => o.name)));
	}
	static readonly Dictionary<Type, (string name, int value)[]> s_enumCache = new();
	
	FileNode _GetFile(string s, FNFind kind) {
		var f = _f.f.FindRelative(s, kind, orAnywhere: true);
		if (f == null) {
			if (App.Model.FoundMultiple != null) _ErrorV($"multiple '{s}' exist in this workspace. Use path, or rename a file.");
			else if (kind == FNFind.Folder) _ErrorV($"folder '{s}' does not exist in this workspace");
			else if (kind != FNFind.Any && null != _f.f.FindRelative(s, FNFind.File)) _ErrorV($"expected a {(kind == FNFind.Class ? "class" : "C#")} file");
			else _ErrorV($"file '{s}' does not exist in this workspace");
			return null;
		}
		int v = filesystem.exists(s = f.FilePath, true);
		if (v != (f.IsFolder ? 2 : 1)) { _ErrorV("file does not exist: " + s); return null; }
		return f;
	}
	
	public static FileNode FindFile(FileNode fRelativeTo, string metaValue, FNFind kind) {
		var f = fRelativeTo.FindRelative(metaValue, kind, orAnywhere: true);
		if (f != null) {
			int v = filesystem.exists(f.FilePath, true);
			if (v != (f.IsFolder ? 2 : 1)) return null;
		}
		return f;
	}
	
	MCFileAndString _GetFileAndString(string s, FNFind kind) {
		s = SplitArgs_(s, out var s2);
		
		//rejected
		//if (orFullPathAnywhere && pathname.isFullPathExpand(ref s)) {
		//	//rejected: support folders or wildcard. Users can add a link to the folder to the workspace, it is supported.
		//	if (!filesystem.exists(s).File) { _ErrorV("file does not exist: " + s); s = null; }
		//	return new(null, s2, s);
		//}
		
		return new(_GetFile(s, kind), s2);
	}
	
	internal static string SplitArgs_(string s, out string s2) {
		int i = s.Find(" /");
		if (i > 0) {
			s2 = s[(i + 2)..].NullIfEmpty_();
			s = s[..i];
		} else s2 = null;
		return s;
	}
	
	string _GetOutPath(string s) {
		s = s.TrimEnd('\\');
		if (!pathname.isFullPathExpand(ref s)) {
			if (s.Starts('%')) _ErrorV("relative path starts with %");
			if (s.Starts('\\')) s = _f.f.Model.FilesDirectory + s;
			else s = pathname.getDirectory(_f.f.FilePath, true) + s;
		}
		return pathname.Normalize_(s, noExpandEV: true);
	}
	
	bool _CodeFilesContains(FileNode f) {
		//return CodeFiles.Exists(o => o.f == f); //garbage
		var a = CodeFiles;
		for (int i = a.Count; --i >= 0;) if (a[i].f == f) return true;
		return false;
	}
	
	#endregion
	
	bool _PR(ref string value) {
		if (_GetFile(value, FNFind.Class) is not FileNode f) return false;
		if (f.FindProject(out var projFolder, out var projMain)) f = projMain;
		if (f == MainFile.f) return _ErrorV("circular reference");
		if (ProjectReferences is { } pr) foreach (var v in pr) if (v.f == f) return false;
		MetaComments m = null;
		if (!_flags.Has(MCFlags.ForCodeInfo)) {
			if (!Compiler.Compile(CCReason.CompileIfNeed, out var r, f, projFolder, needMeta: true, addMetaFlags: (_flags & MCFlags.Publish) | MCFlags.IsPR))
				return _ErrorV("failed to compile library");
			//print.it(r.role, r.file);
			if (r.role != MCRole.classLibrary) return _ErrorV("it is not a class library (no meta role classLibrary)");
			value = r.file;
			m = r.meta;
		}
		(ProjectReferences ??= new()).Add((f, m));
		return true;
	}
	
	void _NuGet(string value, string alias) {
		foreach (var v in NugetPackages) if (v.package.Eqi(value)) return;
		NugetPackages.Add((value, alias));
		if (_flags.Has(MCFlags.Publish) && !_flags.Has(MCFlags.IsPR)) return;
		try {
			_xnuget ??= XmlUtil.LoadElemIfExists(App.Model.NugetDirectoryBS + "nuget.xml");
			var xx = _xnuget?.Elem("package", "path", value, true);
			if (xx == null) {
				bool forCiErrors = _flags.Has(MCFlags.ForCodeInfoInEditor);
				var b = new StringBuilder(forCiErrors ? "<>" : null);
				b.Append("NuGet package not installed: ").Append(value);
				//append "install" link etc
				if (value.Split('\\', 2) is var a && a.Length == 2 && a[0].Length > 0 && a[1].Length > 0) {
					var sep = forCiErrors ? "\r\n" : "\r\n\t";
					b.Append(sep);
					//is installed in another folder?
					bool appended = false;
					if (_xnuget != null) {
						var s1 = "\\" + a[1];
						foreach (var v in _xnuget.Elements("package")) {
							if (v.Attr("path") is string s2 && s2.Ends(s1, true)) {
								s2 = s2[..^s1.Length];
								b.Append(!appended ? $"Replace code `nuget {value}` with" : $" or").Append($" `nuget {s2}{s1}`");
								appended = true;
							}
						}
						if (appended) b.Append(sep).Append($"Or <+nuget {value}>install or move<> {a[1]} to folder {a[0]}.");
					}
					if (!appended) b.Append($"<+nuget {value}>Install {a[1]}...<>");
				}
				_ErrorV(b.ToString());
				return;
			}
			var dir = App.Model.NugetDirectoryBS + pathname.getDirectory(value);
			foreach (var x in xx.Elements()) {
				if (x.Name.LocalName is not ("r" or "ro")) continue;
				var r = dir + x.Value;
				if (!References.Resolve(r, alias, false, true)) {
					_ErrorV("NuGet file not found: " + r);
				}
			}
		}
		catch (Exception e) {
			_ErrorV("exception: " + e.Message);
		}
	}
	
	bool _FinalCheckOptions() {
		//const MCSpecified c_spec1 = MCSpecified.ifRunning | MCSpecified.uac | MCSpecified.bit32 | MCSpecified.manifest | MCSpecified.icon | MCSpecified.console | MCSpecified.startFaster;
		//const string c_spec1S = "cannot use: ifRunning, uac, manifest, icon, console, bit32, startFaster";
		const MCSpecified c_spec1 = MCSpecified.ifRunning | MCSpecified.uac | MCSpecified.bit32 | MCSpecified.manifest | MCSpecified.icon | MCSpecified.console;
		const string c_spec1S = "cannot use: ifRunning, uac, manifest, icon, console, bit32";
		
		bool needOP = false;
		var role = UnchangedRole;
		switch (role) {
		case MCRole.miniProgram:
			if (Specified.HasAny(MCSpecified.outputPath | MCSpecified.manifest | MCSpecified.bit32 | MCSpecified.xmlDoc))
				return _ErrorM("with role miniProgram cannot use: outputPath, manifest, bit32, xmlDoc");
			break;
		case MCRole.exeProgram:
			//if (Specified.Has(MCSpecified.startFaster)) return _ErrorM("with role exeProgram cannot use: startFaster");
			needOP = true;
			break;
		case MCRole.editorExtension:
			if (Specified.HasAny(c_spec1 | MCSpecified.outputPath | MCSpecified.xmlDoc))
				return _ErrorM($"with role editorExtension {c_spec1S}, outputPath, xmlDoc");
			break;
		case MCRole.classLibrary:
			if (Specified.HasAny(c_spec1)) return _ErrorM("with role classLibrary " + c_spec1S);
			needOP = true;
			break;
		case MCRole.classFile:
			if (Specified != 0) return _ErrorM("with role classFile (default role of class files) can be used only c, com, nuget, r, resource, file");
			break;
		}
		if (needOP && !_flags.Has(MCFlags.WpfPreview)) OutputPath ??= GetDefaultOutputPath(_f.f, role, withEnvVar: false);
		
		if (IconFile?.IsFolder ?? false) if (role != MCRole.exeProgram) return _ErrorM("icon folder can be used only with role exeProgram"); //difficult to add multiple icons if miniProgram
		
		//if(ResFile != null) {
		//	if(IconFile != null) return _ErrorM("cannot add both res file and icon");
		//	if(ManifestFile != null) return _ErrorM("cannot add both res file and manifest");
		//}
		
		return true;
	}
	
	public static string GetDefaultOutputPath(FileNode f, MCRole role, bool withEnvVar) {
		Debug.Assert(role is MCRole.exeProgram or MCRole.classLibrary or MCRole.miniProgram);
		string r;
		if (role == MCRole.classLibrary) r = withEnvVar ? @"%folders.Workspace%\dll" : App.Model.DllDirectory;
		else r = (withEnvVar ? @"%folders.Workspace%\exe\" : App.Model.WorkspaceDirectory + @"\exe\") + f.DisplayName;
		return r;
	}
	
	public CSharpCompilationOptions CreateCompilationOptions() {
		OutputKind oKind = OutputKind.WindowsApplication;
		if (Role == MCRole.classLibrary || Role == MCRole.classFile) oKind = OutputKind.DynamicallyLinkedLibrary;
		else if (Console) oKind = OutputKind.ConsoleApplication;
		
		var r = new CSharpCompilationOptions(
			oKind,
			optimizationLevel: Optimize ? OptimizationLevel.Release : OptimizationLevel.Debug, //speed: compile the same, load Release slightly slower. Default Debug.
			allowUnsafe: true,
			platform: Bit32 ? Platform.AnyCpu32BitPreferred : Platform.AnyCpu,
			warningLevel: WarningLevel,
			specificDiagnosticOptions: NoWarnings?.Select(wa => new KeyValuePair<string, ReportDiagnostic>(wa[0].IsAsciiDigit() ? ("CS" + wa.PadLeft(4, '0')) : wa, ReportDiagnostic.Suppress)),
			cryptoKeyFile: SignFile?.FilePath, //also need strongNameProvider
			strongNameProvider: SignFile == null ? null : new DesktopStrongNameProvider(),
			nullableContextOptions: Nullable
		//,metadataImportOptions: TestInternal != null ? MetadataImportOptions.Internal : MetadataImportOptions.Public
		);
		
		//Allow to use internal/protected of assemblies specified using IgnoresAccessChecksToAttribute.
		//https://www.strathweb.com/2018/10/no-internalvisibleto-no-problem-bypassing-c-visibility-rules-with-roslyn/
		//This code (the above and below commented code) is for compiler. Also Compiler._AddAttributes adds attribute for run time.
		//if (TestInternal != null) {
		//	r = r.WithTopLevelBinderFlags(BinderFlags.IgnoreAccessibility);
		//}
		//But if using this code, code info has problems. Completion list contains internal/protected from all assemblies, and difficult to filter out. No signature info.
		//We instead modify Roslyn code and use class Au.Compiler.TestInternal. More info in project CompilerDlls here.
		
		//r = r.WithTopLevelBinderFlags(BinderFlags.SemanticModel); //should be used in editor? Tested a bit, it seems works the same.
		
		return r;
	}
	
	public CSharpParseOptions CreateParseOptions() {
		var docMode = DocumentationMode.None;
		if (_flags.Has(MCFlags.ForFindReferences)) docMode = DocumentationMode.Parse;
		else if (_flags.Has(MCFlags.ForCodeInfoInEditor) || XmlDoc) docMode = DocumentationMode.Diagnose;
		
		return new(LanguageVersion.Preview, docMode, SourceCodeKind.Regular, Defines);
	}
	
	/// <summary>
	/// Returns (start, end) of metacomments "/*/ ... /*/" at the start of code (before can be comments, empty lines, spaces, tabs). Returns default if no metacomments.
	/// </summary>
	/// <seealso cref="MetaCommentsParser"/>
	public static StartEnd FindMetaComments(RStr code) {
		for (int i = 0; i <= code.Length - 6; i++) {
			char c = code[i];
			if (c == '/') {
				c = code[++i];
				if (c == '*') {
					int j = code.IndexOf(++i, "*/");
					if (j < 0) break;
					if (code[i] == '/' && code[j - 1] == '/') return new(i - 2, j + 2);
					i = j + 1;
				} else if (c == '/') {
					i = code.IndexOf(i, '\n');
					if (i < 0) break;
				} else break;
			} else if (c is not ('\r' or '\n' or ' ' or '\t')) break;
		}
		return default;
	}
	
	/// <summary>
	/// Parses metacomments and returns offsets of all option names and values in code.
	/// </summary>
	/// <param name="code">Code that starts with metacomments "/*/ ... /*/".</param>
	/// <param name="meta">The range of metacomments, returned by <see cref="FindMetaComments"/>.</param>
	/// <seealso cref="MetaCommentsParser"/>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static IEnumerable<Token> EnumOptions(string code, StartEnd meta) {
		for (int i = meta.start + 3, iEnd = meta.end - 3; i < iEnd; i++) {
			Token t = default;
			for (; i < iEnd; i++) if (code[i] > ' ') break; //find next option
			if (i == iEnd) break;
			t.nameStart = i;
			if (i < iEnd && code[i] is '/') {
				t.nameEnd = i;
			} else {
				while (i < iEnd && code[i] is > ' ' and not ';') i++; //find separator after name
				t.nameEnd = i;
				while (i < iEnd && code[i] is ' ' or '\t') i++; //find value
			}
			t.valueStart = i;
			i = code.AsSpan(i, iEnd - i).IndexOfAny(';', '\n'); if (i < 0) i = iEnd; else i += t.valueStart; //find ; or newline after value
			int j = i; while (j > t.valueStart && code[j - 1] <= ' ') j--; //rtrim
			t.valueEnd = j;
			t.code = code;
			yield return t;
		}
	}
	
	/// <summary>
	/// <see cref="EnumOptions"/>.
	/// </summary>
	public struct Token {
		public int nameStart, nameEnd, valueStart, valueEnd;
		public string code;
		
		public string Name => nameEnd > nameStart ? code[nameStart..nameEnd] : null;
		public string Value => code[valueStart..valueEnd];
		public bool NameIs(string s) => code.Eq(nameStart..nameEnd, s);
		public bool ValueIs(string s) => code.Eq(valueStart..valueEnd, s);
		public bool IsDisabled => nameEnd == nameStart;
	}
}

/// <param name="f"></param>
/// <param name="code"></param>
/// <param name="isMain"></param>
/// <param name="isC">Added through meta 'c' or "global.cs".</param>
record class MCCodeFile(FileNode f, string code, bool isMain, bool isC) {
	internal bool allowAnyMeta_;
	public override string ToString() => f.ToString();
}

record struct MCFileAndString(FileNode f, string s);

enum MCRole { miniProgram, exeProgram, editorExtension, classLibrary, classFile }

enum MCUac { inherit, user, admin }

enum MCIfRunning { warn_restart, warn, cancel_restart, cancel, wait_restart, wait, run_restart, run, restart, end, end_restart, _norestartFlag = 1 }

/// <summary>
/// Flags for <see cref="MetaComments"/>
/// </summary>
[Flags]
enum MCFlags {
	/// <summary>
	/// Used for code info, not when compiling.
	/// Ignores meta such as run options (ifRunning etc) and non-code/reference files (resource etc).
	/// This flag is included in <b>ForCodeInfoInEditor</b> and <b>ForFindReferences</b>.
	/// </summary>
	ForCodeInfo = 1,
	
	/// <summary>
	/// Used for code info in editor.
	/// Includes <b>ForCodeInfo</b>.
	/// Same as <b>ForCodeInfo</b>; also adds some editor-specific stuff, like CodeInfo._diag.AddMetaError and DocumentationMode.Diagnose.
	/// </summary>
	ForCodeInfoInEditor = 2 | 1,
	
	/// <summary>
	/// Used by <see cref="CiProjects.GetSolutionForFindReferences"/>.
	/// Includes <b>ForCodeInfo</b>.
	/// </summary>
	ForFindReferences = 4 | 1,
	
	/// <summary>
	/// Call <see cref="ErrBuilder.PrintAll"/>.
	/// </summary>
	PrintErrors = 8,
	
	/// <summary>
	/// Need only references (r, pr, com, nuget) and file.
	/// </summary>
	OnlyRef = 16,
	
	/// <summary>
	/// Compiling for WPF preview.
	/// Defines WPF_PREVIEW and resets some meta.
	/// </summary>
	WpfPreview = 32,
	
	/// <summary>
	/// Used via meta pr of another project.
	/// </summary>
	IsPR = 64,
	
	/// <summary>
	/// Used for Publish.
	/// </summary>
	Publish = 128,
}

[Flags]
enum MCSpecified {
	ifRunning = 1,
	uac = 1 << 1,
	bit32 = 1 << 2,
	optimize = 1 << 3,
	define = 1 << 4,
	warningLevel = 1 << 5,
	noWarnings = 1 << 6,
	testInternal = 1 << 7,
	preBuild = 1 << 8,
	postBuild = 1 << 9,
	outputPath = 1 << 10,
	role = 1 << 11,
	icon = 1 << 12,
	manifest = 1 << 13,
	sign = 1 << 14,
	xmlDoc = 1 << 15,
	console = 1 << 16,
	nullable = 1 << 17,
	startFaster = 1 << 18,
}
