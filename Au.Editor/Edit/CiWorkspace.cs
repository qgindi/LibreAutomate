extern alias CAW;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;


namespace LA;

/// <summary>
/// Ctor creates new <b>AdhocWorkspace</b> (<see cref="Workspace"/>) and adds projects and documents.
/// Dtor disposes the <b>AdhocWorkspace</b>. Must be disposed soon.
/// </summary>
class CiWorkspace : IDisposable {
	public AdhocWorkspace Workspace { get; }
	
	/// <summary>
	/// <c>=> Workspace.CurrentSolution</c>
	/// </summary>
	public Solution Solution => Workspace.CurrentSolution;
	
	/// <summary>
	/// <b>MetaComments</b> of the main project.
	/// </summary>
	public MetaComments Meta { get; }
	
	/// <summary>
	/// Files etc of the main project (the first) and pr projects.
	/// </summary>
	public ProjectData[] Projects { get; }
	
	/// <summary>
	/// Gets <b>Compilation</b> of the main project: <c>=> Solution.GetProject(Projects[0].projectId).GetCompilationAsync().Result</c>
	/// </summary>
	public Compilation GetCompilation() => Solution.GetProject(Projects[0].projectId).GetCompilationAsync().Result_();
	
	/// <summary>
	/// <b>DocumentId</b> of <i>fn</i>, or null.
	/// Searches only in the main project.
	/// </summary>
	public DocumentId GetDocId(FileNode fn) {
		foreach (var v in Projects[0].files) if (v.f.f == fn) return v.docId;
		return null;
	}
	
	public CiWorkspace(FileNode fn, Caller caller) {
		Workspace = new();
		List<ProjectData> projects = new();
		
		bool inEditor = caller is Caller.CodeInfoNormal or Caller.CodeInfoWpfPreview;
		
		if (!fn.FindProject(out var projFolder, out var projMain)) projMain = fn;
		Debug.Assert(projMain.IsCodeFile);
		
		if (inEditor) TestInternal.CiClear();
		
		List<ProjectInfo> aPI = new();
		Dictionary<FileNode, ProjectReference> dPR = null;
		var mcFlags = caller switch {
			Caller.CodeInfoNormal => MCFlags.ForCodeInfoInEditor,
			Caller.CodeInfoWpfPreview => MCFlags.ForCodeInfoInEditor | MCFlags.WpfPreview,
			_ => MCFlags.ForCodeInfo
		};
		(_, Meta) = _AddProject(projMain, projFolder, mcFlags);
		Workspace.AddProjects(aPI);
		Projects = projects.ToArray();
		
		(ProjectId pid, MetaComments meta) _AddProject(FileNode projMain, FileNode projFolder, MCFlags mcFlags) {
			var m = new MetaComments(mcFlags);
			m.Parse(projMain, projFolder);
			
			if (inEditor && m.TestInternal is string[] testInternal) TestInternal.CiAdd(m.Name, testInternal);
			
			var projectId = ProjectId.CreateNewId();
			
			ProjectData pd = new(m, projectId, new());
			projects.Add(pd);
			
			if (inEditor) CiProjects.AttachFileOf(projectId, projMain);
			var adi = new List<DocumentInfo>();
			foreach (var k in m.CodeFiles) {
				var docId = DocumentId.CreateNewId(projectId);
				var tav = TextAndVersion.Create(SourceText.From(k.code, Encoding.UTF8), VersionStamp.Default, k.f.FilePath);
				adi.Add(DocumentInfo.Create(docId, k.f.Name, null, SourceCodeKind.Regular, TextLoader.From(tav), k.f.ItemPath));
				if (inEditor) CiProjects.AttachFileOf(docId, k.f);
				pd.files.Add((k, docId));
			}
			//TODO3: reuse document+syntaxtree of global.cs and its meta c files if their text not changed.
			
			int iPI = aPI.Count; aPI.Add(null); //let this project be before pr projects in aPI
			
			List<ProjectReference> aPR = null;
			if (caller != Caller.Other && m.ProjectReferences is { } a1) {
				dPR ??= new();
				foreach (var v in a1) {
					if (!dPR.TryGetValue(v.f, out var pr)) {
						pr = new ProjectReference(_AddProject(v.f, v.f.Parent, mcFlags & ~MCFlags.WpfPreview).pid);
						dPR.Add(v.f, pr);
					}
					(aPR ??= new()).Add(pr);
				}
			}
			
			aPI[iPI] = ProjectInfo.Create(projectId, VersionStamp.Default, m.Name, m.Name, LanguageNames.CSharp, null, null,
				m.CreateCompilationOptions(),
				m.CreateParseOptions(),
				adi,
				aPR,
				m.References.Refs);
			
			return (projectId, m);
		}
	}
	
	public void Dispose() {
		Workspace.Dispose();
	}
	
	public enum Caller {
		/// <summary>Called by <b>CodeInfo</b>. Supports meta testInternal, <see cref="CiProjects.FileOf"/>.</summary>
		CodeInfoNormal,
		
		/// <summary><b>CodeInfoNormal</b> + <see cref="MCFlags.WpfPreview"/>.</summary>
		CodeInfoWpfPreview,
		
		/// <summary>Add project references (meta pr).</summary>
		OtherPR,
		
		/// <summary>Don't add project references (meta pr).</summary>
		Other
	}
	
	public record struct ProjectData(MetaComments meta, ProjectId projectId, List<(MCCodeFile f, DocumentId docId)> files);
}
