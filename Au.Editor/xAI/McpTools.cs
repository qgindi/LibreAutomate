using AI;
using System.Security.Authentication;

namespace LA;

class McpTools {
	readonly string _appDir = LA.folders2.La;
	
	public McpTools() {
		AiModel.ApiKeys = App.Settings.ai_ak;
	}
	
#if !true
	[Mcp("Echoes the input string")]
	public string Echo([Mcp("Any text")] string text, [Mcp("Make lowercase")] bool lowerCase = false) {
		return lowerCase ? text.ToLowerInvariant() : text;
	}
	
	[Mcp("Test enum")]
	public string TestEnum([Mcp("Color", EnumValues = "red|green|blue")] string color) {
		return color;
	}
	
	[Mcp("Test numbers")]
	public string TestNumbers([Mcp("Int")] int a, [Mcp("Long")] long b, [Mcp("Double")] double c) {
		return (a + b + c).ToS();
	}
	
	[Mcp("Test array")]
	public string TestArray([Mcp("Colors")] string[] a) {
		return string.Join('|', a);
	}
#else
	[Mcp("Gets the table of contents of the LibreAutomate documentation.")]
	public string get_la_docs_toc() {
		return filesystem.loadText($@"{_appDir}\toc-ai.yml");
	}
	
	[Mcp("Gets specified articles from the LibreAutomate documentation.")]
	public string get_la_docs(
		[Mcp("""
Names of articles.
Names of API articles are like "Namespace.Type.Member", "Namespace.Type", "Namespace.Type<T>.Member".
Names of non-API articles are like "[articles] name", "[editor] name", "[cookbook] name".
""")]
		string[] names
		) {
		if (App.Settings.ai_mcp_print) print.it($"<><lc yellowgreen><\a>MCP {nameof(get_la_docs)}:\n{string.Join('\n', names)}</\a><>");
		
		using var db = new sqlite($@"{_appDir}\doc-ai.db", SLFlags.SQLITE_OPEN_READONLY);
		using var sta = db.Statement($"SELECT name,text FROM doc WHERE name IN ({string.Join(',', names.Select(_ => "?"))})");
		sta.BindAll(names);
		var texts = new string[names.Length];
		while (sta.Step()) {
			string name = sta.GetText(0), text = sta.GetText(1);
			texts[names.IndexOf(name)] = text;
		}
		Debug_.PrintIf(texts.Contains(null));
		
		var sb = new StringBuilder();
		sb.AppendLine("Below are the articles you requested.").AppendLine(c_listFormatInfo);
		for (int i = 0; i < texts.Length; i++) {
			_AppendArticle(sb, i, names[i], texts[i]);
		}
		
		return sb.ToString();
	}
	
	void _AppendArticle(StringBuilder sb, int i, string name, string text) {
		sb.AppendFormat("\r\n--- {0}. {1} ---\r\n\r\n{2}\r\n", i + 1, name, text);
	}
	
	const string c_listFormatInfo = """

Article separator line format: `--- N. Name ---`, where N is 1-based index of the article.

Names of library API member articles are like `Namespace.Type.Member`. Namespaces: `Au`, `Au.More`, `Au.Triggers`, `Au.Types`.
Names of other articles have a prefix:
- `[articles]` - a library documentation article other than API member reference
- `[cookbook]` - a how-to article with code examples
- `[editor]` - LibreAutomate IDE documentation
""";
	
