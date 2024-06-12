namespace Au.Compiler;

partial class Compiler {
	/// <summary>
	/// Resolves whether need to [re]compile or can run previously compiled assembly.
	/// </summary>
	unsafe class XCompiled {
		readonly string _file;
		Dictionary<uint, string> _data;
		string _environmentString;
		
		public string CacheDirectory { get; }
		
		public static XCompiled OfWorkspace => (App.Model.CompilerContext_ ??= new XCompiled()) as XCompiled;
		
		public XCompiled() {
			CacheDirectory = App.Model.WorkspaceDirectory + @"\.compiled";
			_file = CacheDirectory + @"\compiled.log";
			
			//The first line in the log file contains .NET version, Au.dll version, OS version and workspace path,
			//	like 5.0.4|1.2.3.4|A00-64|\\?\Volume{GUID}\Path
			//	Volume GUID for a removable drive is:
			//		The same on the same computer/OS, regardless of drive letter and USB port.
			//		Different on each computer/OS. It's good.
			filesystem.more.getFinalPath(folders.Workspace, out var ws, format: FPFormat.VolumeGuid);
			_environmentString = osVersion.onaString + "|" + ws;
		}
		
		/// <summary>
		/// Called before executing script f. If returns true, don't need to compile.
		/// </summary>
		/// <param name="f"></param>
		/// <param name="r">Receives file path and execution options.</param>
		/// <param name="projFolder">Project folder or null. If not null, f must be its main file.</param>
		public bool IsCompiled(FileNode f, out CompResults r, FileNode projFolder) {
			r = new CompResults();
			
			if (_data == null && !_Open()) return false;
			
			if (!_data.TryGetValue(f.Id, out string value)) return false;
			//Debug_.Print(value);
			int iPipe = 0;
			
			bool isScript = f.IsScript;
			r.role = MetaComments.DefaultRole(isScript);
			
			string asmFile;
			if (r.notInCache = value?.Starts("|=") ?? false) {
				iPipe = value.IndexOf('|', 2); if (iPipe < 0) iPipe = value.Length;
				asmFile = pathname.NormalizeMinimally_(value[2..iPipe]);
			} else {
				asmFile = CacheDirectory + "\\" + f.IdString + ".dll";
			}
			//print.it(asmFile);
			
			if (!filesystem.getProperties(asmFile, out var asmProp, FAFlags.UseRawPath | FAFlags.DontThrow)) return false;
			DateTime asmTime = asmProp.LastWriteTimeUtc;
			
			if (_IsFileModified(f)) return false;
			
			bool isMultiFileProject = false;
			if (value != null && iPipe < value.Length) {
				iPipe++;
				foreach (var v in value.Split(iPipe.., '|', StringSplitOptions.RemoveEmptyEntries)) {
					int offs = v.start + 1;
					char ch = value[v.start];
					switch (ch) {
					case 't':
						r.role = (MCRole)value.ToInt(offs);
						break;
					case 'n':
						r.ifRunning = (MCIfRunning)value.ToInt(offs);
						break;
					case 'u':
						r.uac = (MCUac)value.ToInt(offs);
						break;
					case 'b':
						r.bit32 = true;
						break;
					case 'f':
						r.flags = (MiniProgram_.MPFlags)value.ToInt(offs);
						break;
					case 'p':
						isMultiFileProject = true;
						if (projFolder != null) {
							if (!Hash.MD5Result.FromString(value.AsSpan(offs, v.end - offs), out var md5)) return false;
							Hash.MD5Context md = default;
							foreach (var f1 in projFolder.EnumProjectClassFiles(f)) {
								if (_IsFileModified(f1)) return false;
								md.Add(f1.Id);
							}
							if (md.IsEmpty || md.Hash != md5) return false;
						}
						break;
					case '*':
						string dll;
						if (value[offs] == '*') dll = string.Concat(App.Model.NugetDirectoryBS, value.AsSpan((offs + 1)..v.end));
						else if (value[offs] == '?') dll = string.Concat(App.Model.DllDirectoryBS, value.AsSpan((offs + 1)..v.end));
						else {
							dll = value[offs..v.end];
							if (!pathname.isFullPathExpand(ref dll)) dll = folders.ThisAppBS + dll;
						}
						if (_IsFileModified2(dll)) return false;
						break;
					case 'l':
					case 'c':
					case 'x':
					case 'y':
					case 'k':
					case 'm':
					case 's':
						value.ToInt(out uint u1, offs);
						var f2 = App.Model.FindById(u1);
						if (f2 == null) return false;
						if (ch == 'l') {
							if (f2.FindProject(out var projFolder2, out var projMain2)) f2 = projMain2;
							if (f2 == f) return false; //will be compiler error "circular reference"
							//print.it(f2, projFolder2);
							if (!IsCompiled(f2, out _, projFolder2)) return false;
							//print.it("library is compiled");
						} else {
							if (_IsFileModified(f2)) return false;
						}
						break;
					default: return false;
					}
				}
			}
			if (isMultiFileProject != (projFolder != null)) {
				if (projFolder == null) return false;
				foreach (var f1 in projFolder.EnumProjectClassFiles(f)) return false; //project with single file?
			}
			//Debug_.Print("compiled");
			
			r.file = asmFile;
			r.name = pathname.getNameNoExt(f.Name);
			return true;
			
			bool _IsFileModified(FileNode f_) => _IsFileModified2(f_.FilePath);
			
			bool _IsFileModified2(string path_) {
				if (!filesystem.getProperties(path_, out var prop_, FAFlags.UseRawPath | FAFlags.DontThrow)) return true;
				Debug.Assert(!prop_.Attributes.Has(FileAttributes.Directory));
				//print.it(prop_.LastWriteTimeUtc, asmDate);
				if (prop_.LastWriteTimeUtc > asmTime) return true;
				return false;
			}
		}
		
