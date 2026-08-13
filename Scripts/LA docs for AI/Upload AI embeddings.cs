/// Uploads an AI embeddings storage file to https://github.com/qgindi/LA-downloads/releases.
/// Currently used only for icons embeddings.
/// To run, click link printed by Embeddings._GetEmbeddings. Can't run directly because need a hash etc.
/// It prints the link when created new embeddings. To print always when UI-searching, temporarily enable the `//emFile.PrintUploadIfAtHome`.

/*/ c Ed util shared.cs; c GithubReleaseManager.cs; /*/

string file = args[0], zipName = args[1];
//print.it(file, zipName);

//if (!dialog.showOkCancel("Upload AI embedding vectors")) return;

string zipFile = folders.ThisAppTemp + zipName;
try {
	print.it("Compressing...");
	if (!LA.SevenZip.Compress(out var errors, zipFile, file)) { print.it(errors); return; }
	
	//run.selectInExplorer(zipFile);
	
	print.it("Uploading...");
	var m = new GithubReleaseManager("LA-downloads");
	m.Init("v1.0.0");
	m.AddOrReplaceAsset(zipFile, "application/x-compressed");
	
	print.it($"<>Uploaded: {zipName} to <link>https://github.com/qgindi/LA-downloads/releases<>");
}
finally { filesystem.delete(zipFile, FDFlags.CanFail); }
