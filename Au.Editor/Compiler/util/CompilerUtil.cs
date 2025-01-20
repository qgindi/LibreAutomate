using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Resources;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Au.Compiler;

static partial class CompilerUtil {
	/// <summary>
	/// Returns true if the dll file is a .NET assembly (any, not only of the .NET library).
	/// </summary>
	public static bool IsNetAssembly(string path) {
		using var pr = new PEReader(filesystem.loadStream(path));
		return pr.HasMetadata;
	}
	
	/// <summary>
	/// Returns true if the dll file is a .NET assembly. Sets isRefOnly = true if it's a reference-only assembly.
	/// </summary>
	public static bool IsNetAssembly(string path, out bool isRefOnly) {
		isRefOnly = false;
		using var pr = new PEReader(filesystem.loadStream(path));
		if (!pr.HasMetadata) return false;
		var mr = pr.GetMetadataReader();
		
		foreach (var v in mr.GetAssemblyDefinition().GetCustomAttributes()) {
			var ca = mr.GetCustomAttribute(v);
			var h = ca.Constructor;
			if (h.Kind == HandleKind.MemberReference) {
				var m = mr.GetMemberReference((MemberReferenceHandle)h);
				var t = mr.GetTypeReference((TypeReferenceHandle)m.Parent);
				var s = mr.GetString(t.Name);
				if (s == "ReferenceAssemblyAttribute") { isRefOnly = true; break; }
			}
		}
		
		return true;
	}
	
	public static CSharpSyntaxTree[] CreateSyntaxTrees(MetaComments meta) {
		var pOpt = meta.CreateParseOptions();
		var trees = new CSharpSyntaxTree[meta.CodeFiles.Count];
		for (int i = 0; i < trees.Length; i++) {
			var f1 = meta.CodeFiles[i];
			
			//never mind: should use Encoding.UTF8 etc if the file is with BOM. Encoding.Default is UTF-8 without BOM.
			//	Else, when debugging with VS or VS Code, they say "source code changed" and can't set breakpoints by default.
			//	But they have an option to debug modified files anyway.
			//	This program saves new files without BOM. It seems VS Code too. VS saves with BOM (maybe depends on its settings).
			//	CONSIDER: use ParseText overload with SourceText, for which use StreamReader that detects BOM. Not tested.
			var encoding = Encoding.Default;
			
			trees[i] = CSharpSyntaxTree.ParseText(f1.code, pOpt, f1.f.FilePath, encoding) as CSharpSyntaxTree;
			
			//info: file path is used later in several places: in compilation error messages, run time stack traces (from PDB), debuggers, etc.
			//	Our PrintServer.SetNotifications callback will convert file/line info to links. It supports compilation errors and run time stack traces.
		}
		return trees;
	}
	