		/// <summary>
		/// Called when successfully compiled script f. Saves data that next time will be used by <see cref="IsCompiled"/>.
		/// </summary>
		/// <param name="outFile">The output assembly.</param>
		public void AddCompiled(FileNode f, string outFile, MetaComments m, MiniProgram_.MPFlags miniFlags) {
			if (_data == null && !_Open()) _data = new();
			
			/*
IDmain|=path.exe|tN|nN|uN|fN|b|pMD5project|cIDcode|lIDlibrary|xIDresource|yIDfile|kIDicon|mIDmanifest|sIDsign|*ref
= - outFile
t - role
n - ifRunning
u - uac
f - miniFlags
b - bit32
p - MD5 of Id of all project files except main
c - c
l - pr
x - resource
y - file
k - icon
m - manifest
s - sign
* - r
			*/
			
			string value = null;
			using (new StringBuilder_(out var b)) {
				if (m.OutputPath != null) b.Append("|=").Append(folders.unexpandPath(outFile)); //else f.Id in cache
				if (m.Role != MetaComments.DefaultRole(m.MainFile.f.IsScript)) b.Append("|t").Append((int)m.Role);
				if (m.IfRunning != default) b.Append("|n").Append((int)m.IfRunning);
				if (m.Uac != default) b.Append("|u").Append((int)m.Uac);
				if (miniFlags != default) b.Append("|f").Append((int)miniFlags);
				if (m.Bit32) b.Append("|b");
				
				Hash.MD5Context md = default;
				foreach (var v in m.CodeFiles) {
					if (v.isC) _AppendFile("|c", v.f); //ids of C# files added through meta 'c'
					else if (!v.isMain) md.Add(v.f.Id); //MD5 hash of project files, except main
				}
				if (!md.IsEmpty) b.Append("|p").Append(md.Hash.ToString());
				
				if (m.ProjectReferences is { } pr) foreach (var (v, _) in pr) _AppendFile("|l", v); //ids of meta 'pr' files
				if (m.Resources != null) foreach (var v in m.Resources) _AppendFile("|x", v.f); //ids of meta 'resource' files
				if (m.OtherFiles != null) foreach (var v in m.OtherFiles) _AppendFile("|y", v.f); //ids of meta 'file' files
				_AppendFile("|k", m.IconFile);
				_AppendFile("|m", m.ManifestFile);
				_AppendFile("|s", m.SignFile);
				
				//references
				var refs = m.References.Refs;
				int j = m.References.DefaultRefCount;
				if (refs.Count > j) {
					string appDir = folders.ThisAppBS, nugetDir = App.Model.NugetDirectoryBS, dllDir = App.Model.DllDirectoryBS;
					for (; j < refs.Count; j++) {
						string s1 = refs[j].FilePath, prefix = "|*";
						if (s1.Starts(appDir, true)) {
							s1 = s1[appDir.Length..];
						} else if (s1.Starts(nugetDir, true)) {
							s1 = s1[nugetDir.Length..];
							prefix = "|**";
						} else if (s1.Starts(dllDir, true)) {
							s1 = s1[dllDir.Length..];
							prefix = "|*?";
						} else {
							s1 = folders.unexpandPath(s1);
						}
						b.Append(prefix).Append(s1);
					}
				}
				
				if (b.Length != 0) value = b.ToString();
				
				void _AppendFile(string m, FileNode f) {
					if (f == null) return;
					if (f.IsFolder) {
						Debug.Assert(m is "|x" or "|y" or "|k");
						foreach (var des in f.Descendants()) if (!des.IsFolder) b.Append(m).Append(des.IdString);
					} else {
						b.Append(m).Append(f.IdString);
					}
				}
			}
			
			uint id = f.Id;
			if (_data.TryGetValue(id, out var oldValue) && value == oldValue) { /*Debug_.Print("same");*/ return; }
			//Debug_.Print("different");
			_data[id] = value;
			_Save();
		}
		
