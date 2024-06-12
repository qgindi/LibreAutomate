extern alias CAW;

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;

namespace Au.Compiler;

/// <summary>
/// Resolves assembly metadata references (string to PortableExecutableReference).
/// For each compilation create a MetaReferences variable, call Resolve if need non-default references, then use Refs list.
/// </summary>
/// <remarks>
/// Temporarily keeps PortableExecutableReference objects in a cache. Except Au and .NET design-time assemblies (they are small, without code).
/// Single static cache is used by all MetaReferences variables.
/// Cache may require many MB of unmanaged memory, therefore PortableExecutableReference objects are removed and GC-collected when not used anywhere for some time. Reloading is quite fast.
/// 	A reference to a PortableExecutableReference variable prevents removing it from cache and GC-collecting.
/// 	A reference to a MetaReferences variable prevents removing/disposing its Refs items.
/// 	CodeAnalysis Project etc objects also have references, therefore need to manage their lifetimes too.
/// MetaReferences variables can be created in different threads, but a variable must be used in a single thread.
/// </remarks>
class MetaReferences {
	/// <summary>
	/// Contains info used to load a PortableExecutableReference. Loads when need. Supports XML documentation.
	/// </summary>
	class _MR {
		public readonly string name, path;
		public readonly bool isNuget;
		public bool ignoreThis, ignoreDef;
		readonly WeakReference<PortableExecutableReference> _wr = new(null);
		PortableExecutableReference _refKeeper;
		long _rkTimeout;
		DateTime _fileTime;
		
		public _MR(string name, string path, bool isNuget) {
			//print.it(name);
			this.name = name;
			this.path = path;
			this.isNuget = isNuget;
		}
		
		public PortableExecutableReference Ref {
			get {
				if (!_wr.TryGetTarget(out var r)) {
					//for(int i=0;i<10;i++) //tested: process memory does not grow when loading same file several times
					//perf.first();
					r = MetadataReference.CreateFromFile(path, (this as _MR2)?.Prop ?? default, _DocumentationProvider.Create(path));
					if (isNuget) s_attach.AddOrUpdate(r, "");
					_fileTime = filesystem.getProperties(path, out var p, FAFlags.UseRawPath | FAFlags.DontThrow) ? p.LastWriteTimeUtc : default;
					//perf.nw();
					
					_wr.SetTarget(r);
					//print.it("LOADED", name, this is _MR2);
				} //else print.it("cached", name, this is _MR2);
				
				//prevent GC too early, eg while compiling many files
				_rkTimeout = Environment.TickCount64 + 30_000;
				_refKeeper = r;
				
				return r;
			}
		}
		
		public void UncacheIfNeed(long timeNow) {
			if (_fileTime != default) {
				filesystem.getProperties(path, out var p, FAFlags.UseRawPath | FAFlags.DontThrow);
				if (p.LastWriteTimeUtc != _fileTime) {
					_wr.SetTarget(null);
					_refKeeper = null;
					_fileTime = default;
					return;
				}
			}
			if (_refKeeper != null && timeNow > _rkTimeout) _refKeeper = null;
		}
		
#if DEBUG
		public bool IsCached => _wr.TryGetTarget(out _);
#endif
	}
	
	class _MR2 : _MR {
		readonly string _alias;
		readonly bool _isCOM;
		
		public _MR2(string name, string path, string alias, bool isCOM, bool isNuget) : base(name, path, isNuget) {
			_alias = alias;
			_isCOM = isCOM;
		}
		
		public MetadataReferenceProperties Prop => new(aliases: _alias == null ? default : ImmutableArray.Create(_alias), embedInteropTypes: _isCOM);
		
		public bool PropEq(string alias, bool isCOM) => isCOM == _isCOM && alias == _alias;
	}
	
	static readonly List<_MR> s_cache = new();
	
	public static bool IsNuget(PortableExecutableReference r) => s_attach.TryGetValue(r, out _);
	static ConditionalWeakTable<PortableExecutableReference, string> s_attach = new();
	
	/// <summary>
	/// List containing most <see cref="DefaultReferences"/> + references for which was called <see cref="Resolve"/> of this MetaReferences variable.
	/// </summary>
	public IReadOnlyList<PortableExecutableReference> Refs => _refs;
	readonly List<PortableExecutableReference> _refs;
	