	[Mcp("""
Returns requested information about LibreAutomate API or IDE.
Whenever you need information about LibreAutomate API or IDE, create one or more short queries and call this tool.
This tool uses semantic search (AI embedding) to find the requested information.
If this tool receives a list of queries (one per line), it processes them paralelley (much faster) and optimizes results.
""")]
	public string find_la_docs(
		[Mcp("""
One or more short queries for semantic search, one per line.
IMPORTANT: each query must be focused on a **single** task or concept.
Keyword lists are discouraged. Use meaningful phrases.
Examples of good queries: "activate window", "send keys", "key names and syntax".
Example of a BAD query: "activate Chrome window send keys Ctrl+L LibreAutomate". What's bad here: it's not a meaningful single-task phrase but a soup of search keywords for two tasks; it includes names of windows and keys; redundant context info "LibreAutomate".
""")]
		string query,
		[Mcp("The `description` text of the `query` parameter of this tool, as specified in the MCP tool definition. This is to ensure you know how to use this parameter.")]
		string passPhrase
		) {
#if DEBUG
		print.it("--- passPhrase:");
		print.it(passPhrase);
		print.it("---");
#endif
		
		if (App.Settings.ai_mcp_print) print.it($"<><lc yellowgreen>MCP {nameof(find_la_docs)}:\r\n<><lc LemonChiffon><\a>{query}</\a><>");
		
		var lines = query.Lines(noEmpty: true);
		var aResults = new List<(string name, string text)>[lines.Length];
		
		var emModel = AiModel.GetModel<AiEmbeddingModel>(App.Settings.ai_modelEmbed, displayName: true) ?? throw new AuException("Missing settings in LibreAutomate. Please go to Options > AI and select models for documentation search.");
		var rrModel = AiModel.GetModel<AiRerankModel>(App.Settings.ai_modelRerank, displayName: true);
		if (rrModel == null) AiModel.RerankerModelWarning();
		
		try {
			var em = new Embeddings(emModel);
			var ems = em.GetDocsEmbeddings();
			
			Parallel.For(0, lines.Length, new ParallelOptions { MaxDegreeOfParallelism = 7 }, i => _QueryLine(lines[i], aResults[i] = []));
			
			void _QueryLine(string query, List<(string name, string text)> results) {
				int takePlus = Math.Min(20, query.Count(c => c is <= ' ' or ',' or '.' or ';' or '?'));
				int take = 15 + takePlus;
				
				var queryVector = em.CreateEmbedding(query);
				var topAll = em.GetTopMatches(queryVector, ems, rrModel == null ? 30 : 100);
				if (topAll.Count == 0) return;
				
				Dictionary<string, (float score, bool summary)> dTop = [];
				foreach (var v in topAll) {
					var name = v.f.name;
					bool isSum = name[0] == '+';
					if (isSum) name = name[1..];
					dTop.TryAdd(name, (v.score, isSum));
				}
				var aTop = dTop.Select(o => (name: o.Key, v: o.Value)).OrderByDescending(o => o.v.score).ToArray();
				
				string[] names = aTop.Select(o => o.name).ToArray();
				string[] texts = em.GetDocsTexts(names);
				
				if (rrModel != null) {
					var headers = rrModel.GetHeaders();
					var post = rrModel.GetPostData(query, texts);
					var j = rrModel.Post(post, headers).Json();
					//print.it(j.ToJsonString(new() { WriteIndented = true }));
					var ar = rrModel.GetResults(j).ToArray();
					float maxScore = ar[0].score, minScore = maxScore - (.3f + takePlus / 200f);
					//print.it(take, maxScore, minScore, maxScore - minScore);
					int i = 0;
					foreach (var v in ar) {
						if (i++ > take || v.score < minScore || v.score < .4f) break;
						results.Add((names[v.index], texts[v.index]));
						//print.it(v.score, names[v.index]);
					}
				} else {
					int i = 0;
					float minScore = aTop[0].v.score - (.2f + takePlus / 200f);
					foreach (var v in aTop) {
						if (v.v.score < minScore) break;
						results.Add((v.name, texts[i++]));
					}
				}
			}
		}
		catch (Exception ex) when (ex is InvalidCredentialException or AggregateException { InnerException: InvalidCredentialException }) {
			var api = emModel.api;
			throw new AuException($"Missing settings in LibreAutomate. Please go to Options > AI and set the API key for {api}.\nYou can create an API key in your account on the {api} website.");
		}
		
		Dictionary<string, string> dResults = [];
		for (int i = 0, n = aResults.Max(o => o.Count); i < n; i++) {
			foreach (var list in aResults) {
				if (i < list.Count) dResults.TryAdd(list[i].name, list[i].text);
			}
		}
		
		var sb = new StringBuilder();
		
		if (passPhrase.Length < 30 && lines.Length == 1 && query.Length > 30) {
			sb.AppendLine("Warning: the `passPhrase` argument is too short. Likely you (AI) ignore the description of the `query` parameter and use an incorrect query. Then the quality of results is bad.\r\n");
			//Some models pass exact descriptions. Some models pass summaries. Some smaller models pass eg the parameter name.
		}
		
		sb.AppendLine($"""
Found {dResults.Count} articles that likely contain the requested information.
{c_listFormatInfo}

List of article names:
""");
		
		sb.AppendLine();
		foreach (var (i, name) in dResults.Keys.Index()) {
			sb.AppendLine($"{i + 1}. {name}");
			if (App.Settings.ai_mcp_print) print.it($"{i + 1}. {name}");
		}
		
		sb.AppendLine("\r\nArticles:");
		
		foreach (var (i, v) in dResults.Index()) {
			_AppendArticle(sb, i, v.Key, v.Value);
		}
		
		//print.it(sb); print.scrollToTop();
		
		return sb.ToString();
	}
#endif
}
