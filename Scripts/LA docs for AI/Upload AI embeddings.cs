/// If need, uploads an AI embeddings storage file to the LA cloud storage.

/*/ testInternal Au,Au.Editor; c AI script common.cs; c LaCloudStorage.cs; /*/
using AI;
if (script.testing) print.clear();
print.it("<><lc yellowgreen>Ensuring AI embedding downloads are up to date. Wait until DONE.<>");

AiModel.ApiKeys = App.Settings.ai_ak;

var existing = LA.Shared.LaCloudStorage.List("AI-embedding");

foreach (var model in (AiEmbeddingModel[])[new ModelVoyageEmbed(), new ModelGeminiEmbed(), new ModelMistralEmbed(), new ModelVoyageEmbedM()]) {
	print.it(model.GetType());
	var em = new Embeddings(model);
	if (model.isCompact) {
		em.GetIconsEmbeddings(true);
	} else {
		var ev = em.GetDocsEmbeddings();
		if (!ev.Any(o => o.name[0] == '+')) throw new Exception("<>Error: first need to <script AI summaries.cs>add summaries<>.");
	}
	var (file, zipName) = em.VectorFileInfoForScript;
	//print.it(file, zipName);
	if (zipName is null) throw new Exception("Vectors of this model are not downloadable. See `TryGetZipName` in `AI search.cs`.");
	
	if (existing.Any(o => o == zipName)) continue;
	
	string zipFile = folders.ThisAppTemp + zipName;
	try {
		print.it($"Compressing...");
		if (!LA.SevenZip.Compress(out var errors, zipFile, file)) throw new Exception(errors);
		//run.selectInExplorer(zipFile);
		
		print.it("Uploading...");
		LA.Shared.LaCloudStorage.Upload("AI-embedding", [zipFile]);
	}
	finally { filesystem.delete(zipFile, FDFlags.CanFail); }
}
print.it("""
<>DONE. Now optionally <script GitHub push LA docs for AI.cs>upload md files for AI<>.
""");