	/// <summary>
	/// Creates managed resources for embedding.
	/// </summary>
	/// <param name="meta"></param>
	/// <param name="asmName"></param>
	/// <param name="trees">Syntax trees of all source files. Used to extract XAML icons from strings like "*icon". If library, this func can replace some array elements.</param>
	/// <param name="failed">Called when failed. Eg a file not found, or error in meta. The <b>FileNode</b> can be null.</param>
	/// <param name="result">
	/// Called for each embedded resource, including the one containing non-embedded resources. Receives:
	/// <br/>• name - embedded resource name.
	/// <br/>• stream - embedded resource data. Null if <i>resourcesFile</i> not null.
	/// <br/>• file - path of embedded resource file. If the resource contains non-embedded resources, it is <i>resourcesFile</i> (which can be null).</param>
	/// <param name="resourcesFile">If not null, saves non-embedded resources in this ".resources" file. Then the caller can add the file as an embedded resource.</param>
	/// <returns>false if failed.</returns>
	public static bool CreateManagedResources(MetaComments meta, string asmName, CSharpSyntaxTree[] trees, Action<Exception, FileNode> failed, Action<(string name, Stream stream, string file)> result, string resourcesFile = null) {
		ResourceWriter rw = null;
		MemoryStream stream = null;
		FileNode curFile = null;
		bool needStream = resourcesFile == null;
		
		void _RW() { rw ??= needStream ? new(stream = new()) : new(resourcesFile); }
		
		try {
			var a = meta.Resources;
			if (!a.NE_()) {
				foreach (var v in a) {
					//if (v.f == null) { // /resources //rejected
					//	_End();
					//	resourcesName = v.s + ".resources";
					//} else
					
					//if has suffix /path, make resource name = path relative to pathRoot, like "subfolder/filename.ext". Else "filename.ext".
					bool usePath = false;
					FileNode pathRoot = null;
					string resType = v.s;
					if (resType != null) {
						if (usePath = resType == "path") resType = null;
						else if (usePath = resType.Ends(" /path")) resType = resType[..^6].NullIfEmpty_();
						if (usePath) {
							curFile = v.f;
							pathRoot = meta.MainFile.f.Parent;
							if (!v.f.IsDescendantOf(pathRoot)) throw new ArgumentException("/path cannot be used if the file isn't in this folder");
							if (resType == "strings") throw new ArgumentException("/path cannot be used with /strings");
						}
					}
					
					if (v.f.IsFolder) {
						foreach (var des in v.f.Descendants()) if (!des.IsFolder) _Add(des, resType, pathRoot ?? v.f);
					} else {
						_Add(v.f, resType, pathRoot);
					}
				}
				curFile = null;
				
				void _Add(FileNode f, string resType, FileNode folder = null) {
					curFile = f;
					string name = f.Name, path = f.FilePath;
					if (folder != null) for (var pa = f.Parent; pa != folder; pa = pa.Parent) name = pa.Name + "/" + name;
					//print.it(f, resType, folder, name, path);
					if (resType == "embedded") {
						result((name, needStream ? filesystem.loadStream(path) : null, path));
					} else {
						name = name.Lower(); //else pack URI fails; ResourceUtil can find any.
						_RW();
						switch (resType) {
						case null:
							//rw.AddResource(name, File.OpenRead(path), closeAfterWrite: true); //no, would not close on error
							rw.AddResource(name, new MemoryStream(filesystem.loadBytes(path)));
							break;
						case "byte[]":
							rw.AddResource(name, filesystem.loadBytes(path));
							break;
						case "string":
							rw.AddResource(name, filesystem.loadText(path));
							break;
						case "strings":
							var csv = csvTable.load(path);
							if (csv.ColumnCount != 2) throw new ArgumentException("CSV must contain 2 columns separated with ,");
							foreach (var row in csv.Rows) rw.AddResource(row[0], row[1]);
							break;
						default: throw new ArgumentException("error in meta: Incorrect /suffix");
						}
					}
					//documented in DProperties: This program does not URL-encode resource names.
					//	VS encodes space -> %20, UTF8 -> %xx, ^ -> %5e, etc. But not like .NET URL-encoding functions do.
					//	I did not find documentation about it.
					//	If not encoded, pack URI will not work.
					//	But if encoded, ResourceUtil would not work. Or should it URL-encode the parameter?
				}
			}
			
			//add XAML icons from strings like "*name #color"
			if (meta.Role != MCRole.editorExtension && 0 == (meta.MiscFlags & 1)) {
				HashSet<string> hs = null;
				for (int i = 0; i < trees.Length; i++) {
					List<LiteralExpressionSyntax> ai = null;
					var tree = trees[i];
					var root = tree.GetCompilationUnitRoot();
					foreach (var v in root.DescendantNodes()) {
						if (v is LiteralExpressionSyntax les && les.IsKind(SyntaxKind.StringLiteralExpression)
							&& les.Token.Value is string s && s.Length >= 8 && s[0] == '*') {
							bool hasLibraryPrefix = s[1] == '<';
							if (hasLibraryPrefix) {
								int j = s.IndexOf('>');
								if (j < 0 || !s.Eq(2..j, asmName, true)) continue;
								s = s[++j..];
							}
							if (DIcons.TryGetIconFromBigDB(s, out var xaml)) {
								s = WpfUtil_.RemoveColorFromIconString(s);
								s = s.Lower();
								if ((hs ??= new()).Add(s)) {
									_RW();
									rw.AddResource(s, xaml);
								}
								if (!hasLibraryPrefix && meta.Role == MCRole.classLibrary) (ai ??= new()).Add(les);
							}
						}
					}
					if (ai != null) { //in library need "*<asmName>*name #color"
						var r2 = root.ReplaceNodes(ai, (n, _) => {
							var s = n.Token.Value as string;
							var tok = SyntaxFactory.Literal($"*<{asmName}>{s.Lower()}");
							return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, tok);
						});
						trees[i] = tree.WithRootAndOptions(r2, tree.Options) as CSharpSyntaxTree;
					}
				}
			}
			
			if (rw != null) {
				rw.Generate();
				stream?.Seek(0, SeekOrigin.Begin);
				result((asmName + ".g.resources", stream, resourcesFile));
			}
			
			return true;
		}
		catch (Exception e) {
			failed(e, curFile);
			return false;
		}
		finally {
			if (!needStream) rw?.Dispose(); //else Roslyn will close the stream. There is no other disposable data in rw.
		}
	}
	
	public static void CopyFileIfNeed(string sFrom, string sTo) {
		//print.it(sFrom);
		if (filesystem.getProperties(sTo, out var p2, FAFlags.UseRawPath) //if exists
			&& filesystem.getProperties(sFrom, out var p1, FAFlags.UseRawPath)
			&& p2.LastWriteTimeUtc == p1.LastWriteTimeUtc
			&& p2.Size == p1.Size) return;
		filesystem.copy(sFrom, sTo, FIfExists.Delete);
	}
	
	/// <summary>
	/// Gathers all files from meta 'file' of <i>meta</i> project and descendant pr projects, and copies to <i>outDir</i> or calls callback for each.
	/// </summary>
	/// <param name="meta"></param>
	/// <param name="outDir"></param>
	/// <param name="copy">Called for each file. If null, calls <see cref="CopyFileIfNeed"/>.</param>
	public static void CopyMetaFileFilesOfAllProjects(MetaComments meta, string outDir, Action<string, string> copy = null) {
		HashSet<FileNode> noDup = null;
		_OtherFilesOfProject(meta);
		
		void _OtherFilesOfProject(MetaComments m) {
			if (m != meta) if (!(noDup ??= new()).Add(m.MainFile.f)) return; //skip duplicates
			
			if (m.OtherFiles != null) {
				foreach (var v in m.OtherFiles) {
					var dest = outDir;
					if (!v.s.NE()) dest = pathname.combine(dest, v.s, s2CanBeFullPath: true);
					
					_CopyOther(v.f, dest);
					
					void _CopyOther(FileNode f, string dest) {
						if (f.IsFolder) {
							if (!f.Name.Ends('-')) dest = dest + "\\" + f.Name;
							foreach (var c in f.Children()) _CopyOther(c, dest);
						} else {
							string from = f.FilePath, to = dest + "\\" + pathname.getName(from);
							if (copy != null) copy(from, to);
							else CopyFileIfNeed(from, to);
						}
					}
				}
			}
			
			if (m.ProjectReferences is { } pr)
				foreach (var v in pr) _OtherFilesOfProject(v.m);
		}
	}

