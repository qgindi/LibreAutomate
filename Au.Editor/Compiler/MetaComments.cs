using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Xml.Linq;

//CONSIDER: c https://..../file.cs
//	Problem: security. These files could contain malicious code.
//	It seems nuget supports source files, not only compiled assemblies: https://stackoverflow.com/questions/52880687/how-to-share-source-code-via-nuget-packages-for-use-in-net-core-projects

namespace Au.Compiler {
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
	/// When compiling multiple files (project, or using option 'c'), only the main file can contain all options. Other files can contain only 'r', 'c', 'resource', 'com', 'nuget'.
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
	/// <h3>Settings used when compiling</h3>
	/// <code><![CDATA[
	/// optimize false|true //if false (default), don't optimize code; this is known as "Debug configuration". If true, optimizes code; then low-level code is faster, but can be difficult to debug; this is known as "Release configuration".
	/// define SYMBOL1,SYMBOL2,d:DEBUG_ONLY,r:RELEASE_ONLY //define preprocessor symbols that can be used with #if etc. If no optimize true, DEBUG and TRACE are added implicitly.
	/// warningLevel 1 //compiler warning level, 0 (none) to 5 (all). Default: 5.
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
	/// ifRunning warn_restart|warn|cancel_restart|cancel|wait_restart|wait|run_restart|run|restart //what to do if this script is already running. Default: warn_restart. More info below.
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
	/// 
	/// About thread COM apartment type:
	/// For scripts it is STA, and cannot be changed.
	/// For apps it is STA if the Main function has [STAThread] attribute; or if role editorExtension. Else it is MTA.
	/// </example>
	class MetaComments {
		/// <summary>
		/// Name of the main C# file, without ".cs".
		/// </summary>
		public string Name { get; private set; }

		/// <summary>
		/// True if the main C# file is C# script, not regular C# file.
		/// </summary>
		public bool IsScript { get; private set; }

		/// <summary>
		/// Meta option 'optimize'.
		/// Default: <see cref="DefaultOptimize"/> (default false).
		/// </summary>
		public bool Optimize { get; private set; }

		/// <summary>
		/// Gets or sets default meta option 'optimize' value. Initially false.
		/// </summary>
		public static bool DefaultOptimize { get; set; }

		/// <summary>
		/// Meta option 'define'.
		/// </summary>
		public List<string> Defines { get; private set; }

		/// <summary>
		/// Meta option 'warningLevel'.
		/// Default: <see cref="DefaultWarningLevel"/> (default 5).
		/// </summary>
		public int WarningLevel { get; private set; }

		/// <summary>
		/// Gets or sets default meta option 'warningLevel' value. Initially 5.
		/// </summary>
		public static int DefaultWarningLevel { get; set; } = 5;

		/// <summary>
		/// Meta option 'noWarnings'.
		/// Default: <see cref="DefaultNoWarnings"/> (default null).
		/// </summary>
		public List<string> NoWarnings { get; private set; }

		/// <summary>
		/// Gets or sets default meta option 'noWarnings' value. Initially CS1701,CS1702.
		/// </summary>
		public static List<string> DefaultNoWarnings { get; set; } = new() { "CS1701", "CS1702" };
		//CS1701,CS1702: VS used to add 1701,1702 to default project properties. Now no, but it seems it implicitly disables these warnings. So we too.

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

		/// <summary>
		/// Meta nuget, like @"-\PackageName".
		/// </summary>
		public List<string> NugetPackages { get; private set; }

		/// <summary>
		/// If there are meta nuget, returns the root element of the auto-loaded XML file that contains a list of installed NuGet packages and their files. Else null.
		/// </summary>
		public XElement NugetXmlRoot => _xnuget;
		XElement _xnuget;

		/// <summary>
		/// All C# files of this compilation.
		/// The order is optimized for compilation and does not match the natural order:
		/// - If there is global.cs, it is the first, followed by its descendant meta c files, total <see cref="GlobalCount"/>.
		/// - Then main file, preceded by its descendant meta c files.
		/// - Then project files, each preceded by its descendant meta c files.
		/// </summary>
		public List<MetaCodeFile> CodeFiles { get; private set; }

		/// <summary>
		/// The compilation entry file. Probably not <c>CodeFiles[0]</c>.
		/// </summary>
		public MetaCodeFile MainFile { get; private set; }

