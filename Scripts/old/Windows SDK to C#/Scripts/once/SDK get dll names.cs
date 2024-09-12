/// Gets dll function names and their dll file names from .lib and .dll files. Also gets ordinal or alias if need.
/// Saves in "C:\code\au\Other\Api\DllMap.txt". Format:
/// FuncX dllX.dll
/// FuncZ dllX.dll|#ordinal
/// FuncY dllX.dll|FuncNameInDll

/*/ c ..\SdkUtil.cs; /*/

print.clear();

string libDir = @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.26100.0\um\x64";
string sysDir = (string)folders.System + "\\";
object[] libs = {
	(libDir, "*.lib"),
	sysDir+"hhctrl.ocx",
	sysDir+"ieframe.dll",
	sysDir+"pla.dll",
	sysDir+"msvcrt.dll",
	sysDir+"ntdll.dll",
	sysDir+"kernelbase.dll",
};

string r = _CreateDllMap(2 | 0);
filesystem.saveText(@"C:\code\au\Other\Api\DllMap.txt", r);


//Extracts dll function names and their dll file names from lib or/and dll file(s).
//Returns the list of extracted functions-dlls.
//Note: Shows warnings "no exports" etc. It's normal when processing all lib or dll in a folder.
//
//flags: 1 include subfolders, 2 include system dlls for libs, 4 use cached dumpbin results if exists
string _CreateDllMap(int flags) {
	string dumpbin = SdkUtil.GetVisualStudioToolsFolderPath(true) + @"\dumpbin.exe";
	string sLib = null, sDll = null;
	
	//cache dumpbin results when developing post-dumpbin code
	string cacheLib = folders.ThisAppTemp + "_CreateDllMap lib.txt";
	string cacheDll = folders.ThisAppTemp + "_CreateDllMap dll.txt";
	if (0 != (flags & 4) && filesystem.exists(cacheLib) && filesystem.exists(cacheDll)) {
		sLib = File.ReadAllText(cacheLib);
		sDll = File.ReadAllText(cacheDll);
		print.it("NOTE: using cached dumpbin results (flag 4).");
	} else {
		var files = new List<string>();
		foreach (var o in libs) {
			if (o is (string dir, string fn)) {
				files.AddRange(filesystem.enumFiles(dir, fn, 0 != (flags & 1) ? FEFlags.AllDescendants : 0).Where(f => 0 == f.Name.Starts(false, "api-ms-", "ntstc_")).Select(o => o.FullPath));
			} else {
				files.Add(o as string);
			}
		}
		
		var dDllOfLib = new ConcurrentDictionary<string, string>(); //at first process lib, and add dll to this
		ConcurrentBag<string> bLib = new(), bDll = new();
		
		Parallel.ForEach(files, path => {
			path = path.Lower();
			var fn = pathname.getName(path);
			if (fn.Starts("api-ms-")) return;
			if (!fn.Ends(".lib")) { dDllOfLib.TryAdd(path, fn); return; }
			if (0 != fn.Like(false, "ntstc_*", "mspbase.lib", "strmbase.lib", "uuid.lib", "dnslib.lib", "netlib.lib", "dnsrpc.lib", "clfsmgmt.lib", "icu*.lib", "lz32.lib")) return; //large, no dll func, or no public/useful dll func
			//print.it(fn);
			
			/*
			/HEADERS - functions and dll names; slow.
			/EXPORTS - just function names.
			*/
			int e = run.console(out var s, dumpbin, $"/HEADERS \"{path}\"");
			if (e != 0) {
				if (e == -1073741515) {
					run.it(dumpbin); //show "mspdb80.dll is missing"
					throw new AuException($"dumpbin.exe failed, {e}");
				}
				print.warning($"dumpbin.exe failed, {e}, {fn}");
				return;
			}
			
			if (!s.RxFindAll(@"(?m)^  DLL name *: (.+)\R(?s).+?\R\R", out var a)) {
				print.it($"no exports in {fn}");
			} else {
				string prev = null;
				foreach (var m in a) {
					var dll = m[1].Value;
					if (dll.Contains("-ms-")) {
						//print.it(dll);
						continue;
					}
					bLib.Add(m.Value);
					_SearchAdd(dll.Lower());
				}
				
				if (0 != (flags & 2)) _SearchAdd(fn.ReplaceAt(^3.., "dll"));
				
				void _SearchAdd(string fn) {
					if (fn == prev) return; prev = fn;
					//print.it(fn);
					var s = filesystem.searchPath(fn);
					if (s != null) dDllOfLib.TryAdd(s.Lower(), fn);
					//else print.it(fn);
				}
			}
		});
		
		Parallel.ForEach(dDllOfLib, kv => {
			var (path, fn) = kv;
			int e = run.console(out var s, dumpbin, $"/EXPORTS \"{path}\"");
			if (e != 0) {
				print.warning($"dumpbin.exe failed, {e}, {fn}");
				return;
			}
			
			if (!s.RxMatch(@"\bordinal hint RVA.+\R(?s).+?\R\R", 0, out string s1)) print.it($"no exports in {fn}");
			bDll.Add($"DLL: {fn}\r\n{s1}");
		});
		
		sLib = string.Join("\r\n", bLib.OrderBy(o => o, StringComparer.OrdinalIgnoreCase)); //OrderBy because Parallel makes random
		sDll = string.Join("\r\n", bDll.OrderBy(o => o, StringComparer.OrdinalIgnoreCase));
		filesystem.saveText(cacheLib, sLib);
		filesystem.saveText(cacheDll, sDll);
	}
	
	/* list item formats:
	symbol dll
	symbol dll|#ordinal
	symbol dll|funcNameInDll
	*/
	
	Dictionary<string, string> d = new(), dDll = new();
	var _cd = _CreateChooseDllMap();
	
	//LIB
	
	var rx = """
(?m)^  DLL name *: (.+)
  Symbol name *: (\w.+)
  Type *: code
  Name type *: (.+)
  (?:Ordinal *: (.+)|Hint .+\R  Name *: (.+))\R\R
""";
	if (!sLib.RxFindAll(rx, out var a)) throw new AuException();
	foreach (var m in a) {
		string DLL = m[1].Value.Lower(), sym = m[2].Value, name = m[5].Value, ord = m[4].Value;
		if (sym[0] == '_') sym = sym.RxReplace(@"^_([^@]+?)", "$1");
		string v;
		if (!ord.NE()) {
			if (DLL == "mapi32.dll") v = DLL; //two ord, one of which incorrect
			else v = $"{DLL}|#{ord.ToInt()}";
		} else if (!name.NE()) {
			if (name == sym) v = DLL;
			else v = $"{DLL}|{name}";
		} else throw new AuException();
		
		if (!d.TryGetValue(sym, out var vv)) d.Add(sym, v);
		else if (_ChooseDll(vv, v, sym)) d[sym] = v;
	}
	
	//DLL
	
	rx = @"(?m)(?>^DLL: (.+)\Rordinal hint RVA .+\R\R)(?s)(.+?)\R\R";
	if (!sDll.RxFindAll(rx, out a)) throw new AuException();
	foreach (var m in a) {
		string DLL = m[1].Value.Lower();
		if (DLL == "hhsetup.dll") continue; //all func "?..."
		if (DLL == "profapi.dll") continue; //fails
		if (!m[2].Value.RxFindAll(@"(?m)^      (.{20})(\w+)", out var b)) { print.warning($"failed, {DLL}"); continue; }
		foreach (var k in b) {
			var sym = k[2].Value;
			if (d.ContainsKey(sym)) continue;
			if (sym.Starts("Dll")) continue; //DllGetClassObject etc
			if (!dDll.TryGetValue(sym, out var vv)) dDll.Add(sym, DLL);
			else if (_ChooseDll(vv, DLL, sym)) dDll[sym] = DLL;
		}
	}
	
	print.it($"Found {d.Count + dDll.Count} functions."); //29934
	
	return string.Join("\r\n", d.Select(o => o.Key + " " + o.Value).Concat(dDll.Select(o => o.Key + " " + o.Value)).OrderBy(o => o).Append(""));
	
	bool _ChooseDll(string dllOld, string dllNew, string sym) {
		if (dllOld != dllNew) {
			foreach (var (s1, s2) in _cd) {
				if(dllOld.Like(s1) && dllNew.Like(s2)) return true;
				if(dllOld.Like(s2) && dllNew.Like(s1)) return false;
			}
			
			//print.it($"{sym,-40}:  (\"{dllNew}\", \"{dllOld}\"),");
		}
		return false;
	}
	
	static (string s1, string s2)[] _CreateChooseDllMap() {
		return new (string, string)[] {
			("bluetoothapis.dll", "bthprops.cpl"),
			("chakra.dll", "jscript9.dll"),
			("d3d11.dll", "gdi32.dll"),
			("dsparse.dll", "ntdsapi.dll"),
			("evr.dll", "mfplat.dll"),
			("iprop.dll", "ole32.dll"),
			("kernel32.dll", "normaliz.dll"),
			("kernelbase.dll", "kernel32.dll"),
			("kernelbase.dll", "msvcrt.dll"),
			("kernelbase.dll", "version.dll"),
			("kernelbase.dll", "ntdll.dll"),
			("kernel32.dll", "ntdll.dll"),
			("*", "netapi32.dll"),
			("ncrypt.dll", "bcrypt.dll"),
			("ntdll.dll", "msvcrt.dll"),
			("*", "secur32.dll"),
			("shlwapi.dll|#*", "shlwapi.dll"),
			("spoolss.dll", "winspool.drv*"),
			("winsta.dll", "wtsapi32.dll"),
			("irprops.cpl", "bthprops.cpl"),
			("wintrust.dll", "crypt32.dll"),
			("imagehlp.dll", "dbghelp.dll"),
			("mfcore.dll", "mf.dll"),
			("mf.dll", "mfplat.dll"),
			("mscorsn.dll", "mscoree.dll"),
			("mstask.dll", "mstask.dll|#*"),
			("olepro32.dll*", "oleaut32.dll*"),
			("olecli32*", "ole32.dll"),
			("shfolder.dll", "shell32.dll"),
			("wsock32.dll*", "*"),
			("setupapi.dll", "cfgmgr32.dll"),
			("wmi.dll", "advapi32.dll"),
			("certcli.dll", "certca.dll"),
			("vertdll.dll", "*"),
			("comctl32.dll", "comctl32.dll|#*"),
			("msi.dll", "msi.dll|#*"),
			("oledlg.dll", "oledlg.dll|#*"),
			("shell32.dll|#*", "shell32.dll"),
			("schannel.dll", "secur32.dll"),
			("secur32.dll", "sspicli.dll"),
			("uxtheme.dll|#*", "uxtheme.dll"),
			("wer.dll", "advapi32.dll"),
			("cryptbase.dll", "advapi32.dll"),
			("comctl32.dll|_TrackMouseEvent", "user32.dll"),
			("sporder.dll", "ws2_32.dll"),
			("*", "xaudio*"),
			("*", "xinput*"),
			("*", "d3d*"),
		};
	}
}
