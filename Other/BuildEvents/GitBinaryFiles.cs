using System.IO.Compression;
using System.Runtime.ConstrainedExecution;

static class GitBinaryFiles {
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
				("Roslyn", "*.dll", false),
				("Debugger", "**m *.dll||*.exe", true),
				(@"..\Other\BuildEvents\.tools", "ResourceHacker.exe", false),
			];

		List<FEFile> aFiles = new();
		foreach (var v in enumFiles) {
			foreach (var f in filesystem.enumFiles(laDir + v.dir, v.files, (v.subdirs ? FEFlags.AllDescendants : 0) | FEFlags.UseRawPath)) {
				if (f.Name.Eqi("cookbook.db")) continue; //changes too frequently. And don't need; will use files from ..\Cookbook dir.
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
			var d = dialog.showProgress(true, "Updating LaBinary.7z", ".");
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
			var zipFile = folders.ThisAppTemp + "LaBinary.7z";
			filesystem.delete(zipFile);
			if (0 != run.console(out string s1, laDir + @"32\7za.exe", $@"a ""{zipFile}"" @""{listFile}""", laDir)) {
				print.it(s1);
				return 1;
			}
			//run.it(zipFile); dialog.show("zip OK");

			d.Send.ChangeText2("Uploading...", false);
			Sftp.UploadToLA("domains/libreautomate.com/public_html/download", zipFile);

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
			var zipFile = folders.ThisAppTemp + "LaBinary.7z";
			internet.http.Get("https://www.libreautomate.com/download/LaBinary.7z", true).Download(zipFile);

			int r = run.console(out string so, solutionDirBS + @"_\32\7za.exe", $@"x ""{zipFile}"" -aoa", laDir);
			if (r != 0) throw new AuException("Failed to extract LaBinary.7z. " + so);

			filesystem.moveTo(laDir + "ResourceHacker.exe", solutionDirBS + @"Other\BuildEvents\.tools");

			filesystem.delete(restoreFile);
		}

		return 0;
	}
}
