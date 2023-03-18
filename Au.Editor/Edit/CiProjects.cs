using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using SymAcc = Microsoft.CodeAnalysis.Accessibility;
using Au.Compiler;

class CiProjects {
	//public void Test() {
	
	//}
	
	/// <summary>
	/// From meta comments of all workspace files extracts pr and c.
	/// Slow, loads texts of all code files except those in excluded folders. At first saves everything.
	/// </summary>
	/// <returns>
	/// List containing <b>FileNode</b> of files that have valid pr or c, along with non-null <b>FileNode</b> arrays containing unique pr and c, including transitive c.
	/// All pr/c of a project are in a single list item where <b>f</b> is the project-main file.
	/// Random order if using parallel.
	/// </returns>
	static async Task<List<(FileNode f, FileNode[] pr, FileNode[] c, Dictionary<FileNode, FileNode> cPr)>> _GetAllMetaPrAndC() {
		var model = App.Model;
		model.Save.AllNowIfNeed();
		
		//excluded folders
		HashSet<FileNode> hsEF = new();
		if (!model.WSSett.ci_skipFolders.NE()) {
			foreach (var v in model.WSSett.ci_skipFolders.Lines(noEmpty: true)) {
				var f = model.Find(v, FNFind.Folder);
				if (f != null) hsEF.Add(f);
			}
		}
		
		//get file paths before loading async
		List<(FileNode f, string path)> a1 = new();
		FileNode projMain = null; //for all files in a project let f = project-main file
#if DEBUG
		//if(keys.isScrollLock) _Folder(model.Find("\\Deps", FNFind.Folder)); else
#endif
		_Folder(model.Root);
		
		void _Folder(FileNode parent) {
			for (var f = parent.FirstChild; f != null; f = f.Next) {
				if (f.IsCodeFile) {
					var path = f.FilePath;
					if (projMain != null && f.IsClass) a1.Add((projMain, path)); else a1.Add((f, path));
				} else if (f.IsFolder && !hsEF.Contains(f)) {
					bool isProj = projMain == null && f.IsProjectFolder(out projMain);
					_Folder(f);
					if (isProj) projMain = null;
				}
			}
		}
		
		//load texts of all files. Find meta pr and c, and add to d.
		var d = new Dictionary<FileNode, List<(int meta, string value)>>();
		await Task.Run(() => {
			if (App.Settings.find_parallel) {
				Parallel.For(0, a1.Count, i => _Meta(i, true));
			} else {
				for (int i = 0; i < a1.Count; i++) _Meta(i, false);
			}
			
			void _Meta(int i, bool parallel) {
				//rejected: optimize: load only n bytes from the start of the file. Tested, similar speed.
				if (FileNode.GetFileTextLL_(a1[i].path) is not string code || code.Length < 9) return;
				var meta = MetaComments.FindMetaComments(code); if (meta.end == 0) return;
				bool locked = false;
				foreach (var v in MetaComments.EnumOptions(code, meta)) {
					int m; if (v.NameIs("pr")) m = 1; else if (v.NameIs("c")) m = 2; else continue;
					if (!locked && parallel) { locked = true; Monitor.Enter(d); }
					if (d.TryGetValue(a1[i].f, out var a2)) a2.Add((m, v.Value)); else d.Add(a1[i].f, new() { (m, v.Value) });
				}
				if (locked) Monitor.Exit(d);
			}
		});
		
		//find pr/c files and convert to list ar
		List<(FileNode f, FileNode[] pr, FileNode[] c, Dictionary<FileNode, FileNode> cPr)> ar = new();
		List<FileNode> aPR = new(), aC = new();
		foreach (var (f, a) in d) {
			aPR.Clear(); aC.Clear();
			foreach (var v in a) {
				if (v.meta == 1) { //pr
					var p = MetaComments.FindFile(f, v.value, FNFind.Class)?.GetProjectMainOrThis();
					if (p != null && p != f && !aPR.Contains(p)) aPR.Add(p);
				} else { //c
					if (MetaComments.FindFile(f, v.value, FNFind.Any) is FileNode ff) {
						if (ff.IsFolder) foreach (var c in ff.Descendants()) _AddC(c); else _AddC(ff);
						void _AddC(FileNode c) { if (c.IsClass && c != f && !aC.Contains(c)) aC.Add(c); }
					}
				}
			}
			if (aPR.Any() || aC.Any()) {
				ar.Add((f, aPR.ToArray(), aC.ToArray(), null));
				//print.it(f, aPR, aC);
			}
		}
		
		//append transitive c and pr. Eg if file F1 contains c F2, and F2 contains c F3, add F3 to F1.
		//	yes: F1 -> c F2 -> c F3
		//	yes: F1 -> c F2 -> pr F3
		//	yes: F1 -> pr F2 -> c F3
		//	no: F1 -> pr F2 -> pr F3
		//	no: F1 -> pr F2 -> c F3 -> pr F4
		_Transitive();
		void _Transitive() {
			var td = ar.ToDictionary(o => o.f, o => (o.pr, o.c));
			List<FileNode> tap = new(), tac = new();
			foreach (ref var v in ar.AsSpan()) {
				tap.Clear(); tap.AddRange(v.pr);
				tac.Clear(); tac.AddRange(v.c);
				Dictionary<FileNode, FileNode> cPr = null;
				foreach (var f in v.pr) _AddTransitive(f, true, f);
				foreach (var f in v.c) _AddTransitive(f, false, null);
				if (tap.Count > v.pr.Length) v.pr = tap.ToArray();
				if (tac.Count > v.c.Length) v.c = tac.ToArray();
				v.cPr = cPr;
				
				void _AddTransitive(FileNode fu, bool noPr, FileNode fPr) {
					if (!td.TryGetValue(fu, out var t)) return;
					var (apr, ac) = t;
					if (!noPr) {
						foreach (var f in apr) {
							if (tap.Contains(f)) continue;
							tap.Add(f);
							_AddTransitive(f, true, f);
						}
					}
					foreach (var f in ac) {
						if (tac.Contains(f) || tap.Contains(f)) continue;
						tac.Add(f);
						if (fPr != null) (cPr ??= new()).Add(f, fPr);
						_AddTransitive(f, noPr, fPr);
					}
				}
			}
		}
		
		//foreach (var v in ar.OrderBy(o => o.f.ItemPath)) print.it(v.f.ItemPath, v.pr, v.c);
		//print.it("----");
		
		return ar;
	}
	