		/// <summary>
		/// Removes saved f data, so that next time <see cref="IsCompiled"/> will return false.
		/// </summary>
		public void Remove(FileNode f, bool deleteAsmFile) {
			if (_data == null && !_Open()) return;
			if (_data.Remove(f.Id)) {
				_Save();
				if (deleteAsmFile) {
					try { filesystem.delete(CacheDirectory + "\\" + f.IdString + ".dll"); }
					catch (Exception ex) { Debug_.Print(ex); }
				}
			}
		}
		
		bool _Open() {
			if (_data != null) return true;
			if (!filesystem.exists(_file).File) return false;
			string sData = filesystem.loadText(_file);
			foreach (var v in sData.Lines(.., noEmpty: true)) {
				if (_data == null) {
					if (sData[v.Range] != _environmentString) goto g1;
					_data = new(sData.LineCount());
				} else {
					sData.ToInt(out uint id, v.start, out int idEnd);
					if (null != App.Model.FindById(id))
						_data[id] = v.end > idEnd ? sData[idEnd..v.end] : null;
				}
			}
			if (_data == null) return false; //empty file
			
			//delete temp files
			foreach (var f in filesystem.enumerate(CacheDirectory, FEFlags.UseRawPath | FEFlags.IgnoreInaccessible)) {
				if (f.Name.Like("*'*")) Api.DeleteFile(f.FullPath);
			}
			
			return true;
			g1:
			_ClearCache();
			return false;
		}
		
		void _Save() {
			filesystem.createDirectory(CacheDirectory);
			using var b = filesystem.waitIfLocked(() => File.CreateText(_file));
			b.WriteLine(_environmentString);
			foreach (var v in _data) {
				if (v.Value == null) b.WriteLine(v.Key); else { b.Write(v.Key); b.WriteLine(v.Value); }
			}
			//tested: fast, same speed as StringBuilder+WriteAllText. With b.WriteLine(v.Key+v.Value) same speed or slower.
		}
		
		void _ClearCache() {
			_data = null;
			try { filesystem.delete(CacheDirectory); }
			catch (AuException e) { print.warning("Failed to delete compiled script assembly cache. " + e.ToString(), -1); }
		}
	}
	
	public static void Uncache(FileNode f, bool andDescendants = false) {
		var x = XCompiled.OfWorkspace;
		x.Remove(f, true);
		if (andDescendants && f.IsFolder) foreach (var v in f.Descendants()) x.Remove(v, true);
	}
}