	/// <summary>
	/// These references are added when compiling any script/library.
	/// Au.dll and all .NET design-time assemblies.
	/// </summary>
	public static readonly Dictionary<string, PortableExecutableReference> DefaultReferences = new(300, StringComparer.OrdinalIgnoreCase);
	
	/// <summary>
	/// Returns true if <i>name</i> is in <b>DefaultReferences</b> or is a private .NET assembly. Case-insensitive.
	/// </summary>
	public static bool IsDefaultRef(string name) => DefaultReferences.ContainsKey(name) || name.Eqi("System.Private.CoreLib");
	
	static MetaReferences() {
		//var p1 = perf.local();
		s_netDocProvider = new _NetDocumentationProvider();
		//p1.Next('d');
		
		using var db = EdDatabases.OpenRef();
		using var stat = db.Statement("SELECT * FROM ref");
		while (stat.Step()) {
			var asmName = stat.GetText(0);
			var doc = s_netDocProvider.HaveRef(asmName) ? s_netDocProvider : null;
			var r = MetadataReference.CreateFromImage(stat.GetArray<byte>(1), documentation: doc, filePath: asmName);
			DefaultReferences.Add(asmName, r);
		}
		//p1.Next('c');
		
		var auPath = folders.ThisAppBS + "Au.dll";
		DefaultReferences.Add("Au", MetadataReference.CreateFromFile(auPath, documentation: _DocumentationProvider.Create(auPath)));
		//p1.NW('a');
		
		App.Timer1sWhenVisible += UncacheOldFiles;
	}
	
	public static void UncacheOldFiles() {
		//using var p1 = perf.local();
		lock (s_cache) {
			long timeNow = Environment.TickCount64;
			foreach (var v in s_cache) v.UncacheIfNeed(timeNow);
		}
	}
	
	public MetaReferences() {
		var def = DefaultReferences;
		_refs = new List<PortableExecutableReference>(def.Count + 10);
		foreach (var v in def) _refs.Add(v.Value);
		
		DefaultRefCount = _refs.Count;
	}
	
	//public MetaReferences(FileNode fn) : this() {
	//	_test = fn.Name == "test.cs";
	//}
	//bool _test;
	
	/// <summary>
	/// If <i>s</i> is like "X.dll /alias=Abc" (or "Abc=X.dll" if <i>supportOldSyntax</i>), splits and sets <i>s1</i> = "X.dll" and <i>alias</i> = "Abc".
	/// </summary>
	/// <param name="s1">Receives <i>s</i> part before " /", or full <i>s</i>.</param>
	/// <param name="alias">Receives <i>s</i> part after " /alias=", or null.</param>
	/// <param name="supportOldSyntax">Support "Alias=X.dll".</param>
	/// <returns>false if invalid "/string".</returns>
	internal static bool ParseAlias_(string s, out string s1, out string alias, bool supportOldSyntax = false) {
		alias = null;
		s1 = MetaComments.SplitArgs_(s, out var s2);
		if (s2 != null) {
			foreach (var v in s2.Split('|')) {
				if (v.Starts("alias=")) alias = v[6..].NullIfEmpty_();
				else return false;
			}
		} else if (supportOldSyntax && s.Contains('=')) { //fbc: "alias=ref"
			if (s.RxMatch(@"^(\w+)=(.+)$", out var rm)) { alias = rm[1].Value; s1 = rm[2].Value; }
		}
		return true;
	}
	