	/// <summary>
	/// Gets all files that use some files in <i>d</i> through meta pr or c.
	/// </summary>
	/// <returns>
	/// fUser - a user file. If in a project, it's the project-main file.
	/// pr - if fUser uses some files in <i>d</i> through meta pr, contains these projects. Else null.
	/// </returns>
	static async Task<List<(FileNode fUser, HashSet<FileNode> pr)>> _GetUsers(Dictionary<FileNode, Project> d, _FRContext x) {
		var a = await _GetAllMetaPrAndC();
		
		List<(FileNode fUser, HashSet<FileNode> pr)> ar = new();
		var defProjects = d
			.Where(o => o.Value.CompilationOptions.OutputKind == OutputKind.DynamicallyLinkedLibrary)
			.Select(o => FileOf(o.Value))
			.Distinct()
			.ToArray();
		//print.it(d.Keys, defProjects.Select(o => o.ItemPath));
		var skipC = x.sol.Projects.SelectMany(o => o.DocumentIds.Select(FileOf)).ToHashSet();
		foreach (var user in a) {
			HashSet<FileNode> pr = null; bool hasC = false;
			foreach (var f in defProjects)
				if (user.pr.Contains(f))
					if (!skipC.Contains(user.f))
						(pr ??= new()).Add(f);
			foreach (var f in d.Keys)
				if (user.c.Contains(f))
					if (!skipC.Contains(user.f)) {
						//if user uses this c through a pr, add that pr instead
						if (user.cPr != null && user.cPr.TryGetValue(f, out var cPr)) {
							if (!defProjects.Contains(cPr)) (pr ??= new()).Add(cPr);
						} else {
							hasC = true;
						}
					}
			
			if (pr != null || hasC) ar.Add((user.f, pr));
		}
		
		return ar;
	}
	
