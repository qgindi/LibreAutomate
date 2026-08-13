static class GitBinaryFiles {
	const string c_zipFilename = "git-clone-LA-binary.7z";

	/// <summary>
	/// To call this when pushing LA to GitHub, add file `.git\hooks\pre-push`.
	/// <code><![CDATA[
	/// #!/bin/sh
	/// 
	/// "Other/BuildEvents/bin/Debug/BuildEvents.exe" "gitPrePushHook"
	/// exit $?
	/// ]]></code>
	/// </summary>
	public static int PrePushHook() {
		var laDir = Environment.CurrentDirectory + @"\_\";

		(string dir, string files, bool subdirs)[] enumFiles = [
				(null, "*.db", false),
				(null, "*MSTSCLib.dll", false),
				(null, "toc.json", false),
				(null, "toc-ai.yml", false),
				(null, "xrefmap.yml", false),
				("Roslyn", "*.dll", false),
				("Debugger", "**m *.dll||*.exe", true),
			];

		List<FEFile> aFiles = new();
		foreach (var v in enumFiles) {
			foreach (var f in filesystem.enumFiles(laDir + v.dir, v.files, (v.subdirs ? FEFlags.AllDescendants : 0) | FEFlags.UseRawPath)) {
				//if (f.Name.Like("doc-*.db")) continue; //BAD: changes too frequently
				aFiles.Add(f);
			}
		}
		//print.it(aFiles);

		bool update = true;
		var csvFile = laDir + "gitBinary.csv";
		if (filesystem.exists(csvFile)) {
			update = false;
			var t = csvTable.load(csvFile);
			var d = t.ToDictionary(ignoreCase: true, ignoreDuplicates: false);
			foreach (var f in aFiles) {
				var rel = f.FullPath[laDir.Length..];
				if (filesystem.getProperties(f.FullPath, out var p) && d.TryGetValue(rel, out var prevTime)) {
					if (f.LastWriteTimeUtc.ToString("s") == prevTime) continue;
				}
				update = true;
				break;
			}
		}

		if (update) {
			var d = dialog.showProgress(true, "Updating " + c_zipFilename, ".");
			d.Destroyed += k => { Environment.Exit(2); };

			var t = new csvTable();
			var b = new StringBuilder();
			foreach (var f in aFiles) {
				var rel = f.FullPath[laDir.Length..];
				t.AddRow(rel, f.LastWriteTimeUtc.ToString("s"));
				b.AppendLine(rel);
			}
			t.Save(csvFile);

			using var listFile = new TempFile();
			filesystem.saveText(listFile, b.ToString());

			d.Send.ChangeText2("Compressing...", false);
			var zipFile = folders.ThisAppTemp + c_zipFilename;
			filesystem.delete(zipFile);
			if (0 != run.console(out string s1, laDir + @"32\7za.exe", $@"a ""{zipFile}"" @""{listFile}""", laDir)) {
				print.it(s1);
				return 1;
			}
			//run.it(zipFile); dialog.show("zip OK");

			d.Send.ChangeText2("Uploading...", false);
			var rm = new GithubReleaseManager("LA-downloads");
			rm.Init("v1.0.0");
			rm.AddOrReplaceAsset(zipFile, "application/x-compressed");

			filesystem.delete(zipFile, FDFlags.CanFail);
		}

		return 0;
	}

	public static int Restore(string solutionDirBS, bool test = false) {
		var laDir = solutionDirBS + @"_\";
		if (test) {
			laDir += @"test\";
			filesystem.saveText(laDir + "gitBinaryRestore.csv", "restore");
		} else {
			if (laDir.Eqi(@"C:\code\au\_\") && Directory.Exists(@"C:\code-lib\roslyn")) return 0; //we at home
		}

		string restoreFile = laDir + "gitBinaryRestore.csv";
		if (filesystem.exists(restoreFile)) {
			var zipFile = folders.ThisAppTemp + c_zipFilename;
			if (!internet.http.Get("https://github.com/qgindi/LA-downloads/releases/download/v1.0.0/" + c_zipFilename, true).Download(zipFile)) return 1;

			int r = run.console(out string so, solutionDirBS + @"_\32\7za.exe", $@"x ""{zipFile}"" -aoa", laDir);
			if (r != 0) throw new AuException($"Failed to extract {c_zipFilename}. " + so);

			filesystem.delete(restoreFile);
		}

		return 0;
	}
}