		/// <summary>
		/// Count of global files, ie global.cs and its meta c descendants. They are at the start of <see cref="CodeFiles"/>.
		/// Note: if main file is a descendant of global.cs, it and its descendants are not included.
		/// </summary>
		public int GlobalCount { get; private set; }

		/// <summary>
		/// Unique resource files added through meta option 'resource' in all C# files of this compilation.
		/// null if none.
		/// </summary>
		public List<MetaFileAndString> Resources { get; private set; }

		/// <summary>
		/// Meta option 'preBuild'.
		/// </summary>
		public MetaFileAndString PreBuild { get; private set; }

		/// <summary>
		/// Meta option 'postBuild'.
		/// </summary>
		public MetaFileAndString PostBuild { get; private set; }

		/// <summary>
		/// Meta option 'ifRunning'.
		/// Default: warn_restart (warn and don't run, but restart if from editor).
		/// </summary>
		public EIfRunning IfRunning { get; private set; }

		/// <summary>
		/// Meta option 'uac'.
		/// Default: inherit.
		/// </summary>
		public EUac Uac { get; private set; }

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
		public ERole Role { get; private set; }

		/// <summary>
		/// Gets default meta option 'role' value. It is miniProgram if isScript, else classFile.
		/// </summary>
		public static ERole DefaultRole(bool isScript) => isScript ? ERole.miniProgram : ERole.classFile;

		/// <summary>
		/// Same As <b>Role</b>, but unchanged in WPF preview mode.
		/// </summary>
		public ERole UnchangedRole { get; private set; }

		/// <summary>
		/// Meta option 'sign'.
		/// </summary>
		public FileNode SignFile { get; private set; }

		/// <summary>
		/// Meta 'xmlDoc'.
		/// Default: false.
		/// </summary>
		public bool XmlDoc { get; private set; }

		/// <summary>
		/// Which options are specified.
		/// </summary>
		public EMSpecified Specified { get; private set; }

		/// <summary>
		/// If there is meta, gets character positions before the starting /*/ and after the ending /*/. Else default.
		/// </summary>
		public StartEnd MetaRange { get; private set; }

		EMPFlags _flags;

		/// <summary>
		/// Extracts meta comments from all C# files of this compilation, including project files and files added through meta option 'c'.
		/// Returns false if there are errors, except with flag ForCodeInfo. Then use <see cref="Errors"/>.
		/// </summary>
		/// <param name="f">Main C# file. If projFolder not null, must be the main file of the project.</param>
		/// <param name="projFolder">Project folder of the main file, or null if it is not in a project.</param>
		/// <param name="flags"></param>
		public bool Parse(FileNode f, FileNode projFolder, EMPFlags flags) {
			Debug.Assert(Errors == null); //cannot be called multiple times
			Errors = new ErrBuilder();
			_flags = flags;

			_ParseFile(f, true, false);

			if (projFolder != null) {
				foreach (var ff in projFolder.EnumProjectClassFiles(f)) _ParseFile(ff, false, false);
			}

			//print.it(GlobalCount, CodeFiles);

			//define d:DEBUG_ONLY, r:RELEASE_ONLY
			for (int i = Defines.Count; --i >= 0;) {
				var s = Defines[i]; if (s.Length < 3 || s[1] != ':') continue;
				bool? del = s[0] switch { 'r' => !Optimize, 'd' => Optimize, _ => null };
				if (del == true) Defines.RemoveAt(i); else if (del == false) Defines[i] = s[2..];
			}

			if (!Optimize) {
				if (!Defines.Contains("DEBUG")) Defines.Add("DEBUG");
				if (!Defines.Contains("TRACE")) Defines.Add("TRACE");
			}
			//if(Role == ERole.exeProgram && !Defines.Contains("EXE")) Defines.Add("EXE"); //rejected

			_FinalCheckOptions();

			if (Errors.ErrorCount > 0) {
				if (flags.Has(EMPFlags.PrintErrors)) Errors.PrintAll();
				return false;
			}
			return true;
		}

