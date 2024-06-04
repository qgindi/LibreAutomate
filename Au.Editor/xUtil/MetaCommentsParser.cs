using Au.Compiler;

//SHOULDDO: preserve order.

/// <seealso cref="MetaComments.FindMetaComments"/>
/// <seealso cref="MetaComments.EnumOptions"/>
class MetaCommentsParser {
	FileNode _fn;
	public string role, ifRunning, uac, bit32,
		optimize, warningLevel, noWarnings, nullable, testInternal, define, preBuild, postBuild,
		outputPath, console, icon, manifest, sign, xmlDoc, miscFlags, noRef, startFaster;
	List<string> _pr, _r, _com, _nuget, _c, _resource, _file;
	
	public List<string> pr => _pr ??= new();
	public List<string> r => _r ??= new();
	public List<string> com => _com ??= new();
	public List<string> nuget => _nuget ??= new();
	public List<string> c => _c ??= new();
	public List<string> resource => _resource ??= new();
	public List<string> file => _file ??= new();
	
	public MetaCommentsParser(FileNode f) : this(f.GetCurrentText(out var s) ? s : "") { _fn = f; }
	
	public MetaCommentsParser(string code) {
		var meta = MetaComments.FindMetaComments(code);
		if (meta.end == 0) return;
		MetaRange = meta;
		foreach (var t in MetaComments.EnumOptions(code, meta)) _ParseOption(t.Name, t.Value.ToString());
		Multiline = code[meta.start..meta.end].Contains('\n');
	}
	
	public StartEnd MetaRange { get; private set; }
	
	void _ParseOption(string name, string value) {
		switch (name) {
		case "role": role = value; break;
		case "outputPath": outputPath = value; break;
		case "ifRunning": ifRunning = value; break;
		case "uac": uac = value; break;
		case "bit32": bit32 = value; break;
		case "optimize": optimize = value; break;
		case "warningLevel": warningLevel = value; break;
		case "noWarnings": noWarnings = value; break;
		case "nullable": nullable = value; break;
		case "testInternal": testInternal = value; break;
		case "define": define = value; break;
		case "preBuild": preBuild = value; break;
		case "postBuild": postBuild = value; break;
		case "console": console = value; break;
		case "icon": icon = value; break;
		case "manifest": manifest = value; break;
		case "sign": sign = value; break;
		case "xmlDoc": xmlDoc = value; break;
		case "miscFlags": miscFlags = value; break;
		case "noRef": noRef = value; break;
		case "startFaster": startFaster = value; break;
		case "pr": pr.Add(value); break;
		case "r": r.Add(value); break;
		case "com": com.Add(value); break;
		case "nuget": nuget.Add(value); break;
		case "c": c.Add(value); break;
		case "resource": resource.Add(value); break;
		case "file": file.Add(value); break;
		}
	}
	
	/// <summary>
	/// Formats metacomments string "/*/ ... /*/".
	/// Returns "" if there are no options.
	/// </summary>
	public string Format(string prepend, string append) {
		//prepare to make relative paths
		string dir = null;
		if (_fn != null) {
			dir = _fn.ItemPath;
			int i = dir.LastIndexOf('\\') + 1;
			if (i > 1) dir = dir.Remove(i); else dir = null;
		}
		
		var b = new StringBuilder();
		b.Append(prepend).Append(Multiline ? "/*/\r\n" : "/*/ ");
		_Append("role", role); //must be the first
		
		_Append("ifRunning", ifRunning);
		_Append("uac", uac);
		
		_Append("optimize", optimize);
		_Append("define", define);
		_Append("warningLevel", warningLevel);
		_Append("noWarnings", noWarnings);
		_Append("nullable", nullable);
		_Append("testInternal", testInternal);
		_Append("preBuild", preBuild, true);
		_Append("postBuild", postBuild, true);
		
		_Append("outputPath", outputPath);
		_Append("icon", icon, true);
		_Append("manifest", manifest, true);
		_Append("sign", sign, true);
		_Append("bit32", bit32);
		_Append("console", console);
		_Append("xmlDoc", xmlDoc);
		_Append("miscFlags", miscFlags);
		_Append("noRef", noRef);
		_Append("startFaster", startFaster);
		
		_AppendList("pr", _pr);
		_AppendList("r", _r);
		_AppendList("com", _com, true);
		_AppendList("nuget", _nuget);
		_AppendList("c", _c, true);
		_AppendList("resource", _resource, true);
		_AppendList("file", _file, true);
		
		if (b.Length <= 5) return "";
		b.Append("/*/");
		b.Append(append);
		return b.ToString();
		
		void _Append(string name, string value, bool relativePath = false) {
			if (value != null) {
				if (relativePath && dir != null && value.Starts(dir, true)) value = value[dir.Length..];
				b.Append(name).Append(' ').Append(value).Append(Multiline ? ";\r\n" : "; ");
			}
		}
		
		void _AppendList(string name, List<string> a, bool relativePath = false) {
			if (a != null) foreach (var v in a.Distinct()) _Append(name, v, relativePath);
		}
	}
	