	/// <summary>
	/// Finds reference assembly file, creates PortableExecutableReference and adds to the cache.
	/// Returns false if file not found.
	/// </summary>
	/// <param name="reference">
	/// Assembly filename (like "My.dll" or "My"), relative path or full path.
	/// If not full path, must be in <b>folders.ThisApp</b>; if <i>isCOM</i> - in App.Model.WorkspaceDirectory + @"\.interop\".
	/// </param>
	/// <param name="alias">Alias or null.</param>
	/// <exception cref="Exception">Eg failed to load the found file.</exception>
	/// <remarks>
	/// Loads the file but does not parse. If bad format, error later when compiling.
	/// </remarks>
	public bool Resolve(string reference, string alias, bool isCOM, bool isNuget) {
		if (reference.Length == 0) return false;
		
		var cache = s_cache;
		lock (cache) {
			int i;
			for (i = 0; i < cache.Count; i++) {
				if (cache[i].name.Eqi(reference) && _PropEq(cache[i], alias, isCOM)) goto g1;
			}
			
			var path = reference;
			bool isFull = pathname.isFullPathExpand(ref path);
			if (!isFull && isCOM) { isFull = true; path = App.Model.WorkspaceDirectory + @"\.interop\" + path; }
			if (!isFull) path = folders.ThisAppBS + path;
			path = pathname.Normalize_(path);
			if (!filesystem.exists(path, useRawPath: true).File) return false;
			//note: we don't use Microsoft.CodeAnalysis.Scripting.ScriptMetadataResolver. It is very slow, makes compiling many times slower.
			
			for (i = 0; i < cache.Count; i++) {
				if (cache[i].path.Eqi(path) && _PropEq(cache[i], alias, isCOM)) goto g1;
			}
			
			_MR k;
			if (isCOM || alias != null) k = new _MR2(reference, path, alias, isCOM, isNuget);
			else k = new _MR(reference, path, isNuget);
			
			if (!isCOM && _BadDefReplacement(k)) throw new InvalidOperationException($"Cannot use reference '{reference}'.");
			
			cache.Add(k);
			
			g1:
			_AddRef(i);
		}
		
		return true;
		
		static bool _PropEq(_MR u, string alias, bool isCOM) {
			if (u is _MR2 m2) return m2.PropEq(alias, isCOM);
			return !isCOM && alias == null;
		}
		
		void _AddRef(int iCache) {
			var k = s_cache[iCache];
			
			if (k.ignoreThis) return;
			
			var r = k.Ref;
			if (_refs.Contains(r)) return;
			
			if (k.ignoreDef) {
				var s = pathname.getNameNoExt(k.path);
				for (int i = DefaultRefCount; --i >= 0;) {
					if (_refs[i].Display.Eqi(s)) {
						_refs.RemoveAt(i);
						DefaultRefCount--;
						break;
					}
				}
			}
			
			_refs.Add(r);
		}
		
		//Returns true if k name matches one of default refs, unless k is any version of that ref.
		//In the later case sets k.ignoreDef=true if k is a newer version, else sets k.ignoreThis=true.
		bool _BadDefReplacement(_MR k) {
			var name = pathname.getNameNoExt(k.path);
			if (!DefaultReferences.TryGetValue(name, out var def)) return false;
			
			if (_hsNoRef?.Contains(name) ?? false) return false;
			//TODO3: what if eg two nuget packages (from different folders) have two different versions of that assembly?
			//	Eg from _refs already removed v6 and added v8, and now trying to add v7 or v9.
			//	Rare.
			//	Now should try to find in _refs. If not found in default area and found in nondefault area, replace the nondefault.
			
			var idR = _Identity(k.Ref); if (idR == null) return true;
			var idD = _Identity(def);
			//print.it(idR, idDef);
			if (idR.Name != idD.Name || !idR.PublicKeyToken.SequenceEqual(idD.PublicKeyToken)) return true;
			if (idR.Version > idD.Version) k.ignoreDef = true; else k.ignoreThis = true;
			return false;
			
			static AssemblyIdentity _Identity(PortableExecutableReference pe) {
				if (pe.GetMetadataNoCopy() is AssemblyMetadata am && am.GetAssembly() is PEAssembly pa) return pa.Identity;
				return null;
			}
			
			//TODO3: also for k dependencies that aren't in meta
		}
	}
	
	/// <summary>
	/// Gets the number of default references.
	/// Note: it may be less than DefaultReferences.Count (some default references can be removed).
	/// </summary>
	public int DefaultRefCount { get; private set; }
	
	/// <summary>
	/// Removes from <b>Refs</b> all matching a wildex. Used for meta noRef.
	/// </summary>
	internal void RemoveFromRefs(string wildx) {
		wildex x = wildx;
		for (int i = _refs.Count; --i >= 0;) {
			var r = _refs[i];
			if (x.Match(r.FilePath)) {
				_refs.RemoveAt(i);
				if (i < DefaultRefCount) {
					DefaultRefCount--;
					(_hsNoRef ??= new(StringComparer.OrdinalIgnoreCase)).Add(pathname.getNameNoExt(r.Display));
				}
			}
		}
	}
	HashSet<string> _hsNoRef; //names of refs removed by meta noRef
	
	/// <summary>
	/// Extracts path from compiler error message CS0009 and removes the reference from cache.
	/// </summary>
	/// <param name="errorMessage">"Metadata file '....dll' could not be opened ..."</param>
	public static void RemoveBadRefFromCache(string errorMessage) {
		if (errorMessage.RxMatch(@"'(.+?)'", 1, out string path))
			lock (s_cache) { s_cache.RemoveAll(v => v.path.Eqi(path)); }
	}
	
	//public static void CompactCache() => _MR.CompactCache();
	
#if DEBUG
	internal static void DebugPrintCachedRefs() {
		foreach (var v in s_cache) if (v.IsCached) print.it(v.name);
	}
#endif
	
	/// <summary>
	/// Gets XML documentation for an assembly.
	/// Uses a 2-column SQLite database auto-created from XML file by <see cref="Create"/>.
	/// Not XML file directly because it uses much memory. Unless the file is small or DB fails.
	/// </summary>
	class _DocumentationProvider : DocumentationProvider {
		protected sqlite _db;
		sqliteStatement _stat;
		
		/// <summary>
		/// Creates documentation provider for assembly <i>asmPath</i>.
		/// Returns null if its xml file does not exist.
		/// Returns _DocumentationProvider if xml file is big and found or successfully created and successfully loaded database for it.
		/// Else returns _XmlFileDocumentationProvider.
		/// </summary>
		public static DocumentationProvider Create(string asmPath) {
			if (s_d.TryGetValue(asmPath, out var dp)) return dp;
			
			var xmlPath = Path.ChangeExtension(asmPath, "xml");
			if (!filesystem.getProperties(xmlPath, out var px)) return null;
			
			if (px.Size >= 10_000) {
				var md5 = new Hash.MD5Context(); md5.Add(xmlPath.Lower());
				var dbPath = folders.ThisAppTemp + md5.Hash.ToString() + ".db";
				//CONSIDER: later delete oldest temp files.
				try {
					if (!filesystem.getProperties(dbPath, out var pd) || pd.LastWriteTimeUtc != px.LastWriteTimeUtc) {
						//Debug_.Print($"creating db: {asmPath}  ->  {dbPath}");
						filesystem.delete(dbPath);
						bool isAu = asmPath.Ends("\\Au.dll", true);
						using (var d = new sqlite(dbPath)) {
							using var trans = d.Transaction();
							d.Execute("CREATE TABLE doc (name TEXT PRIMARY KEY, xml TEXT)");
							using var statInsert = d.Statement("INSERT INTO doc VALUES (?, ?)");
							
							var xr = XmlUtil.LoadElem(xmlPath);
							foreach (var e in xr.Descendants("member")) {
								var name = e.Attr("name");
								
								if (isAu) {
									//remove <remarks>. Users can press F1 and read online.
									//	In editor sometimes it takes too much space. Badly formatted because optimized for markdown.
									e.Element("remarks")?.Remove();
									e.Element("example")?.Remove();
									
									//rejected. Too much info for "quick info". Can be distracting and annoying. Ok if would show icons instead. Or maybe 1-word links.
									//var er = e.Element("remarks");
									//var ee = e.Element("example");
									//if (er != null) {
									//	er.Value = ee != null
									//		? "More info in Remarks. Example available. Click and press F1."
									//		: "More info in Remarks. Click and press F1.";
									//	ee?.Remove();
									//} else {
									//	ee?.ReplaceWith(new XElement("remarks", "Example available. Click and press F1."));
									//}
								} else {
									e.Element("example")?.Remove();
								}
								
								using var reader = e.CreateReader();
								reader.MoveToContent();
								var xml = reader.ReadInnerXml();
								//print.it(name, xml);
								
								statInsert.BindAll(name, xml).Step();
								statInsert.Reset();
							}
							trans.Commit();
							d.Execute("VACUUM");
						}
						File.SetLastWriteTimeUtc(dbPath, px.LastWriteTimeUtc);
					}
					var db = new sqlite(dbPath, SLFlags.SQLITE_OPEN_READONLY); //never mind: we don't dispose it on process exit
					s_d[asmPath] = dp = new _DocumentationProvider { _db = db };
					return dp;
				}
				catch (Exception ex) { Debug_.Print(ex); }
			}
			//return XmlDocumentationProvider.CreateFromFile(xmlPath); //no, need XML with root element.
			return new _XmlFileDocumentationProvider(xmlPath);
		}
		static ConcurrentDictionary<string, _DocumentationProvider> s_d = new(StringComparer.OrdinalIgnoreCase);
		
		protected internal override string GetDocumentationForSymbol(string documentationMemberID, CultureInfo preferredCulture, CancellationToken cancellationToken = default) {
			if (_db != null) {
				lock (_db) { //sometimes not in main thread
					try {
						//print.it(documentationMemberID);
						_stat ??= _db.Statement("SELECT xml FROM doc WHERE name=?");
						//if (_stat.Bind(1, documentationMemberID).Step()) return _stat.GetText(0);
						if (_stat.Bind(1, documentationMemberID).Step()) return "<r>" + _stat.GetText(0) + "</r>";
						//Roslyn bug: inheritdoc does not work with XML fragment (without single root element).
						//	Also then trows/catches exception every time.
						//	But eg XmlDocumentationProvider.CreateFromFile provider returns fragment.
						//Debug_.Print(documentationMemberID);
					}
					catch (SLException ex) { Debug_.Print(ex); }
					finally { _stat?.Reset(); }
				}
			}
			return null;
		}
		
		public override bool Equals(object obj) {
			Debug_.PrintIf(obj != this, "Equals");
			return obj == this;
		}
		
		public override int GetHashCode() {
			Debug_.Print("GetHashCode");
			return 1;
		}
	}
	
	/// <summary>
	/// Gets XML documentation for .NET runtime assemblies.
	/// Uses a 2-column SQLite database created from XML files by script "Create .NET ref and doc databases.cs".
	/// Not XML files directly because it uses about 150 MB of memory.
	/// </summary>
	class _NetDocumentationProvider : _DocumentationProvider {
		HashSet<string> _refs;
		
		public _NetDocumentationProvider() {
			try {
				_db = EdDatabases.OpenDoc(); //never mind: we don't dispose it on process exit
				if (_db.Get(out string s, "SELECT xml FROM doc WHERE name='.'")) _refs = new HashSet<string>(s.Split('\n'));
			}
			catch (SLException ex) { Debug_.Print(ex); }
		}
		
		/// <summary>
		/// Returns true if the database contains XML doc of the reference assembly.
		/// </summary>
		/// <param name="refName">Like "mscorlib" or "System.Drawing" or "Au".</param>
		public bool HaveRef(string refName) => _refs?.Contains(refName) ?? false;
	}
	static readonly _NetDocumentationProvider s_netDocProvider;
	
	/// <summary>
	/// Gets XML documentation from XML file.
	/// </summary>
	sealed class _XmlFileDocumentationProvider : XmlDocumentationProvider {
		private readonly string _filePath;
		
		public _XmlFileDocumentationProvider(string filePath) {
			_filePath = filePath;
		}
		
		protected override Stream GetSourceStream(CancellationToken cancellationToken)
			=> new FileStream(_filePath, FileMode.Open, FileAccess.Read);
		
		protected override string GetDocumentationForSymbol(string documentationMemberID, CultureInfo preferredCulture, CancellationToken cancellationToken = default) {
			var s = base.GetDocumentationForSymbol(documentationMemberID, preferredCulture, cancellationToken);
			//print.it(documentationMemberID, s);
			if (!s.NE()) s = "<r>" + s + "</r>";
			return s;
		}
		
		public override bool Equals(object obj) => obj == this;
		
		public override int GetHashCode() => 1;
	} //copied from Roslyn's private FileBasedXmlDocumentationProvider (XmlDocumentationProvider.CreateFromFile returns it) and modified
}
