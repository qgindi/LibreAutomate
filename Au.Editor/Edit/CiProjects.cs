
using Au.Compiler;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using SymAcc = Microsoft.CodeAnalysis.Accessibility;

class CiProjects {
	public void Test() {
		
	}
	
	/// <summary>
	/// From meta comments of all workspace files extracts pr and c.
	/// Slow, loads texts of all code files except those in excluded folders. At first saves everything.
	/// </summary>
	/// <returns>
	/// List containing <b>FileNode</b> of files that have valid pr or c, along with non-null <b>FileNode</b> arrays containing unique pr and c, including transitive c.
	/// All pr/c of a project are in a single list item where <b>f</b> is the project-main file.
	/// Random order if using parallel.
	/// </returns>
	static List<(FileNode f, FileNode[] pr, FileNode[] c)> _GetAllMetaPrAndC() { //TODO: make async?
		var model = App.Model;
		model.Save.AllNowIfNeed();
		
		//excluded folders
		HashSet<FileNode> hsEF = new();
		if (!model.WSSett.ci_exclude.NE()) {
			foreach (var v in model.WSSett.ci_exclude.Lines(noEmpty: true)) {
				var f = model.Find(v, FNFind.Folder);//TODO: test warnings
				if (f != null) hsEF.Add(f);
			}
		}
		
		//get file paths before loading in parallel. Skip excluded folders.
		List<(FileNode f, string path)> a1 = new();
		FileNode projMain = null; //for all files in a project let f = project-main file
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
		var d = new Dictionary<FileNode, List<(int index, int meta, string value)>>();
		if (App.Settings.find_parallel) {
			Parallel.For(0, a1.Count, i => _Meta(i, true));
		} else {
			for (int i = 0; i < a1.Count; i++) _Meta(i, false);
		}
		
		void _Meta(int i, bool parallel) {
			//CONSIDER: optimize: load UTF-8 into a thread-local buffer and find meta comments. If found, convert that part to UTF-16 string. Except if has a UTF-16 BOM.
			//TODO: load max 100 KB from the file. And allow files of any size.
			if (FileNode.GetFileTextLL_(a1[i].path) is not string code || code.Length == 0) return;
			var u = MetaComments.FindMetaComments(code);
			if (u.end == 0) return;
			bool once = false;
			foreach (var v in MetaComments.EnumOptions(code, u)) {
				int m; if (v.NameIs("pr")) m = 1; else if (v.NameIs("c")) m = 2; else continue;
				if (!once) { once = true; if (parallel) Monitor.Enter(d); }
				if (d.TryGetValue(a1[i].f, out var a2)) a2.Add((i, m, v.Value())); else d.Add(a1[i].f, new() { (i, m, v.Value()) });
			}
			if (once && parallel) Monitor.Exit(d);
		}
		
		//find pr/c files and convert to list ar
		List<(FileNode f, FileNode[] pr, FileNode[] c)> ar = new();
		List<FileNode> aPR = new(), aC = new();
		foreach (var (f, a) in d) {
			//foreach (var (f, a) in d.OrderBy(o => o.Value[0].index)) { //TODO: maybe don't need to sort here. If don't need, remove index from the tuple.
			aPR.Clear(); aC.Clear();
			foreach (var v in a) {
				var fr = MetaComments.FindFile(f, v.value, FNFind.Class);
				if (fr == null) continue;
				var aa = v.meta == 1 ? aPR : aC;
				if (!aa.Contains(fr) && fr != f) aa.Add(fr);
			}
			if (aPR.Any() || aC.Any()) {
				ar.Add((f, aPR.ToArray(), aC.ToArray()));
				//print.it(f, aPR, aC);
			}
		}
		
		//append transitive c. Eg if file F1 contains c F2, and F2 contains C F3, add F3 to F1.
		var td = ar.Where(o => o.c.Length > 0).ToDictionary(o => o.f, o => o.c);
		List<FileNode> ta = new();
		foreach (ref var v in ar.AsSpan()) {
			if (v.c.Length == 0) continue;
			ta.Clear(); ta.AddRange(v.c);
			foreach (var fc in v.c) if (td.TryGetValue(fc, out var ac)) _AddTransitive(ac);
			if (ta.Count > v.c.Length) v.c = ta.ToArray();
			
			void _AddTransitive(FileNode[] ac) {
				foreach (var fc in ac) {
					if (ta.Contains(fc)) continue;
					ta.Add(fc);
					if (td.TryGetValue(fc, out var ac2)) _AddTransitive(ac2);
				}
			}
		}
		
		return ar;
	}
	