#if false //in the past this was used for sqlite. Maybe in the future this code can be useful for something.
	public static bool UsesSqlite(Stream asmStream, bool recursive = false) {
		using var pr = new PEReader(asmStream, PEStreamOptions.LeaveOpen);
		var mr = pr.GetMetadataReader();
		
		//var usedRefs = mr.AssemblyReferences.Select(handle => mr.GetString(mr.GetAssemblyReference(handle).Name)).ToArray();
		//print.it(usedRefs);
		
		foreach (var handle in mr.TypeReferences) {
			var tr = mr.GetTypeReference(handle);
			//print.it(mr.GetString(tr.Name), mr.GetString(tr.Namespace));
			string type = mr.GetString(tr.Name);
			if ((type.Starts("sqlite") && mr.GetString(tr.Namespace) == "Au")
				|| (type.Starts("SL") && mr.GetString(tr.Namespace) == "Au.Types")) return true;
		}
		
		if (recursive) {
			var fs = asmStream as FileStream; Debug.Assert(fs != null);
			var dir = pathname.getDirectory(fs.Name, withSeparator: true);
			var hs = new HashSet<string>();
			foreach (var arh in mr.AssemblyReferences) {
				var ar = mr.GetAssemblyReference(arh);
				var an = mr.GetString(ar.Name);
				if (MetaReferences.IsDefaultRef(an)) continue;
				if (hs.Contains(an)) continue;
				var path2 = dir + an + ".dll";
				if (!filesystem.exists(path2, useRawPath: true).File) continue;
				if (UsesSqlite(path2, true)) return true;
			}
		}
		
		return false;
	}
	
	/// <summary>
	/// Returns true if the .NET assembly uses sqlite types and therefore sqlite3.dll.
	/// </summary>
	/// <param name="dll">Assembly path.</param>
	/// <param name="recursive">Include descendant reference assemblies.</param>
	public static bool UsesSqlite(string dll, bool recursive = false) {
		using var fs = filesystem.loadStream(dll);
		return UsesSqlite(fs, recursive);
	}