	/// <summary>
	/// Gets all files that use <i>sym</i>.
	/// If <i>sym</i> is defined in class file(s), calls other <b>_GetUsers</b> overload and passes them.
	/// If <i>sym</i> is defined in metadata or global.cs, and current file is a class file, calls that <b>_GetUsers</b> overload and passes current file.
	/// </summary>
	/// <returns>
	/// fUser - a user file. If in a project, it's the project-main file.
	/// pr - if fUser uses some files through meta pr, contains these projects. Else null.
	/// </returns>
	static async Task<List<(FileNode fUser, HashSet<FileNode> pr)>> _GetUsers(ISymbol sym, _FRContext x) {
		if (sym is IAliasSymbol alias) sym = alias.Target;
		//if (sym is not (INamespaceSymbol or INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IEventSymbol or IFieldSymbol)) return null;
		if (sym.DeclaredAccessibility is SymAcc.Private or SymAcc.NotApplicable) return null; //Internal and ProtectedAndInternal can be accessed through [InternalsVisible] or testInternal.
		Dictionary<FileNode, Project> d = new();
		HashSet<FileNode> globals = null;
		bool isInMetadata = false, isInGlobal = false;
		foreach (var loc in sym.Locations) {
			FileNode f; bool currentFile = false;
			if (loc.IsInSource) {
				f = FileOf(loc.SourceTree, x.sol);
				if (f.IsClass && (globals ??= _GetGlobals()).Contains(f)) {
					isInGlobal = currentFile = true;
					f = x.cd.sci.EFile;
				}
			} else {
				if (isInMetadata) continue;
				isInMetadata = currentFile = true;
				f = x.cd.sci.EFile;
				x.pem = loc.MetadataModule.GetMetadata().Module;
			}
			
			if (f.IsClass && !d.ContainsKey(f)) {
				var doc = currentFile
					? x.sol.GetDocument(x.sol.GetDocumentIdsWithFilePath(f.ItemPath).First())
					: x.sol.GetDocument(loc.SourceTree);
				d.Add(f, doc.Project);
			}
		}
		if (isInMetadata || isInGlobal) x.info = "Searched only in this and related files, not in entire workspace.";
		return d.Any() ? await _GetUsers(d, x) : null;
		
		//rejected: append link 'Search in entire workspace'.
		//	Can be very slow if many files. Now 3 s with 1000 1-file users.
		
		static HashSet<FileNode> _GetGlobals() {
			HashSet<FileNode> h = new();
			var g = App.Model.Find("global.cs", FNFind.Class);
			if (g != null) _File(g);
			void _File(FileNode f) {
				if (!h.Add(f)) return;
				if (FileNode.GetFileTextLL_(f.FilePath) is not string code || code.Length == 0) return;
				var meta = MetaComments.FindMetaComments(code); if (meta.end == 0) return;
				foreach (var v in MetaComments.EnumOptions(code, meta)) {
					if (v.NameIs("c")) {
						if (MetaComments.FindFile(f, v.Value, FNFind.Any) is FileNode ff) {
							if (ff.IsFolder) foreach (var c in ff.Descendants()) _File(c); else _File(ff);
						}
					}
				}
			}
			return h;
		}
	}
	