		/// <summary>
		/// Gets meta comments from a C# file and its meta c descendants.
		/// </summary>
		void _ParseFile(FileNode f, bool isMain, bool isC, bool isGlobalSc = false) {
			if (!isMain && _CodeFilesContains(f)) return;
			//var p1 = perf.local();
			string code = f.GetCurrentText();
			//p1.Next();
			bool isScript = f.IsScript;
			var cf = new MetaCodeFile(f, code, isMain, isC);

			if (isMain) {
				MainFile = cf;

				Name = pathname.getNameNoExt(f.Name);
				IsScript = isScript;

				Optimize = DefaultOptimize;
				WarningLevel = DefaultWarningLevel;
				NoWarnings = DefaultNoWarnings != null ? new List<string>(DefaultNoWarnings) : new List<string>();
				Defines = new();
				Role = DefaultRole(isScript);

				CodeFiles = new();
				References = new();
				NugetPackages = new();
			}

			CodeFiles.Add(cf);
			int nc = CodeFiles.Count;
			var fPrev = _f; _f = cf;

			if (isMain) { //add global.cs
				var model = f.Model;
				//var glob = model.Find(@"\Classes\global.cs", FNFind.Class) ?? model.Find("global.cs", FNFind.Class);
				var glob = model.Find("global.cs", FNFind.Class); //fast, uses dictionary
				if (glob != null) {
					if (glob == f) isGlobalSc = true;
					else _ParseFile(glob, false, true, isGlobalSc: true);
				} else if (!model.NoGlobalCs_) {
					model.NoGlobalCs_ = true;
					Panels.Output.ZOutput.ZTags.AddLinkTag("+restoreGlobal", _ => App.Model.AddMissingDefaultFiles(globalCs: true));
					if (model.FoundMultiple == null) print.warning("Missing class file \"global.cs\". <+restoreGlobal>Restore<>.", -1, "<>");
					else print.warning("Cannot use class file 'global.cs', because multiple exist.", -1);
				}
			}

			var meta = _metaRange = FindMetaComments(code);
			if (meta.end > 0) {
				if (isMain) MetaRange = meta;
				foreach (var t in EnumOptions(code, meta)) {
					//var p1 = perf.local();
					_ParseOption(t.Name(), t.Value(), t.nameStart, t.valueStart);
					//p1.Next(); var t1 = p1.TimeTotal; if(t1 > 5) print.it(t1, t.Name(), t.Value());
				}
			}
			//p1.NW();

			if (isMain) {
				this.UnchangedRole = this.Role;
				if (_flags.Has(EMPFlags.WpfPreview)) {
					this.Role = ERole.miniProgram;
					this.IfRunning = EIfRunning.run;
					this.Defines.Add("WPF_PREVIEW");
					this.Uac = default;
					this.Bit32 = false;
					this.Console = false;
					this.Optimize = false;
					this.OutputPath = null;
					this.PreBuild = default;
					this.PostBuild = default;
					this.XmlDoc = false;
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
		}

		MetaCodeFile _f; //current
		StartEnd _metaRange; //current

		void _ParseOption(string name, string value, int iName, int iValue) {
			//print.it(name, value);
			_nameFrom = iName; _nameTo = iName + name.Length;
			_valueFrom = iValue; _valueTo = iValue + value.Length;

			if (value.Length == 0) { _ErrorV("value cannot be empty"); return; }
			bool forCodeInfo = _flags.Has(EMPFlags.ForCodeInfo);

			switch (name) {
			case "r":
			case "com":
			case "pr" when _f.isMain:
				if (name[0] == 'p') {
					//Specified |= EMSpecified.pr;
					if (!_PR(ref value) || forCodeInfo) return;
				}

				try {
					//var p1 = perf.local();
					if (!References.Resolve(value, name[0] == 'c')) {
						_ErrorV("reference assembly not found: " + value); //FUTURE: need more info, or link to Help
					}
					//p1.NW('r');
				}
				catch (Exception e) {
					_ErrorV("exception: " + e.Message); //unlikely. If bad format, will be error later, without position info.
				}
				return;
			case "nuget":
				if (!NugetPackages.Contains(value, StringComparer.OrdinalIgnoreCase)) {
					NugetPackages.Add(value);
					try {
						_xnuget ??= XmlUtil.LoadElemIfExists(App.Model.NugetDirectoryBS + "nuget.xml");
						var xx = _xnuget?.Elem("package", "path", value, true);
						if (xx == null) {
							_ErrorV("nuget package not installed: " + value);
							return;
						}
						var dir = App.Model.NugetDirectoryBS + pathname.getDirectory(value);
						foreach (var x in xx.Elements()) {
							if (x.Name.LocalName is not ("r" or "ro")) continue;
							var r = dir + x.Value;
							if (!References.Resolve(r, false)) {
								_ErrorV("nuget file not found: " + r);
							}
						}
					}
					catch (Exception e) {
						_ErrorV("exception: " + e.Message);
					}
				}
				return;
			case "c":
				var ff = _GetFile(value, FNFind.Any);
				if (ff != null) {
					if (ff.IsFolder) {
						foreach (var v in ff.Descendants()) if (v.IsClass) _ParseFile(v, false, true);
					} else {
						if (ff.IsClass) _ParseFile(ff, false, true);
						else _ErrorV("must be a class file");
					}
				}
				return;
			}
			if (_flags.Has(EMPFlags.OnlyRef)) return;

			if (name == "resource") {
				//if (value.Ends(" /resources")) { //add following resources in value.resources instead of in AssemblyName.g.resources. //rejected. Rarely used. Would need more code, because meta resource can be in multiple files.
				//	if (!forCodeInfo) (Resources ??= new()).Add(new(null, value[..^11]));
				//} else
				{
					var fs1 = _GetFileAndString(value, FNFind.Any);
					if (!forCodeInfo && fs1.f != null) {
						Resources ??= new();
						if (!Resources.Exists(o => o.f == fs1.f && o.s == fs1.s)) Resources.Add(fs1);
					}
				}
				return;
			}

			if (!_f.isMain) {
				_ErrorN($"in this file only these options can be used: r, com, nuget, c, resource. Others only in the main file of the compilation - {MainFile.f.Name}.");
				return;
			}

			switch (name) {
			case "optimize":
				_Specified(EMSpecified.optimize);
				if (_TrueFalse(out bool optim, value)) Optimize = optim;
				break;
			case "define":
				_Specified(EMSpecified.define);
				Defines.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
				break;
			case "warningLevel":
				_Specified(EMSpecified.warningLevel);
				int wl = value.ToInt();
				if (wl >= 0 && wl <= 9999) WarningLevel = wl;
				else _ErrorV("must be 0 - 9999");
				break;
			case "noWarnings":
				_Specified(EMSpecified.noWarnings);
				NoWarnings.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
				break;
			case "testInternal":
				_Specified(EMSpecified.testInternal);
				TestInternal = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				break;
			case "role":
				_Specified(EMSpecified.role);
				if (_Enum(out ERole ro, value)) {
					Role = ro;
					if (IsScript && (ro == ERole.classFile || Role == ERole.classLibrary)) _ErrorV("role classFile and classLibrary can be only in class files");
				}
				break;
			case "preBuild":
				_Specified(EMSpecified.preBuild);
				PreBuild = _GetFileAndString(value, FNFind.CodeFile);
				break;
			case "postBuild":
				_Specified(EMSpecified.postBuild);
				PostBuild = _GetFileAndString(value, FNFind.CodeFile);
				break;
			case "outputPath":
				_Specified(EMSpecified.outputPath);
				if (!forCodeInfo) OutputPath = _GetOutPath(value);
				break;
			case "ifRunning":
				_Specified(EMSpecified.ifRunning);
				if (_Enum(out EIfRunning ifR, value)) IfRunning = ifR;
				break;
			case "uac":
				_Specified(EMSpecified.uac);
				if (_Enum(out EUac uac, value)) Uac = uac;
				break;
			case "bit32":
				_Specified(EMSpecified.bit32);
				if (_TrueFalse(out bool is32, value)) Bit32 = is32;
				break;
			case "console":
				_Specified(EMSpecified.console);
				if (_TrueFalse(out bool con, value)) Console = con;
				break;
			//case "config":
			//	_Specified(EMSpecified.config);
			//	ConfigFile = _GetFile(value);
			//	break;
			case "manifest":
				_Specified(EMSpecified.manifest);
				ManifestFile = _GetFile(value, FNFind.File);
				break;
			case "icon":
				_Specified(EMSpecified.icon);
				IconFile = _GetFile(value, FNFind.Any);
				break;
			//case "resFile":
			//	_Specified(EMSpecified.resFile);
			//	ResFile = _GetFile(value);
			//	break;
			case "sign":
				_Specified(EMSpecified.sign);
				SignFile = _GetFile(value, FNFind.File);
				break;
			case "xmlDoc":
				_Specified(EMSpecified.xmlDoc);
				if (_TrueFalse(out bool xmlDOc, value)) XmlDoc = xmlDOc;
				break;
			//case "version": //will be auto-created from [assembly: AssemblyVersion] etc
			//	break;
			default:
				_ErrorN("unknown meta comment option");
				break;
			}
		}

		#region util

		int _nameFrom, _nameTo, _valueFrom, _valueTo;

		bool _Error(string s, int from, int to) {
			if (!_flags.Has(EMPFlags.ForCodeInfo)) {
				Errors.AddError(_f.f, _f.code, from, "error in meta: " + s);
			} else if (_flags.Has(EMPFlags.ForCodeInfoInEditor) && _f.f == Panels.Editor.ZActiveDoc.EFile) {
				CodeInfo._diag.AddMetaError(_metaRange, from, to, s);
			}
			return false;
		}

		bool _ErrorN(string s) => _Error(s, _nameFrom, _nameTo);

		bool _ErrorV(string s) => _Error(s, _valueFrom, _valueTo);

		bool _ErrorM(string s) => _Error(s, 0, 3);

		void _Specified(EMSpecified what) {
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
					if (!sn.Starts('_')) r[j++] = (sn, (int)a[i].GetRawConstantValue());
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
				//if (kind != FNFind.Any && null != _f.f.FindRelative(s)) _ErrorV($"file '{s}' is of wrong type"); else //unlikely
				_ErrorV($"file '{s}' does not exist in this workspace");
				return null;
			}
			int v = filesystem.exists(s = f.FilePath, true);
			if (v != (f.IsFolder ? 2 : 1)) { _ErrorV("file does not exist: " + s); return null; }
			return f;
		}

		MetaFileAndString _GetFileAndString(string s, FNFind kind) {
			string s2 = null;
			int i = s.Find(" /");
			if (i > 0) {
				s2 = s[(i + 2)..];
				s = s[..i];
			}
			return new(_GetFile(s, kind), s2);
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
			var f = _GetFile(value, FNFind.CodeFile); if (f == null) return false;
			if (f.FindProject(out var projFolder, out var projMain)) f = projMain;
			if (f == MainFile.f) return _ErrorV("circular reference");
			MetaComments m = null;
			if (!_flags.Has(EMPFlags.ForCodeInfo)) {
				if (!Compiler.Compile(ECompReason.CompileIfNeed, out var r, f, projFolder, needMeta: true))
					return _ErrorV("failed to compile library");
				//print.it(r.role, r.file);
				if (r.role != ERole.classLibrary) return _ErrorV("it is not a class library (no meta role classLibrary)");
				value = r.file;
				m = r.meta;
			}
			(ProjectReferences ??= new()).Add((f, m));
			return true;
		}

		bool _FinalCheckOptions() {
			_f = MainFile;

			const EMSpecified c_spec1 = EMSpecified.ifRunning | EMSpecified.uac | EMSpecified.bit32 | EMSpecified.manifest | EMSpecified.icon | EMSpecified.console;
			const string c_spec1S = "cannot use: ifRunning, uac, manifest, icon, console, bit32";

			bool needOP = false;
			var role = UnchangedRole;
			switch (role) {
			case ERole.miniProgram:
				if (Specified.HasAny(EMSpecified.outputPath | EMSpecified.manifest | EMSpecified.bit32 | EMSpecified.xmlDoc)) return _ErrorM("with role miniProgram cannot use: outputPath, manifest, bit32, xmlDoc");
				break;
			case ERole.exeProgram:
				needOP = true;
				break;
			case ERole.editorExtension:
				if (Specified.HasAny(c_spec1 | EMSpecified.outputPath | EMSpecified.xmlDoc)) return _ErrorM($"with role editorExtension {c_spec1S}, outputPath, xmlDoc");
				break;
			case ERole.classLibrary:
				if (Specified.HasAny(c_spec1)) return _ErrorM("with role classLibrary " + c_spec1S);
				needOP = true;
				break;
			case ERole.classFile:
				if (Specified != 0) return _ErrorM("with role classFile (default role of class files) can be used only c, com, nuget, r, resource");
				break;
			}
			if (needOP && !_flags.Has(EMPFlags.WpfPreview)) OutputPath ??= GetDefaultOutputPath(_f.f, role, withEnvVar: false);

			if (IconFile?.IsFolder ?? false) if (role != ERole.exeProgram) return _ErrorM("icon folder can be used only with role exeProgram"); //difficult to add multiple icons if miniProgram

			//if(ResFile != null) {
			//	if(IconFile != null) return _ErrorM("cannot add both res file and icon");
			//	if(ManifestFile != null) return _ErrorM("cannot add both res file and manifest");
			//}

			return true;
		}

		public static string GetDefaultOutputPath(FileNode f, ERole role, bool withEnvVar) {
			Debug.Assert(role == ERole.exeProgram || role == ERole.classLibrary);
			string r;
			if (role == ERole.classLibrary) r = withEnvVar ? @"%folders.Workspace%\dll" : App.Model.DllDirectory;
			else r = (withEnvVar ? @"%folders.Workspace%\exe\" : App.Model.WorkspaceDirectory + @"\exe\") + f.DisplayName;
			return r;
		}

		public CSharpCompilationOptions CreateCompilationOptions() {
			OutputKind oKind = OutputKind.WindowsApplication;
			if (Role == ERole.classLibrary || Role == ERole.classFile) oKind = OutputKind.DynamicallyLinkedLibrary;
			else if (Console) oKind = OutputKind.ConsoleApplication;

			var r = new CSharpCompilationOptions(
			   oKind,
			   optimizationLevel: Optimize ? OptimizationLevel.Release : OptimizationLevel.Debug, //speed: compile the same, load Release slightly slower. Default Debug.
			   allowUnsafe: true,
			   platform: Bit32 ? Platform.AnyCpu32BitPreferred : Platform.AnyCpu,
			   warningLevel: WarningLevel,
			   specificDiagnosticOptions: NoWarnings?.Select(wa => new KeyValuePair<string, ReportDiagnostic>(wa[0].IsAsciiDigit() ? ("CS" + wa.PadLeft(4, '0')) : wa, ReportDiagnostic.Suppress)),
			   cryptoKeyFile: SignFile?.FilePath, //also need strongNameProvider
			   strongNameProvider: SignFile == null ? null : new DesktopStrongNameProvider()
			   //,metadataImportOptions: TestInternal != null ? MetadataImportOptions.Internal : MetadataImportOptions.Public
			   );

			//Allow to use internal/protected of assemblies specified using IgnoresAccessChecksToAttribute.
			//https://www.strathweb.com/2018/10/no-internalvisibleto-no-problem-bypassing-c-visibility-rules-with-roslyn/
			//This code (the above and below commented code) is for compiler. Also Compiler._AddAttributes adds attribute for run time.
			//if (TestInternal != null) {
			//	r = r.WithTopLevelBinderFlags(BinderFlags.IgnoreAccessibility);
			//}
			//But if using this code, code info has problems. Completion list contains internal/protected from all assemblies, and difficult to filter out. No signature info.
			//We instead modify Roslyn code in 2 places. Look in project CompilerDlls here. Also add class Au.Compiler.InternalsVisible and use it in CodeInfo._CreateWorkspace and Compiler._Compile.

			//r = r.WithTopLevelBinderFlags(BinderFlags.SemanticModel); //should be used in editor? Tested a bit, it seems works the same.

			return r;
		}

		public CSharpParseOptions CreateParseOptions() {
			return new(LanguageVersion.Preview,
				_flags.Has(EMPFlags.ForCodeInfo) ? DocumentationMode.Diagnose : (XmlDoc ? DocumentationMode.Parse : DocumentationMode.None),
				SourceCodeKind.Regular,
				Defines);
		}

		/// <summary>
		/// Returns (start, end) of metacomments "/*/ ... /*/" at the start of code (before can be comments, empty lines, spaces, tabs). Returns default if no metacomments.
		/// </summary>
		/// <param name="code">Code. Can be null.</param>
		public static StartEnd FindMetaComments(string code) {
			if (code != null) {
				for (int i = 0; i <= code.Length - 6; i++) {
					char c = code[i];
					if (c == '/') {
						c = code[++i];
						if (c == '*') {
							int j = code.Find("*/", ++i);
							if (j < 0) break;
							if (code[i] == '/' && code[j - 1] == '/') return new(i - 2, j + 2);
							i = j + 1;
						} else if (c == '/') {
							i = code.IndexOf('\n', i);
							if (i < 0) break;
						} else break;
					} else if (!(c == '\r' || c == '\n' || c == ' ' || c == '\t')) break;
				}
			}
			return default;
		}

		/// <summary>
		/// Parses metacomments and returns offsets of all option names and values in code.
		/// </summary>
		/// <param name="code">Code that starts with metacomments "/*/ ... /*/".</param>
		/// <param name="meta">The range of metacomments, returned by <see cref="FindMetaComments"/>.</param>
		public static IEnumerable<Token> EnumOptions(string code, StartEnd meta) {
			for (int i = meta.start + 3, iEnd = meta.end - 3; i < iEnd; i++) {
				Token t = default;
				for (; i < iEnd; i++) if (code[i] > ' ') break; //find next option
				if (i == iEnd) break;
				t.nameStart = i;
				while (i < iEnd && code[i] > ' ') i++; //find separator after name
				t.nameLen = i - t.nameStart;
				while (i < iEnd && code[i] <= ' ') i++; //find value
				t.valueStart = i;
				for (; i < iEnd; i++) if (code[i] == ';') break; //find ; after value
				int j = i; while (j > t.valueStart && code[j - 1] <= ' ') j--; //rtrim
				t.valueLen = j - t.valueStart;
				t.code = code;
				yield return t;
			}
		}

		/// <summary>
		/// <see cref="EnumOptions"/>.
		/// </summary>
		public struct Token {
			public int nameStart, nameLen, valueStart, valueLen;
			public string code;

			public string Name() => code.Substring(nameStart, nameLen);
			public string Value() => code.Substring(valueStart, valueLen);
			public bool NameIs(string s) => s.Length == nameLen && code.Eq(nameStart, s);
			public bool ValueIs(string s) => s.Length == valueLen && code.Eq(valueStart, s);
		}
	}

	struct MetaCodeFile {
		public FileNode f;
		public string code;
		public bool isMain;

		/// <summary>Added through meta option 'c' or "global.cs".</summary>
		public bool isC;

		public MetaCodeFile(FileNode f, string code, bool isMain, bool isC) { this.f = f; this.code = code; this.isMain = isMain; this.isC = isC; }

		public override string ToString() => f.ToString();
	}

	record struct MetaFileAndString(FileNode f, string s);

	enum ERole { miniProgram, exeProgram, editorExtension, classLibrary, classFile }

	enum EUac { inherit, user, admin }

	enum EIfRunning { warn_restart, warn, cancel_restart, cancel, wait_restart, wait, run_restart, run, restart, _norestartFlag = 1 }

	/// <summary>
	/// Flags for <see cref="MetaComments.Parse"/>
	/// </summary>
	[Flags]
	enum EMPFlags {
		/// <summary>
		/// Call <see cref="ErrBuilder.PrintAll"/>.
		/// </summary>
		PrintErrors = 1,

		/// <summary>
		/// Used for code info, not when compiling.
		/// Ignores meta such as run options (ifRunning etc) and non-code/reference files (resource etc).
		/// </summary>
		ForCodeInfo = 2,

		/// <summary>
		/// Need only references (r, pr, com, nuget).
		/// </summary>
		OnlyRef = 4,

		/// <summary>
		/// Used for code info in editor. Includes ForCodeInfo.
		/// Same as ForCodeInfo; also adds some editor-specific stuff, like CodeInfo._diag.AddMetaError.
		/// </summary>
		ForCodeInfoInEditor = 2 | 8,

		/// <summary>
		/// Compiling for WPF preview.
		/// Defines WPF_PREVIEW and resets some meta.
		/// </summary>
		WpfPreview = 16,

		///// <summary>
		///// Used for file Properties dialog etc, not when compiling.
		///// </summary>
		//ForFileProperties = ,
	}

	[Flags]
	enum EMSpecified {
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
	}
}
