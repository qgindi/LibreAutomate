#if false
using System.Text.Json.Serialization;

namespace AI;

record class AiSettings : JSettings {
	public static readonly string File = folders.ThisAppDocuments + @".settings\AI.json";
	
	public static AiSettings Instance { get; } = Load<AiSettings>(File)._Loaded();
	
	AiSettings _Loaded() {
		if (embeddingsModels is null) {
			EmbeddingsModel mistral = new() {
				name = "Mistral", provider = "Mistral",
				url = "https://api.mistral.ai/v1/embeddings",
				authHeader = "Authorization: Bearer $apiKey",
				postJsonQuery = """{"model": "mistral-embed", "input": $input}""", //info: dimensions 1024; tested: with `, "output_dimension": 1024` error bad request.
				resultItemPath = "data/*/embedding",
				resultTokensPath = "usage/total_tokens",
				maxTokens = 32000, minScore = .65
			};
			EmbeddingsModel openai = new() {
				name = "OpenAI", provider = "OpenAI",
				url = "https://api.openai.com/v1/embeddings",
				authHeader = "Authorization: Bearer $apiKey",
				postJsonQuery = """{"model": "text-embedding-3-small", "input": $input, "dimensions": 1024, "encoding_format": "base64"}""",
				resultItemPath = "data/*/embedding",
				resultTokensPath = "usage/total_tokens",
				maxTokens = 300000, minScore = .25
			};
			EmbeddingsModel gemini = new() {
				name = "Gemini", provider = "Gemini",
				url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:batchEmbedContents",
				authHeader = "x-goog-api-key: $apiKey",
				postJsonQuery = """{"requests": $input}""",
				postJsonQueryItem = """{"model": "models/gemini-embedding-001", "content": {"parts":[{"text": $input}]}, "outputDimensionality": 1024, "task_type": "RETRIEVAL_QUERY"}""",
				postJsonDocumentItem = """{"model": "models/gemini-embedding-001", "content": {"parts":[{"text": $input}]}, "outputDimensionality": 1024, "task_type": "RETRIEVAL_DOCUMENT"}""",
				resultItemPath = "embeddings/*/values",
				maxTokens = 8000, minScore = .6
			}; //docs: default outputDimensionality 3072, else need to normalize; tested: don't need (same after normalization).
			embeddingsModels = [mistral, openai, gemini];
		}
		
		return this;
	}
	
	public List<EmbeddingsModel> embeddingsModels;
	
	/// <value>Valid index in <b>embeddingsModels</b>, or -1 if <b>embeddingsModels</b> empty (unlikely). Uses <c>App.Settings.ai_embeddingsModel</c>.</value>
	public int CurrentEmbeddingsModel { get { int i = App.Settings.ai_embeddingsModel; return (uint)i < embeddingsModels.Count ? i : embeddingsModels.Count > 0 ? 0 : -1; } }
	
	public record class EmbeddingsModel {
		public string name, provider, url, authHeader, postJsonQuery, postJsonDocument, postJsonQueryItem, postJsonDocumentItem, resultItemPath, resultTokensPath;
		public int maxTokens = 100000, maxInputs = 1000;
		public double minScore = .1;
		
		[JsonIgnore]
		public AiProviderSettings Provider { get; private set; }
		
		/// <exception cref="ArgumentException"></exception>
		public (string json, string authHeader) GetPostData(ICollection<string> input, bool isQuery) {
			//validate settings
			if (postJsonQuery == "") postJsonQuery = null;
			if (postJsonDocument == "") postJsonDocument = null;
			if (postJsonQueryItem == "") postJsonQueryItem = null;
			if (postJsonDocumentItem == "") postJsonDocumentItem = null;
			if (resultTokensPath == "") resultTokensPath = null;
			if (name.NE() || provider.NE() || url.NE() || url.NE() || authHeader.NE() || (postJsonQuery ?? postJsonDocument) is null || resultItemPath.NE()) _Error("some settings missing");
			maxTokens = maxTokens < 1 ? 100000 : Math.Clamp(maxTokens, 2000, 300000);
			if (maxInputs < 1) maxInputs = 1000;
			minScore = Math.Clamp(minScore, .1, .9);
			Provider ??= App.Settings.ai_providers.FirstOrDefault(o => o.name.Eqi(provider));
			if (Provider is null) _Error($"provider {provider} not found");
			
			string auth = Provider.GetAuthHeader(authHeader);
			
			//in JSON template replace $input with the input array
			var json = isQuery ? (postJsonQuery ?? postJsonDocument) : (postJsonDocument ?? postJsonQuery);
			s_rx1 ??= new(@"""\s*:\s*(\$input)\b");
			if (!s_rx1.Match(json, 1, out RXGroup g)) _Error("JSON template does not contain $input: " + json);
			var b = new StringBuilder();
			
			//if there is a JSON item template, replace its $input with input array items. Used when the JSON array are objects, not strings, eg Gemini.
			string s1 = "\"", s2 = "\"";
			var jsonItem = isQuery ? (postJsonQueryItem ?? postJsonDocumentItem) : (postJsonDocumentItem ?? postJsonQueryItem);
			if (jsonItem != null) {
				if (!s_rx1.Match(jsonItem, 1, out RXGroup k)) _Error("JSON item template does not contain $input: " + json);
				s1 = jsonItem[..k.Start] + "\"";
				s2 = "\"" + jsonItem[k.End..];
			}
			
			foreach (var v in input) b.Append(b.Length == 0 ? "[" : ", ").Append(s1).Append(v.Escape()).Append(s2);
			b.Append("]");
			json = json.ReplaceAt(g.Start, g.Length, b.ToString());
			
			return (json, auth);
			
			void _Error(string s) => throw new ArgumentException($"Error in settings of AI embeddings model {name}: {s}");
		}
		static regexp s_rx1;
	}
}

record class AiProviderSettings(string name) {
	public string apiKey;
	
	public static List<AiProviderSettings> DefaultProviders => [new("Mistral") { apiKey = "%API_MISTRAL%" }, new("OpenAI") { apiKey = "%API_OPENAI%" }, new("Gemini") { apiKey = "%API_GOOGLE%" }];
	
	/// <exception cref="ArgumentException"></exception>
	public string GetAuthHeader(string authHeaderTemplate) {
		int i = authHeaderTemplate.Find("$apiKey"); if (i < 0) _Error("no $apiKey in authHeader");
		var s = apiKey; if (s.NE()) _Error("no API key");
		if (s.Like("%?*%")) {
			s = Environment.GetEnvironmentVariable(s[1..^1]);
			if (s is null) _Error("API key environment variable not found");
		}
		return authHeaderTemplate.ReplaceAt(i, 7, s);
		
		void _Error(string s) => throw new ArgumentException($"Error in settings of AI provider {name}: {s}");
	}
};
#endif
