using System.Text.Json;
using System.Security.Authentication;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace AI;

record class AiSettings {
	public static bool test;//TODO
	
	//public AiSettings() { } //for deserialization of user-defined model
	
	AiSettings(int u_) { //for defaults
		embedding = [];
		embedding.CollectionChanged += (_, e) => {
			if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace) {
				int i = e.NewStartingIndex;
				if (!embedding[i].Validate()) embedding.RemoveAt(i);
			}
		};
		
		embedding.Add(new() { //TODO: remove?
			name = "Mistral text",
			provider = "Mistral",
			url = "https://api.mistral.ai/v1/embeddings",
			json = """{"model": "mistral-embed", "input": $input}""", //info: dimensions 1024; does not support output_dimension and output_dtype
			resultItemPath = "data/*/embedding",
			resultTokensPath = "usage/total_tokens",
			maxTokens = 32000, maxInputs = 256, //both are tested max allowed
			minScore = .6f
		});
		
		embedding.Add(new() {
			name = "Mistral code",
			provider = "Mistral",
			url = "https://api.mistral.ai/v1/embeddings",
			json = """{"model": "codestral-embed", "input": $input, "output_dimension": 1536}""",
			resultItemPath = "data/*/embedding",
			resultTokensPath = "usage/total_tokens",
			maxTokens = 32000, maxInputs = 256, //both are tested max allowed
			minScore = .4f
		});
		embedding.Add(embedding[^1] with {
			name = "Mistral for icons",
			json = """{"model": "codestral-embed", "input": $input, "output_dimension": 384, "output_dtype": "int8"}""",
		});
		embedding.Add(new() {
			name = "OpenAI",
			provider = "OpenAI",
			url = "https://api.openai.com/v1/embeddings",
			json = """{"model": "text-embedding-3-small", "input": $input, "dimensions": 1536, "encoding_format": "base64"}""",
			resultItemPath = "data/*/embedding",
			resultTokensPath = "usage/total_tokens",
			maxTokens = 100000, //300000, but make smaller for better progress display
			maxInputs = 2048, //tested
		});
		embedding.Add(embedding[^1] with {
			name = "OpenAI for icons",
			json = """{"model": "text-embedding-3-small", "input": $input, "dimensions": 384, "encoding_format": "base64"}""",
		});
		embedding.Add(new() {
			name = "Gemini",
			provider = "Gemini",
			url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:batchEmbedContents",
			auth = "x-goog-api-key: $apiKey",
			json = """{"requests": $input}""",
			jsonItem = """{"model": "models/gemini-embedding-001", "content": {"parts":[{"text": $input}]}, "outputDimensionality": 1536, "task_type": "RETRIEVAL_DOCUMENT"}""",
			jsonItemQ = """{"model": "models/gemini-embedding-001", "content": {"parts":[{"text": $input}]}, "outputDimensionality": 1536, "task_type": "RETRIEVAL_QUERY"}""",
			resultItemPath = "embeddings/*/values",
			maxTokens = 8000, maxInputs = 100, //both are tested max allowed
		}); //docs: default outputDimensionality 3072, else need to normalize; tested: don't need (same after normalization).
		embedding.Add(embedding[^1] with {
			name = "Gemini for icons",
			jsonItem = """{"model": "models/gemini-embedding-001", "content": {"parts":[{"text": $input}]}, "outputDimensionality": 384, "task_type": "RETRIEVAL_DOCUMENT"}""",
			jsonItemQ = """{"model": "models/gemini-embedding-001", "content": {"parts":[{"text": $input}]}, "outputDimensionality": 384, "task_type": "RETRIEVAL_QUERY"}""",
		});
		
#if !true //rejected. 1. If used, probably better would be TOML (tested), not JSON. 2. Can use a LA extension instead; why to learn TOML or learn/escape JSON, when can do it in the familiar, convenient, safe and concise C# way.
		var userFile = folders.ThisAppDocuments + @".settings\AI.json";
		if (filesystem.exists(userFile).File) {
			try {
				var b = filesystem.loadBytes(userFile);
				var u = JsonSerializer.Deserialize<AiSettings>(b, JSettings.SerializerOptions);
				foreach (var v in u.embedding ?? []) {
					if (v.name.NE() || embedding.Any(o => o.name.Eqi(v.name))) continue;
					embedding.Add(v);
				}
			}
			catch (Exception ex) { print.warning(ex); }
		}
#endif
	}
	
	public static AiSettings Instance { get; } = new(0);
	
	public ObservableCollection<EmbeddingModel> embedding;
	
	/// <returns>null if not found.</returns>
	public EmbeddingModel GetEmbeddingModel(string name) => embedding.FirstOrDefault(o => o.name.Eqi(name));
	
	public record class EmbeddingModel {
		public string name, provider, url, auth, json, jsonQ, jsonItem, jsonItemQ, resultItemPath, resultTokensPath;
		public int maxTokens, maxInputs;
		public float minScore;
		
		(Range inputD, Range inputItemD, Range inputQ, Range inputItemQ, Range apiKey) _ranges;
		
		static regexp s_rxInput, s_rxKey;
		
		public bool Validate() {
			if (name.NE() || provider.NE() || url.NE() || json.NE() || resultItemPath.NE() || maxTokens < 1 || maxInputs < 1) return _Error("some settings missing");
			
			if (jsonQ == "") jsonQ = null;
			if (jsonItem == "") jsonItem = null;
			if (jsonItemQ == "") jsonItemQ = null;
			if (resultTokensPath == "") resultTokensPath = null;
			
			s_rxInput ??= new(@"""\s*:\s*\K\$input\b");
			if (!_InputRange(json, out _ranges.inputD)) return false;
			if (!_InputRange(jsonQ, out _ranges.inputQ)) return false;
			if (!_InputRange(jsonItem, out _ranges.inputItemD)) return false;
			if (!_InputRange(jsonItemQ, out _ranges.inputItemQ)) return false;
			
			if (auth.NE()) auth = "Authorization: Bearer $apiKey";
			s_rxKey ??= new(@":.*\K\$apiKey\b");
			if (!_Range(auth, out _ranges.apiKey, s_rxKey)) return _Error("auth does not contain $apiKey: " + auth);
			
			bool _Range(string s, out Range r, regexp rx) {
				r = default;
				if (s is null) return true;
				if (!rx.Match(s, 0, out RXGroup g)) return false;
				r = g.Start..g.End;
				return true;
			}
			
			bool _InputRange(string json, out Range r) {
				if (_Range(json, out r, s_rxInput)) return true;
				return _Error("JSON template does not contain $input: " + json);
			}
			
			return true;
			
			bool _Error(string s) {
				print.warning($"Error in settings of AI model '{name}': {s}");
				return false;
			}
		}
		
		public string GetPostJson(ICollection<string> input, bool isQuery) {
			//in JSON template replace $input with `input`
			
			//if there is a JSON item template, replace its $input with `input` items. Used when the JSON array elements are objects, not strings, eg Gemini.
			string s1 = "\"", s2 = "\"";
			string j = isQuery ? (jsonItemQ ?? jsonItem) : jsonItem;
			if (j != null) {
				var range = (object)j == jsonItemQ ? _ranges.inputItemQ : _ranges.inputItemD;
				s1 = j[..range.Start] + "\"";
				s2 = "\"" + j[range.End..];
			}
			
			var b = new StringBuilder();
			foreach (var v in input) b.Append(b.Length == 0 ? "[" : ", ").Append(s1).Append(v.Escape()).Append(s2);
			b.Append("]");
			
			j = isQuery ? (jsonQ ?? json) : json;
			return j.ReplaceAt((object)j == jsonQ ? _ranges.inputQ : _ranges.inputD, b.ToString());
		}
		
		/// <exception cref="Exception"></exception>
		public string GetAuthHeader() {
			if (!App.Settings.ai_ak.TryGetValue(provider, out string apiKey) || apiKey.NE()) throw new InvalidCredentialException("AI settings error: no API key");
			
			if (apiKey is ['%', _, .., '%']) {
				apiKey = Environment.GetEnvironmentVariable(apiKey = apiKey[1..^1]) ?? throw new InvalidCredentialException($"AI settings error: missing environment variable {apiKey}");
			} else {
				apiKey = EdProtectedData.Unprotect(apiKey);
			}
			
			//in auth template replace $apiKey
			return auth.ReplaceAt(_ranges.apiKey, apiKey);
		}
	}
	
	//public override string ToString() => JsonSerializer.Serialize(this, JSettings.SerializerOptions);
	public override string ToString() {
		return print.util.toString(embedding);
		//TODO: add chat
	}
}