#endif
	
	public static bool RunPrePostBuildScript(MetaComments m, bool post, string outFile, bool publish) {
		var x = post ? m.PostBuild : m.PreBuild;
		var f = m.MainFile.f;
		string outPath = publish ? pathname.getDirectory(outFile) : m.OutputPath;
		
		string[] args;
		if (x.s == null) {
			args = new string[] { outFile };
		} else {
			args = StringUtil.CommandLineToArray(x.s);
			
			//fbc: replace variables like $(variable)
			for (int i = 0; i < args.Length; i++)
				if (args[i].Contains("$(")) {
					args[i] = (s_rx1 ??= new regexp(@"\$\((\w+)\)")).Replace(args[i], k => k[1].Value switch {
						"outputFile" => outFile,
						"outputPath" => outPath,
						"source" => f.ItemPath,
						"role" => m.Role.ToString(),
						"optimize" => m.Optimize ? "true" : "false",
						"bit32" => m.Platform == MCPlatform.x86 ? "true" : "false",
						_ => throw new ArgumentException("error in meta: unknown variable " + k.Value)
					});
				}
		}
		
		bool ok = Compiler.Compile(CCReason.Run, out var r, x.f);
		if (r.role != MCRole.editorExtension) throw new ArgumentException($"'{x.f.Name}' role must be editorExtension");
		if (!ok) return false;
		
		PrePostBuild.Info = new(outFile, outPath, f.ItemPath, m.Role.ToString(), m.Optimize, m.Platform.ToString(), !post, publish);
		try { EditorExtension.Run_(r.file, args, handleExceptions: false); }
		finally { PrePostBuild.Info = null; }
		return true;
	}
	static regexp s_rx1;
	
	public static void DeleteExeFile(string programPath) {
		if (filesystem.exists(programPath, useRawPath: true) && !Api.DeleteFile(programPath)) {
			var a = process.getProcessIds(programPath, fullPath: true);
			if (a.Any()) {
				//if (!dialog.showYesNo("End process?", $"Need to delete this program file, but the program is running.\n\n{programPath}", owner: App.Hmain)) return;
				foreach (var v in a) process.terminate(v);
				wait.until(-3, () => Api.DeleteFile(programPath));
			}
		}
	}
}
