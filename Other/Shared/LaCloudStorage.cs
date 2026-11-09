/*/ c LaCloudAzure.cs; c LaCloudGithub.cs; /*/
#define AZURE //use Azure Blob Storage: https://qgindi.blob.core.windows.net/libreautomate. Else GitHub Releases: https://github.com/qgindi/LA-downloads/releases.

namespace LA.Shared;

static class LaCloudStorage {
	/// <summary>
	/// Uploads one or more 7z or/and zip files to the LA's Azure Blob Storage (account "qgindi", container "libreautomate") or GitHub Releases (account "qgindi", repo "LA-downloads").
	/// </summary>
	/// <param name="cloudDirectory">Directory of the Azure blob (Azure), or tag name of the GitHub release.</param>
	/// <param name="files">Paths of local files.</param>
	public static void Upload(string cloudDirectory, string[] files) {
#if AZURE
		var up = new LaCloudAzure();
		foreach (var f in files) {
			up.Upload("libreautomate", cloudDirectory, f);
		}
#else
		var gh = new LaCloudGithub("LA-downloads", cloudDirectory);
		foreach (var f in files) {
			gh.AddOrReplaceAsset(f);
		}
#endif
	}
	
	public static string GetDownloadUrl(string cloudDirectory, string cloudFilename) {
#if AZURE
		return $"https://qgindi.blob.core.windows.net/libreautomate/" + LaCloudAzure.UrlEscapeBlobName($"{cloudDirectory}/{cloudFilename}");
#else
		return = $"https://github.com/qgindi/LA-downloads/releases/download/{cloudDirectory}/{cloudFilename}";
#endif
	}
	
	/// <summary>
	/// Downloads a 7z or/and zip file from the LA's Azure Blob Storage (account "qgindi", container "libreautomate") or GitHub Releases (account "qgindi", repo "LA-downloads").
	/// </summary>
	/// <param name="cloudDirectory">Directory of the Azure blob (Azure), or tag name of the GitHub release.</param>
	/// <param name="cloudFilename">Filename in the cloud storage.</param>
	/// <param name="localFile">Local file path. Will replace if exists. Will create directory if need.</param>
	/// <param name="returnFalseIfNotFound">If "404 not found", return <c>false</c> instead of exception.</param>
	/// <returns>If <i>returnFalseIfNotFound</i>, returns <c>false</c> if status code "404 not found".</returns>
	/// <exception cref="Exception"></exception>
	/// <inheritdoc cref="ExtInternet.DownloadAsync(System.Net.Http.HttpResponseMessage, string, Action{ProgressArgs}, CancellationToken)"/>
	public static bool Download(string cloudDirectory, string cloudFilename, string localFile, Action<ProgressArgs> progress = null, CancellationToken cancel = default, string progressText1 = null, bool returnFalseIfNotFound = false) {
		var url = GetDownloadUrl(cloudDirectory, cloudFilename);
		var r = internet.http.Get(url, true);
		if (returnFalseIfNotFound && r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
		r.Download(localFile, progress, cancel, progressText1);
		return true;
	}
	
	/// <summary>
	/// Gets names of files in Azure Blob Storage (account "qgindi", container "libreautomate") or GitHub Releases (account "qgindi", repo "LA-downloads").
	/// </summary>
	/// <param name="cloudDirectory">Directory of the Azure blob (Azure), or tag name of the GitHub release.</param>
	public static string[] List(string cloudDirectory) {
#if AZURE
		return LaCloudAzure.List("https://qgindi.blob.core.windows.net/libreautomate", cloudDirectory)
			.Where(o => o[^1] != '/')
			.ToArray();
#else
		var gh = new LaCloudGithub("LA-downloads", cloudDirectory);
		return gh.Assets.Select(o => (string)o["name"]).ToArray();
#endif
	}
}