	public bool Multiline { get; set; }
	
	/// <summary>
	/// Adds or updates meta comments in the active document.
	/// </summary>
	/// <returns>true if changed documnt text.</returns>
	public bool Apply() {
		var doc = Panels.Editor.ActiveDoc;
		var f = doc.EFile;
		var code = doc.aaaText;
		var meta = MetaComments.FindMetaComments(code);
		string prepend = null, append = null;
		if (meta.end == 0) {
			if (code.RxMatch(@"^(///.*\R)+(?=\R|$)", 0, out RXGroup g)) { //description
				meta = new(g.End, g.End);
				prepend = "\r\n";
			}
			append = (f.IsScript && code.Eq(meta.end, "//.")) ? " " : "\r\n";
		}
		var s = Format(prepend, append);
		
		if (s.Length == 0) {
			if (meta.end == 0) return false;
			while (meta.end < code.Length && code[meta.end] <= ' ') meta.end++;
		} else if (s.Length == meta.end - meta.start) {
			if (s == doc.aaaRangeText(true, meta.start, meta.end)) return false; //did not change
		}
		
		doc.EReplaceTextGently(meta.start, meta.end, s);
		return true;
	}
	
	/// <summary>
	/// Merges meta options into this from <i>m</i>.
	/// If an option already exists in this: if it's a list (like c or define), adds list items that don't already exist; else replaces the value.
	/// </summary>
	public void Merge(MetaCommentsParser m) {
		_List(ref _pr, m._pr);
		_List(ref _r, m._r);
		_List(ref _com, m._com);
		_List(ref _nuget, m._nuget);
		_List(ref _c, m._c);
		_List(ref _resource, m._resource);
		_List(ref _file, m._file);
		
		_CommaList(ref define, m.define);
		_CommaList(ref noWarnings, m.noWarnings);
		_CommaList(ref testInternal, m.testInternal);
		
		_Single(ref bit32, m.bit32);
		_Single(ref console, m.console);
		_Single(ref icon, m.icon);
		_Single(ref ifRunning, m.ifRunning);
		_Single(ref manifest, m.manifest);
		_Single(ref miscFlags, m.miscFlags);
		_Single(ref noRef, m.noRef);
		_Single(ref nullable, m.nullable);
		_Single(ref optimize, m.optimize);
		_Single(ref outputPath, m.outputPath);
		_Single(ref postBuild, m.postBuild);
		_Single(ref preBuild, m.preBuild);
		_Single(ref role, m.role);
		_Single(ref sign, m.sign);
		_Single(ref startFaster, m.startFaster);
		_Single(ref uac, m.uac);
		_Single(ref warningLevel, m.warningLevel);
		_Single(ref xmlDoc, m.xmlDoc);
		
		static void _List(ref List<string> a, List<string> b) {
			if (b.NE_()) return;
			a ??= new();
			foreach (var s in b) {
				if (!a.Contains(s, StringComparer.OrdinalIgnoreCase)) a.Add(s);
			}
		}
		
		static void _CommaList(ref string a, string b, bool ignoreCase = false) {
			if (b.NE()) return;
			if (a.NE()) a = b;
			else {
				var hs = a.Split_(',', ignoreCase);
				foreach (var s in b.Split_(',')) hs.Add(s);
				a = string.Join(',', hs);
			}
		}
		
		static void _Single(ref string a, string b) {
			if (b.NE()) return;
			a = b;
		}
	}
}