	/// <summary>
	/// Gets all files that use some files in <i>af</i> through meta pr or c.
	/// </summary>
	/// <returns>
	/// fUser - a user file. If in a project, it's the project-main file.
	/// pr - if fUser uses some files in <i>af</i> through meta pr, contains these projects. Else null.
	/// </returns>
	static List<(FileNode fUser, List<Project> pr)> _GetUsers(IEnumerable<FileNode> af, Solution sol) {
		var a = _GetAllMetaPrAndC();
		List<(FileNode fUser, List<Project> pr)> ar = new();
		var defProjects = af.Select(o => o.GetProjectMainOrThis()).Distinct().Where(o => o.GetClassFileRole() == FNClassFileRole.Library).ToArray();
		var skipPr = sol.Projects.Select(FileOf).ToArray();
		var skipC = sol.Projects.SelectMany(o => o.DocumentIds.Select(FileOf)).ToArray();
		foreach (var user in a) {
			List<Project> pr = null; bool hasC = false;
			foreach (var f in defProjects) if (user.pr.Contains(f) && !skipPr.Contains(user.f)) (pr ??= new()).Add(sol.Projects.First(p => f == FileOf(p)));
			foreach (var f in af) if (hasC = user.c.Contains(f) && !skipC.Contains(user.f)) break;
			if (pr != null || hasC) ar.Add((user.f, pr));
		}
		return ar;
		//TODO: if f is global.cs or as c in it...
		//	Let behave like when sym is defined in metadata: search only in current project and related projects (pr in both ways). In results add a link to search in entire workspace.
	}
	
	/// <summary>
	/// Gets all files that use <i>sym</i>.
	/// If <i>sym</i> is defined in class file(s), calls <see cref="_GetUsers(IEnumerable{FileNode}, Solution)"/>.
	/// Else TODO
	/// </summary>
	/// <returns>
	/// fUser - a user file. If in a project, it's the project-main file.
	/// pr - if fUser uses some files through meta pr, contains these projects. Else null.
	/// </returns>
	static List<(FileNode fUser, List<Project> pr)> _GetUsers(ISymbol sym, Solution sol) {
		if (sym is IAliasSymbol alias) sym = alias.Target;
		//if (sym is not (INamespaceSymbol or INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IEventSymbol or IFieldSymbol)) return null;
		if (sym.DeclaredAccessibility is SymAcc.Private or SymAcc.NotApplicable) return null; //Internal and ProtectedAndInternal can be accessed through [InternalsVisible] or testInternal.
		if (sym.IsInSource()) {
			HashSet<FileNode> hs = new();
			foreach (var loc in sym.Locations) {
				if (loc.IsInSource) {
					var f = FileOf(loc.SourceTree, sol);
					if (f.IsClass) hs.Add(f);
				}
			}
			if (hs.Any()) return _GetUsers(hs, sol);
		} else {
			
		}
		
		return null;
	}
	
	public static Solution GetSolutionForFindReferences(ISymbol sym, CodeInfo.Context cd) {
		var sol = cd.document.Project.Solution;
		var users = _GetUsers(sym, sol);
		
		//if (users != null) {
		//	print.clear();
		//	print.it("USERS");
		//	foreach (var v in users) {
		//		print.it(v.fUser, v.pr?.Select(o => o.Name), v.c);
		//	}
		//}
		
		if (!users.NE_()) {
			foreach (var (fu, pr) in users) {
				if (!fu.FindProject(out var projFolder, out var fmain)) fmain = fu;
				var m = new MetaComments();
				m.Parse(fmain, projFolder, MCPFlags.ForCodeInfo);
				
				//if (m.TestInternal is string[] testInternal) InternalsVisible.Add(f.Name, testInternal);//TODO
				
				var projectId = ProjectId.CreateNewId();
				AttachFileOf(projectId, fmain);
				var adi = new List<DocumentInfo>();
				foreach (var mf in m.CodeFiles) {
					var docId = DocumentId.CreateNewId(projectId);
					AttachFileOf(docId, mf.f);
					var tav = TextAndVersion.Create(SourceText.From(mf.code, Encoding.UTF8), VersionStamp.Default);
					adi.Add(DocumentInfo.Create(docId, mf.f.Name, null, SourceCodeKind.Regular, TextLoader.From(tav), mf.f.ItemPath));
					//note: 'filePath=f.ItemPath' is important. If same path used in multiple projects, symbolfinder finds symbols
					//	with same name in all projects, although technically it's not the same symbol. This is what we need.
					//	Never mind: does not find implementations in projects added through meta c.
				}
				
				var pri = ProjectInfo.Create(projectId, VersionStamp.Default, m.Name, fmain.Name, LanguageNames.CSharp, null, null,
					m.CreateCompilationOptions(),
					m.CreateParseOptions(),
					adi,
					pr?.Select(p => new ProjectReference(p.Id)),
					m.References.Refs);
				
				sol = sol.AddProject(pri);
			}
		}
		//print.it(sol.Projects.Count(), sol.Projects.Select(o => o.Name));
		return sol;
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

//struct CiFileData {
//	public Document document;
//}