	public static async Task<(Solution solution, string info)> GetSolutionForFindReferences(ISymbol sym, CodeInfo.Context cd) {
		//print.clear();
		
		_FRContext x = new() { sol = cd.document.Project.Solution, cd = cd };
		var users = await _GetUsers(sym, x);
		
		if (users != null) {
			users.Sort((e1, e2) => (e1.pr?.Contains(e2.fUser) ?? false) ? 1 : (e2.pr?.Contains(e1.fUser) ?? false) ? -1 : 0);
			
			//print.it("USERS");
			//foreach (var v in users) {
			//	print.it(v.fUser, v.pr?.Select(o => o.Name));
			//}
			//never mind: if S -> C -> L, now adds C and S as users of L. Should not add C, because it's in S.
			
			//metadata references. Need only System.Runtime and 1 where the symbol is defined (if in metadata). Faster than with all default refs.
			var allmr = x.cd.document.Project.MetadataReferences;
			List<MetadataReference> mref = new() { allmr.First(o => (o as MetadataImageReference).Display == "System.Runtime") };
			if (x.pem != null && !x.pem.Name.Eqi("System.Runtime.dll"))
				mref.Add(allmr.First(o => (o as MetadataImageReference).GetMetadataNoCopy() is AssemblyMetadata amd && amd.GetModules().Select(o => o.Module).Contains(x.pem)));
			
			//TODO: need all pr and metadata refs that are used by current project.
			//	Now eg does not find aaaSetElementColor in Au.Editor when searching from Au.Controls, because parameter type ColorInt is unknown in Au.Editor.
			
			List<ProjectInfo> aProjectInfo = new();
			Dictionary<FileNode, ProjectId> dFileProj = x.sol.ProjectIds.ToDictionary(o => FileOf(o));
			Dictionary<FileNode, string> dTexts = new();
			foreach (var (f, pr) in users) {
				aProjectInfo.Add(_CreateProject(x, f, pr, dFileProj, mref, dTexts));
			}
			await Task.Run(() => {
				foreach (var pri in aProjectInfo) x.sol = x.sol.AddProject(pri);
			});
		}
		//print.it(x.sol.Projects.Count(), x.sol.Projects.Select(o => o.Name));
		return (x.sol, x.info);
	}
	
	static ProjectInfo _CreateProject(_FRContext x, FileNode f, HashSet<FileNode> pr, Dictionary<FileNode, ProjectId> dFileProj, List<MetadataReference> mref, Dictionary<FileNode, string> dTexts) {
		if (!f.FindProject(out var projFolder, out var fmain)) fmain = f;
		var m = new MetaComments(MCPFlags.ForFindReferences, dTexts);
		m.Parse(fmain, projFolder);
		
		if (m.TestInternal is string[] testInternal) TestInternal.RefsAdd(m.Name, testInternal);
		
		var projectId = ProjectId.CreateNewId();
		AttachFileOf(projectId, fmain);
		var adi = new List<DocumentInfo>();
		foreach (var mf in m.CodeFiles) {
			var docId = DocumentId.CreateNewId(projectId);
			AttachFileOf(docId, mf.f);
			var tav = TextAndVersion.Create(SourceText.From(mf.code, Encoding.UTF8), VersionStamp.Default);
			adi.Add(DocumentInfo.Create(docId, mf.f.Name, null, SourceCodeKind.Regular, TextLoader.From(tav), mf.f.ItemPath));
			//note: 'filePath=mf.f.ItemPath' is important. If same path used in multiple projects, symbolfinder finds symbols
			//	with same name in all projects, although technically it's not the same symbol. This is what we need.
		}
		
		//TODO
		//print.it(m.Name, x.cd.document.Project.ProjectReferences.Select(o => FileOf(o.ProjectId)), pr);
		
		var pri = ProjectInfo.Create(projectId, VersionStamp.Default, m.Name, m.Name, LanguageNames.CSharp, fmain.ItemPath, null,
			m.CreateCompilationOptions(),
			m.CreateParseOptions(),
			adi,
			pr?.Select(o => new ProjectReference(dFileProj[o])),
			//x.cd.document.Project.ProjectReferences,
			mref);
		
		if (fmain.IsClass) dFileProj.Add(fmain, projectId);
		return pri;
	}
	
	class _FRContext {
		public Solution sol;
		public CodeInfo.Context cd;
		public string info;
		public PEModule pem;
	}
	
	public static void AttachFileOf(DocumentId docId, FileNode f) { _cwtFO.Add(docId, f); }
	public static void AttachFileOf(ProjectId projId, FileNode f) { _cwtFO.Add(projId, f); }
	
	static readonly ConditionalWeakTable<object, FileNode> _cwtFO = new();
	
	public static FileNode FileOf(DocumentId id) => _cwtFO.TryGetValue(id, out var v) ? v : null;
	public static FileNode FileOf(Document doc) => FileOf(doc.Id);
	public static FileNode FileOf(SyntaxTree t, Solution sol) => FileOf(sol.GetDocument(t));
	public static FileNode FileOf(ProjectId id) => _cwtFO.TryGetValue(id, out var v) ? v : null;
	public static FileNode FileOf(Project proj) => FileOf(proj.Id);
}
