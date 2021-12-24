using System.Linq;
using Au.Compiler;

//SHOULDDO: preserve order.

class MetaCommentsParser
{
	FileNode _fn;
	public string role, ifRunning, uac, bit32,
		optimize, warningLevel, noWarnings, testInternal, define, preBuild, postBuild,
		outputPath, console, icon, manifest, /*resFile,*/ sign, xmlDoc;
	List<string> _r, _pr, _c, _resource, _com;

	public List<string> r => _r ??= new List<string>();
	public List<string> pr => _pr ??= new List<string>();
	public List<string> c => _c ??= new List<string>();
	public List<string> resource => _resource ??= new List<string>();
	public List<string> com => _com ??= new List<string>();

	bool _multiline;

	public MetaCommentsParser(FileNode f) : this(f.GetText()) { _fn = f; }

	public MetaCommentsParser(string code) {
		var meta = MetaComments.FindMetaComments(code);
		if (meta.end == 0) return;
		MetaRange = meta;
		foreach (var t in MetaComments.EnumOptions(code, meta)) _ParseOption(t.Name(), t.Value());
		_multiline = code[meta.start..meta.end].Contains('\n');
	}

	public (int start, int end) MetaRange { get; private set; }

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
		case "testInternal": testInternal = value; break;
		case "define": define = value; break;
		case "preBuild": preBuild = value; break;
		case "postBuild": postBuild = value; break;
		case "console": console = value; break;
		case "icon": icon = value; break;
		case "manifest": manifest = value; break;
		//case "resFile": resFile = value; break;
		case "sign": sign = value; break;
		case "xmlDoc": xmlDoc = value; break;
		case "r": r.Add(value); break;
		case "com": com.Add(value); break;
		case "pr": pr.Add(value); break;
		case "c": c.Add(value); break;
		case "resource": resource.Add(value); break;
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
		b.Append(prepend).Append(_multiline ? "/*/\r\n" : "/*/ ");
		_Append("role", role);

		_Append("ifRunning", ifRunning);
		_Append("uac", uac);

		_Append("optimize", optimize);
		_Append("define", define);
		_Append("warningLevel", warningLevel);
		_Append("noWarnings", noWarnings);
		_Append("testInternal", testInternal);
		_Append("preBuild", preBuild, true);
		_Append("postBuild", postBuild, true);

		_Append("outputPath", outputPath);
		_Append("icon", icon, true);
		_Append("manifest", manifest, true);
		//_Append("resFile", resFile, true);
		_Append("sign", sign, true);
		_Append("bit32", bit32);
		_Append("console", console);
		_Append("xmlDoc", xmlDoc);

		_AppendList("r", _r);
		_AppendList("com", _com, true);
		_AppendList("pr", _pr);
		_AppendList("c", _c, true);
		_AppendList("resource", _resource, true);

		if (b.Length <= 5) return "";
		b.Append("/*/");
		b.Append(append);
		return b.ToString();

		void _Append(string name, string value, bool relativePath = false) {
			if (value != null) {
				if (relativePath && dir != null && value.Starts(dir, true)) value = value[dir.Length..];
				b.Append(name).Append(' ').Append(value).Append(_multiline ? ";\r\n" : "; ");
			}
		}

		void _AppendList(string name, List<string> a, bool relativePath = false) {
			if (a != null) foreach (var v in a.Distinct()) _Append(name, v, relativePath);
		}
	}
}
