/// Creates/updates summaries in doc-ai.db. They will be used in semantic search, to improve results.
/// How: Takes texts from doc-ai.db, generates summaries using Gemini chat model, and stores in an intermediate DB. Finally copies the summaries to doc-ai.db.
/// Generates only for articles where the summary is missing or need to update. Skips short articles.

/*/
define SCRIPT
testInternal Au,Au.Editor
c AI script common.cs
/*/

using System.Security.Cryptography;
using System.Text.Json;
using AI;

//print.clear();
AiModel.ApiKeys = App.Settings.ai_ak;

#if !true
UserDefinedAiModels.Add();
//var model = new ModelGeminiChat("gemini-2.5-flash"); //good, compact; 3-10 s; 30/250 cents
var model = new ModelGeminiChat("gemini-2.5-flash-lite"); //good, compact, but often too compact, especially for non-API; 1.5 s; 10/40 cents

//var model = new ModelOpenaiChat("gpt-5-nano"); //
//var model = new ModelOpenaiChat("gpt-5-nano") { reasoning = "minimal" }; //good, less compact, sometimes too many details; 3-6 s; 5/40 cents
//var model = new ModelOpenaiChat("gpt-5-mini"); //
//var model = new ModelOpenaiChat("gpt-5-mini") { reasoning = "minimal" }; //good, less compact, sometimes too many details; 3-7 s; 25/200 cents
//var model = new ModelOpenaiChat("gpt-5"); //
//var model = new ModelOpenaiChat("gpt-4.1-nano"); //good format but sometimes with some understanding errors; 3 s; 10/40 cents
//var model = new ModelOpenaiChat("gpt-4.1-mini"); //good; 3 s; 40/160 cents

//var model = new ModelDeepseekChat(); //good, compact; 7.5 s

//var model = new ModelClaudeChat("claude-sonnet-4-0"); //good but minimal count; 3.5 s; with "prefer more" instruction bad (all) and slow
//var model = new ModelClaudeChat("claude-opus-4-1"); //bad; 9 s, sometimes hangs

//var model = new ModelMistralChat("mistral-medium-latest"); //like gpt-5-nano but even more details, 10 s

perf.first();
_Test();
perf.nw();
//print.scrollToTop();

void _Test() {
	string s;
	
	s = @"C:\Temp\Au\markdown\articles\[articles] About the automation library (files, namespaces).md";
	s = @"C:\Temp\Au\markdown\articles\[articles] UI element issues.md";
	s = @"C:\Temp\Au\markdown\articles\[articles] UAC.md";
	s = @"C:\Temp\Au\markdown\articles\[articles] Key names and operators.md";
	s = @"C:\Temp\Au\markdown\articles\[articles] Wildcard expression.md";
	s = @"C:\Temp\Au\markdown\articles\[articles] Output tags.md";
	//s = @"";
	
	s = @"C:\Temp\Au\markdown\editor\[editor] Code editor.md";
	s = @"C:\Temp\Au\markdown\editor\[editor] Command line.md";
	s = @"C:\Temp\Au\markdown\editor\[editor] Class files, projects.md";
	s = @"C:\Temp\Au\markdown\editor\[editor] Compared with QM.md";
	s = @"C:\Temp\Au\markdown\editor\[editor] Debugger.md";
	s = @"C:\Temp\Au\markdown\editor\[editor] File properties.md";
	//s = @"";
	
	s = @"C:\Temp\Au\markdown\cookbook\[cookbook] Autotext triggers, expand text.md";
	s = @"C:\Temp\Au\markdown\cookbook\[cookbook] Callback functions, lambda, delegate, event.md";
	s = @"C:\Temp\Au\markdown\cookbook\[cookbook] Clipboard copy, paste, set-get text etc.md";
	s = @"C:\Temp\Au\markdown\cookbook\[cookbook] Dialog - add elements, show, get values.md";
	s = @"C:\Temp\Au\markdown\cookbook\[cookbook] Http post web form, JSON.md";
	//s = @"";
	
	s = @"C:\Temp\Au\markdown\api\Au\dialog\Au.dialog.show.md";
	s = @"C:\Temp\Au\markdown\api\Au\dialog\Au.dialog.md";
	s = @"C:\Temp\Au\markdown\api\Au\clipboard\Au.clipboard.copyData.md";
	s = @"C:\Temp\Au\markdown\api\Au\clipboard\Au.clipboard.paste.md";
	s = @"C:\Temp\Au\markdown\api\Au\clipboard\Au.clipboard.text.md";
	s = @"C:\Temp\Au\markdown\api\Au\clipboard\Au.clipboard.clear.md";
	s = @"C:\Temp\Au\markdown\api\Au\clipboard\Au.clipboard.md";
	s = @"C:\Temp\Au\markdown\api\Au\consoleProcess\Au.consoleProcess.consoleProcess.md";
	s = @"C:\Temp\Au\markdown\api\Au\consoleProcess\Au.consoleProcess.Prompt.md";
	s = @"C:\Temp\Au\markdown\api\Au\mouse\Au.mouse.click.md";
	s = @"C:\Temp\Au\markdown\api\Au\keys\Au.keys.send.md";
	//s = @"";
	
	string name = pathname.getNameNoExt(s);
	string text = filesystem.loadText(s);
	
	_ProcessText(ref text);
	_ShowTextInOutput2(text);
	if (text.Length < 400) {
		print.it("SKIPPED", text.Length, text);
		return;
	}
	
	s = _GenerateSummary(name, text, model, true);
	print.it(s);
}

