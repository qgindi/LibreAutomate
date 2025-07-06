using System.Windows.Controls;

/// <summary>
/// Downloads and extracts a compressed file. With progress.
/// To extract uses the LA's installed 7za.exe.
/// Must be disposed (it deletes a sentinel file if succeeded).
/// </summary>
sealed class Downloader : IDisposable {
	string _dirExtract;
	bool _deleteOldFiles;
	string _sentinel;
	
	/// <summary>
	/// Prepares to extract files in given directory.
	/// </summary>
	/// <param name="dirExtract">Path of directory where to extract files (later). This func creates new empty directory if does not exist; else just detects whether can create files there.</param>
	/// <param name="failedWarningPrefix">Prefix text for <see cref="print.warning"/> to use when fails.</param>
	/// <returns><c>false</c> if can't install in this directory (prints warning).</returns>
	public bool PrepareDirectory(string dirExtract, string failedWarningPrefix = "<>Warning: ") {
		_dirExtract = null;
		_deleteOldFiles = false;
		try {
			if (filesystem.exists(dirExtract, true).Directory) {
				using (File.Create(dirExtract + @"\~", 0, FileOptions.DeleteOnClose)) { } //can create files there?
				_deleteOldFiles = true;
			} else {
				filesystem.createDirectory(dirExtract);
			}
			_dirExtract = dirExtract;
		}
		catch (Exception e1) {
			print.warning(e1, failedWarningPrefix);
			if (!uacInfo.isAdmin) print.it("\tRestart this program as administrator.");
			return false;
		}
		return true;
	}
	
	/// <summary>
	/// Downloads compressed file from given URL and extracts to the directory specified in a call to <see cref="PrepareDirectory"/>.
	/// </summary>
	/// <param name="url">Direct download URL. Must end with <c>".zip"</c> or <c>".7z"</c>.</param>
	/// <param name="progress">This func will set text like <c>"Downloading, 20%"</c>. Can be null.</param>
	/// <param name="ct"></param>
	/// <param name="progressText">Progress text. While downloading, this function appends ", N%".</param>
	/// <param name="failedWarningPrefix">Prefix text for <see cref="print.warning"/> to use when fails.</param>
	/// <returns><c>false</c> if failed (prints warning); <c>null</c> if canceled (no warning).</returns>
	public async Task<bool?> DownloadAndExtract(string url, TextBlock progress, CancellationToken ct, string progressText = "Downloading", string failedWarningPrefix = "<>Warning: ") {
		if (_dirExtract is null) throw new InvalidOperationException();
		var ext = pathname.getExtension(url).Lower();
		if (!(ext is ".zip" or ".7z")) throw new NotSupportedException("Bad file type");
		
		try {
			progress?.Visibility = System.Windows.Visibility.Visible;
			using var zip = new TempFile(ext);
			
			progress?.Text = progressText + ". Connecting...";
			var rm = await Task.Run(() => internet.http.Get(url, dontWait: true)); //not GetAsync because it blocks for 7 s on my VMWare Win7
			if (!await rm.DownloadAsync(zip, p => { progress?.Text = $"{progressText}, {p.Percent}%"; }, ct)) return null;
			
			progress?.Text = "Extracting";
			await Task.Run(() => {
				var aDel = _deleteOldFiles ? Directory.GetFileSystemEntries(_dirExtract) : null;
				
				string sentinel = _dirExtract + c_sentinel;
				filesystem.saveText(sentinel, "");
				
				if (_deleteOldFiles) filesystem.delete(aDel);
				
				var sevenzip = folders.ThisAppBS + @"32\7za.exe";
				if (0 != run.console(out var s, sevenzip, $@"x -aoa -o""{_dirExtract}"" ""{zip}""")) throw new AuException($"*extract files. {s}");
				
				_sentinel = sentinel;
			});
		}
		catch (Exception e1) {
			print.warning(e1, failedWarningPrefix);
			return false;
		}
		finally {
			progress?.Text = "";
		}
		return true;
	}
	
	const string c_sentinel = @"\.la-extract-sentinel";
	
	public void Dispose() {
		if (_sentinel != null) filesystem.delete(_sentinel);
	}
	
	/// <summary>
	/// Returns <c>true</c> if <see cref="DownloadAndExtract"/> was killed during the "delete existing files and extract" operation. Ie the directory content is invalid.
	/// </summary>
	/// <param name="dirExtract">The same directory as used with <b>DownloadAndExtract</b>.</param>
	public static bool IsDirectoryInvalid(string dirExtract) => filesystem.exists(dirExtract + c_sentinel, true);
}
