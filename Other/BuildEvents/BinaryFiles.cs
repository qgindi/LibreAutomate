
using LA.Shared;

class BinaryFiles(string repoDir) {
	public void SetHooks() {
		var relPath = Path.GetRelativePath(repoDir, process.thisExePath);

		var preCommit = repoDir / @".git\hooks\pre-commit";
		if (!filesystem.exists(preCommit, true)) filesystem.saveText(preCommit, $"""
#!/bin/sh

"{relPath}" "gitPreCommitHook"
exit $?
""");

		var prePush = repoDir / @".git\hooks\pre-push";
		if (!filesystem.exists(prePush, true)) filesystem.saveText(prePush, $"""
#!/bin/sh

"{relPath}" "gitPrePushHook"
exit $?
""");
	}

	_Group[] _Groups => [
		/*
Format-versioned, large, rarely changes. The newest version can be used by older LA versions if the same package format (same filenames, same DB tables, etc).
File like `unversioned-1.7z`. The 1 is package format version.
		*/
		new("unversioned", "1", """
icons.db
winapi.db
*MSTSCLib.dll
"""),
		/*
Format-versioned, small, frequently changes. The newest version can be used by older LA versions if the same package format (same filenames, same DB tables, etc).
File like `doc-1.7z`. The 1 is package format version.
		*/
		new("doc", "1", """
doc-ai.db
doc-html.db
toc-ai.yml
toc.json
xrefmap.yml
"""),
		/*
One 7z file for each .NET major version.
File like `net-10.7z`.
		*/
		new("net", Environment.Version.ToString(2), """
doc.db
ref.db
32/apphost.exe
64/apphost.exe
64/ARM/apphost.exe
"""),
		/*
Upload new 7z file whenever the files change. Normally 1-3 times/year.
File like `fork-2026-10-15.7z`
		*/
		new("fork", null, """
Debugger/**
Roslyn/*.dll
"""),
		];

	public int PreCommitHook() {
		print.it("pre-commit hook");

		var g = new GitRepo(repoDir) { Print = true };

		//exit if nothing to commit
		if (g.gitOut("status --porcelain").Count == 0) return 0;

		foreach (var group in _Groups) {
			group.Init(repoDir);

			if (group.GetGroupInfoIfFilesChanged() is not string versionData) continue;

			filesystem.saveText(group.GroupInfoFile, versionData);
			filesystem.setAttributes(group.GroupInfoFile, FileAttributes.Hidden, false);

			try {
				g.git($"add {group.GroupInfoFileRel}");
			}
			catch {
				filesystem.delete(group.GroupInfoFile);
				return 1;
			}
		}

		return 0;
	}

	public int PrePushHook() {
		//print.it("pre-push hook");

		//exit if nothing to push
		if (null == Environment.GetEnvironmentVariable("test-BinaryFiles")) {
			string commits = Console.In.ReadToEnd();
			if (commits.NE()) return 0;
		}

		foreach (var group in _Groups) {
			group.Init(repoDir);

			//is the group info file updated?
			var fileAttr = File.GetAttributes(group.GroupInfoFile);
			if (fileAttr.Has(FileAttributes.Hidden)) continue;

			group.Upload();

			File.SetAttributes(group.GroupInfoFile, fileAttr | FileAttributes.Hidden);
		}

		return 0;
	}

	public int Restore(bool test = false) {
		string laDirBS = repoDir / @"_\";
		if (test) {
			laDirBS += @"test\";
			filesystem.createDirectory(laDirBS);
		}

		bool once = false;
		foreach (var group in _Groups) {
			group.Init(repoDir);

			//is any file missing or possibly modified?
			var v = _GroupInfo.Load(group.GroupInfoFile);
			var date = v.GetDateTime();
			if (!v.files.Any(f => File.GetLastWriteTimeUtc(laDirBS + f) != date)) continue;

			//is really missing/modified, of just changed the last-write-time?
			var hash = filesystem.more.HashFiles_(v.files, laDirBS);

			if (v.hash != hash) {
				if (!once) {
					once = true;
					print.it("Getting app files that are not generated when building this solution");
				}
				group.Restore(laDirBS, v);
			}

			foreach (var f in v.files) {
				File.SetLastWriteTimeUtc(laDirBS + f, date);
			}
		}

		if (once) print.it("    Done");

		return 0;
	}

	record class _GroupInfo(string date, string hash, string[] files) {
		public static _GroupInfo Load(string file) {
			var a = File.ReadAllLines(file);
			return new(a[0], a[1], a.RemoveAt(0, 2));
		}

		public DateTime GetDateTime() => DateTime.ParseExact(date, "yyyy-MM-dd", null, 0);
	}

	record class _Group(string groupName, string packageVersion, string filePatterns) {
		string _laDirBS;

		public void Init(string repoDir) {
			_laDirBS = repoDir / @"_\";
			GroupInfoFileRel = $@".github\restore\{groupName}";
			GroupInfoFile = repoDir / GroupInfoFileRel;
		}

		public string GroupInfoFileRel { get; private set; }

		public string GroupInfoFile { get; private set; }

		public string GetGroupInfoIfFilesChanged() {
			var files = filesystem.list(_laDirBS, new(filePatterns.Lines(noEmpty: true)), FLFlags.Recurse).FilePaths;
			var hash = filesystem.more.HashFiles_(files, _laDirBS);

			if (filesystem.exists(GroupInfoFile)) {
				var a = File.ReadAllLines(GroupInfoFile);
				if (a[1] == hash) return null;
			}

			return $"""
{DateTime.UtcNow:yyyy-MM-dd}
{hash}
{string.Join("\r\n", files.Select(o => o[_laDirBS.Length..]))}
""";
		}

		public void Upload() {
			var v = _GroupInfo.Load(GroupInfoFile);

			using var listFile = new TempFile();
			filesystem.saveText(listFile, string.Join('\n', v.files));

			string zipFilename = _ZipFilename(v), zipFile = folders.ThisAppTemp + zipFilename;
			filesystem.delete(zipFile);

			var d = dialog.showProgress(true, "Updating LA binary files", zipFilename, footer: "Compressing...");
			try {
				if (0 != run.console(out string s1, _laDirBS + @"32\7za.exe", $@"a ""{zipFile}"" @""{listFile}""", _laDirBS))
					throw new AuException(s1);
				//run.it(zipFile); dialog.show("zip OK");

				d.Send.ChangeFooterText("Uploading...", false);
				LaCloudStorage.Upload("bin", [zipFile]);
			}
			finally {
				d.Send.Close();
				filesystem.delete(zipFile, FDFlags.CanFail);
			}
		}

		string _ZipFilename(_GroupInfo v) => $"{groupName}-{packageVersion ?? v.date}.7z";

		public void Restore(string laDirBS, _GroupInfo v) {
			string zipName = _ZipFilename(v);
			print.it($"    Downloading {zipName} from cloud");
			using TempFile tempFile = new(".7z");
			LaCloudStorage.Download("bin", zipName, tempFile, o => { });

			print.it($"    Extracting {zipName}");
			int r = run.console(out string so, _laDirBS / @"32\7za.exe", $@"x ""{tempFile}"" -aoa", laDirBS);
			if (r != 0) throw new AuException($"Failed to extract {zipName}. " + so);
		}
	}
}