#else

var model = new ModelGeminiChat("gemini-2.5-flash-lite");

var dbFile = folders.ThisAppBS + "doc-ai.db";
using (var dbDoc = new sqlite(folders.ThisApp + "doc-ai.db")) {
	using var staDocSelect = dbDoc.Statement("SELECT name,text FROM doc");
	
	string tempDbPath = folders.LocalAppData + @"LibreAutomate\_dev\doc-ai-summary.db";
	using var dbTemp = new sqlite(tempDbPath);
	dbTemp.Execute("CREATE TABLE IF NOT EXISTS doc (name TEXT PRIMARY KEY, summary TEXT, hash BLOB)");
	using var staTempInsert = dbTemp.Statement("INSERT OR REPLACE INTO doc VALUES (?, ?, ?)");
	using var transTemp = dbTemp.Transaction(sqlOfDispose: "COMMIT");
	
	Dictionary<string, byte[]> dHash = [];
	using (var staTempSelectHash = dbTemp.Statement("SELECT name,hash FROM doc")) {
		while (staTempSelectHash.Step()) {
			dHash.Add(staTempSelectHash.GetText(0), staTempSelectHash.GetArray<byte>(1));
		}
	}
	
	while (staDocSelect.Step()) {
		string name = staDocSelect.GetText(0), text = staDocSelect.GetText(1);
		
		_ProcessText(ref text);
		if (text.Length < 400) {
			//print.it("SKIPPED", text.Length, text);
			continue;
		}
		
		//summary exists and is up to date?
		var hash = SHA256.HashData(text.ToUTF8());
		if (dHash.TryGetValue(name, out var hash2) && hash2.SequenceEqual(hash)) continue;
		
		print.it(name);
		perf.first();
		var sum = _GenerateSummary(name, text, model, false);
		perf.nw();
		//print.it(sum);
		
		staTempInsert.BindAll(name, sum, hash);
		staTempInsert.Step();
		staTempInsert.Reset();
		
		//if (!dialog.showYesNo("Continue?")) return;
		if (keys.isCapsLock) return;
	}
	
	//if (!dialog.showOkCancel("Finished", $"Copy temp summaries to doc-ai.db?")) return;
	using var staDocUpdate = dbDoc.Statement("UPDATE doc SET summary = ? WHERE name = ?");
	using var transDoc = dbDoc.Transaction();
	using var staTempSelect = dbTemp.Statement("SELECT name,summary FROM doc");
	while (staTempSelect.Step()) {
		string name = staTempSelect.GetText(0), sum = staTempSelect.GetText(1);
		staDocUpdate.BindAll(sum, name);
		staDocUpdate.Step();
		staDocUpdate.Reset();
	}
	transDoc.Commit();
	dbDoc.Execute("VACUUM");
}
filesystem.copyTo(dbFile, @"C:\code\Au.Editor", FIfExists.Delete);

