/// Gets GUID names/data from .lib files.
/// Saves in "C:\code\au\Other\Api\GuidMap.txt".

/*/ c ..\SdkUtil.cs; /*/

print.clear();

string libDir = @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.26100.0\um\x64";
(string, string)[] libs = {
	(libDir, "*.lib"),
};

string r = _CreateGuidMap(0);
//print.it(r);
filesystem.saveText(@"C:\code\au\Other\Api\GuidMap.txt", r);


//Extracts GUID names/data from .lib files.
//Returns the list of extracted GUID names/data.
//
//flags: 1 include subfolders
string _CreateGuidMap(int flags) {
	string dumpbin = SdkUtil.GetVisualStudioToolsFolderPath(true) + @"\dumpbin.exe";
	
	var files = new List<FEFile>();
	foreach (var (dir, fn) in libs) {
		files.AddRange(filesystem.enumFiles(dir, fn, 0 != (flags & 1) ? FEFlags.AllDescendants : 0).Where(f => 0 == f.Name.Starts(false, "api-ms-", "ntstc_")));
	}
	
	var bag = new ConcurrentBag<string>();
	Parallel.ForEach(files, f => {
		var fn = f.Name;
		int e = run.console(out var s, dumpbin, $"/HEADERS /RAWDATA \"{f.FullPath}\"");
		if (e != 0) {
			if (e == -1073741515) {
				run.it(dumpbin); //show "mspdb80.dll is missing"
				throw new AuException($"dumpbin.exe failed, {e}");
			}
			print.warning($"dumpbin.exe failed, {e}, {fn}");
			return;
		}
		
		if (!s.Contains("COMDAT; sym=")) return;
		//print.it($"<><lc green>{fn}<>");
		//print.it(s);
		
		var rx = @"(?m) +COMDAT; sym= *((?:[A-Z]|guid)\w+)\R(?: .+\R)+\RRAW DATA #\w+\R  \w+: (\w\w) (\w\w) (\w\w) (\w\w) (\w\w) (\w\w) (\w\w) (\w\w) (\w\w) (\w\w) (\w\w) (\w\w) (\w\w) (\w\w) (\w\w) (\w\w)  .*\R\R";
		foreach (var m in s.RxFindAll(rx)) {
			var sym = m[1].Value;
			if (sym.Starts("IID___")) continue;
			//if(sym.Length>50) print.it(sym);
			bag.Add($"{sym} 0x{m[5]}{m[4]}{m[3]}{m[2]}, 0x{m[7]}{m[6]}, 0x{m[9]}{m[8]}, 0x{m[10]}, 0x{m[11]}, 0x{m[12]}, 0x{m[13]}, 0x{m[14]}, 0x{m[15]}, 0x{m[16]}, 0x{m[17]}");
		}
	});
	
	print.it($"Found {bag.Count} GUIDs."); //28081
	
	return string.Join("\r\n", bag.OrderBy(o => o).Append("")); //OrderBy because Parallel makes random
}
