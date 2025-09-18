namespace LA;

static class TestInternal {
	static HashSet<(string user, string target)> _hsCompiler = new(), _hsCi = new(), _hsRefs = new();
	static bool _compiling, _findingRefs;
	
	static TestInternal() {
		//Used by everything except 'find references'.
		//Called from any thread.
		RoslynMod.TestInternal.IsInternalsVisibleCallback = static (string thisName, string toName) => {
			//print.it("IsInternalsVisibleCallback", thisName, toName);
			if (_compiling) return _hsCompiler.Contains((toName, thisName));
			if (_findingRefs) return _hsRefs.Contains((toName, thisName));
			lock (_hsCi) return _hsCi.Contains((toName, thisName));
		};
		
		//Used by 'find references'. Not by 'find implementations'.
		//Called from any thread.
		RoslynMod.TestInternal.AppendInternalsVisibleCallback += static (string thisName, HashSet<string> toNames) => {
			foreach (var v in _hsRefs) if (v.target == thisName) toNames.Add(v.user);
			//print.it("AppendInternalsVisibleCallback", thisName, toNames);
		};
	}
	
	public static void CompilerStart(string asmName, string[] testInternals) {
		//print.it("CompilerStart", asmName, testInternals);
		Debug.Assert(!_compiling);
		foreach (var v in testInternals) _hsCompiler.Add((asmName, v));
		_compiling = true;
	}
	
	public static void CompilerEnd() {
		//print.it("CompilerEnd");
		_compiling = false;
		_hsCompiler.Clear();
	}
	
	public static void CiAdd(string asmName, string[] testInternals) {
		//print.it("CiAdd", asmName, testInternals);
		lock (_hsCi) {
			foreach (var v in testInternals) _hsCi.Add((asmName, v));
		}
	}
	
	public static void CiClear() {
		//print.it("CiClear");
		lock (_hsCi) _hsCi.Clear();
	}
	
	public static void RefsStart() {
		//print.it("RefsStart", _hsCi);
		_hsRefs.Clear();
		_hsRefs.UnionWith(_hsCi);
		_findingRefs = true;
	}
	
	public static void RefsEnd() {
		//print.it("RefsEnd");
		_findingRefs = false;
		_hsRefs.Clear();
	}
	
	public static void RefsAdd(string asmName, string[] testInternals) {
		//print.it("RefsAdd", asmName, testInternals);
		foreach (var v in testInternals) _hsRefs.Add((asmName, v));
	}
}