print.it("""
<>DONE. Added summaries to doc-ai.db.
	Now <script Upload AI embeddings.cs>upload vectors to GitHub<>
""");

#endif

static string _GenerateSummary(string name, string text, AiChatModel model, bool test) {
	bool isApi = !name.Starts('[');
	//var modelVerbosity = model.model switch { "gemini-2.5-flash-lite" => ModelVerbosity.Low, "gemini-2.5-flash" => ModelVerbosity.Medium, _ => ModelVerbosity.High };
	
	string system;
	const string c_footer = "The generated text will be used **only** to genarate AI embeddings for semantic search. Don't need details, because the original text will be included in the search too (it contains details).";
	if (isApi) {
		system = $$"""
The user message is a LibreAutomate API member documentation.
Need to prepare it for semantic search using AI embedding. Here is how:

1. Generate a concise list of documented features. Or a 1-sentence summary, if the documentation is short.
2. Then append a short comma-separated list of likely user search phrases and important tokens that users might query.

Don't include details, such as parameter names, default values, examples, notes, warnings, tips, alternatives.
{{c_footer}}

---

Output example for method `clipboard.paste(string text, string html = null, OKey options = null, KHotkey hotkey = default, int timeoutMS = 0)`:

- Pastes text or HTML into the focused app using the clipboard
- Sets clipboard data, sends keys `Ctrl+V`, waits until the target app gets clipboard data, and restores old clipboard data
- You can specify a custom hotkey, timeout, and other options

Search keywords: clipboard, paste, text, html, Ctrl+V, restore clipboard
""";
	} else if (name.Starts(@"[cookbook]")) {
		system = $$"""
The user message is a LibreAutomate cookbook article.
Need to prepare it for semantic search using AI embedding. Here is how:

1. Generate a summary.
2. Then append a short comma-separated list of likely user search phrases and important tokens that users might query.

{{c_footer}}
""";
	} else {
		system = $$"""
The user message is a LibreAutomate documentation article.
Need to prepare it for semantic search using AI embedding. Here is how:

1. Generate a summary. One or two sentences.
2. Then append a concise list of documented features, without details or examples. Skip this if there is nothing that is not already in the generated summary.
3. Then append a short comma-separated list of likely user search phrases and important tokens that users might query.

{{c_footer}}
""";
		if (name is "[articles] Key names and operators") system += "\r\nIMPORTANT: also add the list of keys from the \"Named keys\" section.";
	}
	//print.it(system);return;
	
	var post = model.GetPostData(system, [new(ACMRole.user, text)], 0.2);
	var r = model.Post(post, model.GetHeaders());
	var json = r.Json();
	//print.it(json.ToJsonString(new(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
	return model.GetAnswer(json).text;
}

static void _ProcessText(ref string s) {
	//s = s.RxReplace(@"\R##(?:## Examples|### Exceptions|# See Also)(\R(?!##).*)+", "");
	//s = s.RxReplace(@"^(# (?:Method|Operator|Constructor|Indexer).+\R(\R.+)+\R)\R```\R.+\R```\R", "$1", 1);
	s = s.RxReplace(@"\R###+ (?:Exceptions|See [Aa]lso)(\R(?!##).*)+", "");
}

//static void _ShowTextInOutput2(string s) {
//	var w1 = wnd.find("Output2");
//	if (!w1.Is0) w1.SetText(s);
//}

//enum ModelVerbosity { Low, Medium, High }
