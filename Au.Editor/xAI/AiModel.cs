using System.Text.Json.Nodes;
using System.Security.Authentication;
using System.Net.Http;
using LA;

namespace AI;

record AMLimits(int maxTokens, int maxInputs, int requestPeriod = 0, int maxSize = 0);

abstract record class AiModel(string api, string url, string model, AMLimits limits, string apiSuffix = null) {
	#region static
	
	static AiModel() {
		Models = [
			new ModelMistralEmbed(),
			new ModelMistralEmbed2(),
			new ModelMistralChat("mistral-medium-latest"),
			new ModelMistralChat("mistral-large-latest"),
			new ModelMistralChat("codestral-latest"),
			
			new ModelOpenaiEmbed(),
			new ModelOpenaiEmbed2(),
			new ModelOpenaiChat("gpt-5"),
			new ModelOpenaiChat("gpt-5-mini"),
			//new ModelOpenaiCompletionsChat("gpt-5"),
			//new ModelOpenaiCompletionsChat("gpt-5-mini"),
			
			new ModelGeminiEmbed(),
			new ModelGeminiEmbed2(),
			new ModelGeminiChat("gemini-2.5-flash"),
			new ModelGeminiChat("gemini-2.5-flash-lite"),
			
			
			new ModelCohereEmbed(),
			new ModelCohereEmbed2(),
			new ModelCohereChat("command-a-03-2025"),
			new ModelCohereRerank("rerank-v3.5"),
			new ModelCohereRerank("rerank-english-v3.0"),
			
			new ModelVoyageEmbed(),
			new ModelVoyageEmbed2(),
			new ModelVoyageRerank("rerank-2.5"),
			new ModelVoyageRerank("rerank-2.5-lite"),
			
			new ModelClaudeChat("claude-opus-4-1"),
			new ModelClaudeChat("claude-sonnet-4-0"),
		];
	}
	
	public static List<AiModel> Models { get; }
	
	public static T GetModel<T>() where T : AiModel => Models.OfType<T>().First();
	public static T GetModel<T>(string model) where T : AiModel => Models.OfType<T>().First(o => o.model == model);
	
	#endregion
	
	public string DisplayName => $"{api}{apiSuffix} {model}";
	
	/// <exception cref="Exception"></exception>
	public IEnumerable<string> GetHeaders() {
		if (ApiKeys is not { } ak) throw new InvalidOperationException("Property ApiKeys not set");
		if (!ak.TryGetValue(api, out string apiKey) || apiKey.NE()) throw new InvalidCredentialException("AI settings error: no API key");
		
		if (apiKey is ['%', _, .., '%']) {
			apiKey = Environment.GetEnvironmentVariable(apiKey = apiKey[1..^1]) ?? throw new InvalidCredentialException($"AI settings error: missing environment variable {apiKey}");
		} else {
			apiKey = EdProtectedData.Unprotect(apiKey);
		}
		
		return _GetHeaders(apiKey);
	}
	
	protected private virtual IEnumerable<string> _GetHeaders(string apiKey) => ["Authorization: Bearer " + apiKey];
	
	public static Dictionary<string, string> ApiKeys { get; set; }
	
	/// <summary>
	/// HTTP-posts data. Manages <c>limits.requestPeriod</c> (waits if need) and error "too many requests" (waits/retries with UI and cancellation).
	/// </summary>
	/// <exception cref="OperationCanceledException"></exception>
	/// <exception cref="Exception"></exception>
	public HttpResponseMessage Post(object data, IEnumerable<string> headers, CancellationToken cancel = default) {
		if (!s_dpl.TryGetValue(api, out _ApiPostTimes ppt)) s_dpl.Add(api, ppt = new());
		bool retried = false;
		gRetry:
		if (limits.requestPeriod > 0) {
			long sleep = limits.requestPeriod - (Environment.TickCount64 - ppt.lastRequestTime);
			if (sleep > 0) Task.Delay((int)sleep, cancel).GetAwaiter().GetResult();
			ppt.lastRequestTime = Environment.TickCount;
		}
		var r = internet.http.Send(internet.message(HttpMethod.Post, url, headers: headers, internet.jsonContent(data)), cancel);
		if (r.IsSuccessStatusCode) return r;
		if (r.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
			if (r.Headers.RetryAfter is { Delta: not null } ra) ppt.waitRetryS = Math.Max(1, (int)ra.Delta.Value.TotalSeconds);
			if (retried) ppt.waitRetryS += 10; else retried = true;
			using var osd = osdText.showText("", -1, icon: icon.stock(StockIcon.WARNING), showMode: OsdMode.ThisThread, dontShow: true);
			for (int i = 0; i < ppt.waitRetryS; i++) {
				osd.Text = $"Too many AI requests.\nWaiting {ppt.waitRetryS - i} s and will retry.\nYou can click here to cancel.";
				osd.Visible = true;
				wait.doEvents(1000);
				if (!osd.Visible || cancel.IsCancellationRequested) {
					print.it("Too many AI requests", r.Text(ignoreError: true));
					throw new OperationCanceledException();
				}
			}
			goto gRetry;
		}
		throw new HttpRequestException($"{r.StatusCode}. {r.Text(ignoreError: true)}");
	}
	
	class _ApiPostTimes {
		public long lastRequestTime;
		public int waitRetryS = 10;
	}
	static Dictionary<string, _ApiPostTimes> s_dpl = [];
}

abstract record class AiEmbeddingModel(string api, string url, string model, int dimensions, string emType, AMLimits limits)
	: AiModel(api, url, model, limits) {
	
	public bool isCompact { get; protected set; }
	
	public abstract object GetPostData(IList<string> input, bool isQuery);
	
	public abstract IEnumerable<JsonNode> GetVectors(JsonNode j);
	
	public abstract int GetTokens(JsonNode j);
}

abstract record class AiImageEmbeddingModel(string api, string url, string model, int dimensions, string emType, AMLimits limits)
	: AiEmbeddingModel(api, url, model, dimensions, emType, limits) {
	
	public abstract object GetPostData(IList<object[]> input, bool isQuery);
}

abstract record class AiChatModel(string api, string url, string model, AMLimits limits)
	: AiModel(api, url, model, limits) {
	
	public abstract object GetPostData(string systemInstruction, List<AiChatMessage> messages, double? temperature = null);
	
	public abstract AiChatMessage GetAnswer(JsonNode j);
}

record class AiChatMessage(ACMRole role, string text, JsonNode json = null);
enum ACMRole { user, assistant, tool }

abstract record class AiRerankModel(string api, string url, string model, AMLimits limits)
	: AiModel(api, url, model, limits) {
	
	public abstract object GetPostData(string query, IList<string> documents, int top_n = 0);
	
	public abstract IEnumerable<AiRerankResult> GetResults(JsonNode j);
}

record struct AiRerankResult(int index, float score);

#region Mistral

record class ModelMistralEmbed : AiEmbeddingModel {
	public ModelMistralEmbed() : base("Mistral", "https://api.mistral.ai/v1/embeddings", "codestral-embed", 1536, "float", new(32000, 256, requestPeriod: 1100)) { }
	//"mistral-embed" (only 1024 dim float), "codestral-embed"
	
	public override object GetPostData(IList<string> input, bool isQuery)
		=> new { model, input, output_dimension = dimensions, output_dtype = emType };
	
	public override IEnumerable<JsonNode> GetVectors(JsonNode j)
		=> j["data"].AsArray().Select(o => o["embedding"]);
	
	public override int GetTokens(JsonNode j)
		=> (int)j["usage"]["total_tokens"];
}

record class ModelMistralEmbed2 : ModelMistralEmbed {
	public ModelMistralEmbed2() { isCompact = true; dimensions = 384; emType = "int8"; }
}

record class ModelMistralChat : AiChatModel {
	public ModelMistralChat(string model) : base("Mistral", "https://api.mistral.ai/v1/chat/completions", model, new(32000, 256, requestPeriod: 1500)) { }
	//"mistral-small-latest", "mistral-medium-latest", "mistral-large-latest", "codestral-latest"
	
	public override object GetPostData(string systemInstruction, List<AiChatMessage> messages, double? temperature = null)
		=> new {
			model,
			messages = (object[])[new { role = "system", content = systemInstruction }, .. messages.Select(o => new { role = o.role.ToString(), content = o.text })],
			temperature,
		};
	
	public override AiChatMessage GetAnswer(JsonNode j)
		=> new AiChatMessage(ACMRole.assistant, (string)j["choices"][0]["message"]["content"]);
}

#endregion

#region OpenAI

record class ModelOpenaiEmbed : AiEmbeddingModel {
	public ModelOpenaiEmbed() : base("OpenAI", "https://api.openai.com/v1/embeddings", "text-embedding-3-small", 1536, null, new(100000, 2048)) { }
	
	public override object GetPostData(IList<string> input, bool isQuery)
		=> new { model, input, dimensions, encoding_format = "base64" };
	
	public override IEnumerable<JsonNode> GetVectors(JsonNode j)
		=> j["data"].AsArray().Select(o => o["embedding"]);
	
	public override int GetTokens(JsonNode j)
		=> (int)j["usage"]["total_tokens"];
}

record class ModelOpenaiEmbed2 : ModelOpenaiEmbed {
	public ModelOpenaiEmbed2() { isCompact = true; dimensions = 384; }
}

record class ModelOpenaiChat : AiChatModel {
	public ModelOpenaiChat(string model) : base("OpenAI", "https://api.openai.com/v1/responses", model, new(100000, 2048)) { apiSuffix = " responses"; }
	//"gpt-5", "gpt-5-mini", "gpt-5-nano"
	
	public override object GetPostData(string instructions, List<AiChatMessage> messages, double? temperature = null) {
		List<object> input = [];
		foreach (var m in messages) {
			if (m.role is ACMRole.user) input.Add(new { role = "user", content = m.text });
			else input.AddRange(m.json.AsArray());
		}
		
		if (model.Starts("gpt-4"))
			return new {
				model,
				instructions,
				input,
				temperature,
				store = false,
			};
		
		return new {
			model,
			instructions,
			input,
			//temperature, //not supported
			store = false,
			reasoning = new { effort = "medium" } //high, medium, minimal
		};
	}
	
	public override AiChatMessage GetAnswer(JsonNode j) {
		var a = j["output"].AsArray();
		foreach (var v in a) {
			if ((string)v["type"] == "message") { //doc: always "message"
				var m = v["content"][0];
				if ((string)m["type"] != "output_text") continue; //refusal
				return new AiChatMessage(ACMRole.assistant, (string)m["text"], a);
			}
		}
		return null;
	}
	
	//tokens: (int)json["usage"]["total_tokens"]
}

//record class ModelOpenaiCompletionsChat : AiChatModel {
//	public ModelOpenaiCompletionsChat(string model) : base("OpenAI", "https://api.openai.com/v1/chat/completions", model, new(100000, 2048)) { apiSuffix = " completions"; }

//	public override object GetPostData(string systemInstruction, List<AiChatMessage> messages, double? temperature = null)
//		=> new {
//			model,
//			messages = (object[])[new { role = "developer", content = systemInstruction }, .. messages.Select(o => new { role = o.role.ToString(), content = o.text })],
//			temperature,
//		};

//	public override AiChatMessage GetAnswer(JsonNode j)
//		=> new AiChatMessage(ACMRole.assistant, (string)j["choices"][0]["message"]["content"]);

//	//tokens: (int)json["usage"]["total_tokens"]
//}

#endregion

#region Gemini

record class ModelGeminiEmbed : AiEmbeddingModel {
	const string c_model = "gemini-embedding-001";
	
	public ModelGeminiEmbed() : base("Gemini", $"https://generativelanguage.googleapis.com/v1beta/models/{c_model}:batchEmbedContents", c_model, 1536, null, new(8000, 100, requestPeriod: 2000)) { }
	
	private protected override IEnumerable<string> _GetHeaders(string apiKey) => ["x-goog-api-key: " + apiKey];
	
	public override object GetPostData(IList<string> input, bool isQuery)
		=> new {
			requests = (object[])[input.Select(o => new {
				model = "models/" + model,
				content = new { parts = (object[])[new { text = o }] },
				outputDimensionality = dimensions,
				task_type = isQuery ? "RETRIEVAL_QUERY" : "RETRIEVAL_DOCUMENT"
			})]
		};
	
	public override IEnumerable<JsonNode> GetVectors(JsonNode j)
		=> j["embeddings"].AsArray().Select(o => o["values"]);
	
	public override int GetTokens(JsonNode j) => 0;
}

record class ModelGeminiEmbed2 : ModelGeminiEmbed {
	public ModelGeminiEmbed2() { isCompact = true; dimensions = 384; }
}

record class ModelGeminiChat : AiChatModel {
	public ModelGeminiChat(string model) : base("Gemini", $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent", model, new(8000, 100)) { }
	//"gemini-2.5-flash", "gemini-2.5-flash-lite"
	
	private protected override IEnumerable<string> _GetHeaders(string apiKey) => ["x-goog-api-key: " + apiKey];
	
	public override object GetPostData(string systemInstruction, List<AiChatMessage> messages, double? temperature = null) {
		return new {
			system_instruction = new { parts = (object[])[new { text = systemInstruction }] },
			contents = messages.Select(o => new { role = o.role is ACMRole.user ? "user" : "model", parts = (object[])[new { text = o.text }] }),
			generationConfig = new { temperature }
		};
	}
	
	public override AiChatMessage GetAnswer(JsonNode j)
		=> new AiChatMessage(ACMRole.assistant, (string)j["candidates"][0]["content"]["parts"][0]["text"]);
	
	//tokens: (int)json["usageMetadata"]["totalTokenCount"]
}

#endregion

#region Cohere

record class ModelCohereEmbed : AiEmbeddingModel {
	public ModelCohereEmbed() : base("Cohere", "https://api.cohere.ai/v2/embed", "embed-v4.0", 1536, "base64", new(100000, 96, requestPeriod: 1000, maxSize: 256000)) { }
	
	public override object GetPostData(IList<string> texts, bool isQuery)
		=> new { model, texts, output_dimension = dimensions, embedding_types = (string[])[emType], input_type = isQuery ? "search_query" : "search_document" };
	
	public override IEnumerable<JsonNode> GetVectors(JsonNode j)
		=> j["embeddings"][emType].AsArray();
	
	public override int GetTokens(JsonNode j)
		=> (int)j["meta"]["billed_units"]["input_tokens"];
}

record class ModelCohereEmbed2 : ModelCohereEmbed {
	public ModelCohereEmbed2() { isCompact = true; dimensions = 512; emType = "int8"; }
}

record class ModelCohereChat : AiChatModel {
	public ModelCohereChat(string model) : base("Cohere", "https://api.cohere.com/v2/chat", model, new(0, 96, maxSize: 256000)) { } //doc: max content length 256k (chars or tokens?)
	
	public override object GetPostData(string systemInstruction, List<AiChatMessage> messages, double? temperature = null)
		=> new {
			model,
			messages = (object[])[new { role = "developer", content = systemInstruction }, .. messages.Select(o => new { role = o.role.ToString(), content = o.text })],
			temperature,
		};
	
	public override AiChatMessage GetAnswer(JsonNode j)
		=> new AiChatMessage(ACMRole.assistant, (string)j["choices"][0]["message"]["content"]);
}

record class ModelCohereRerank : AiRerankModel {
	public ModelCohereRerank(string model) : base("Cohere", "https://api.cohere.com/v2/rerank", model, new(4096, 1000)) { }
	
	public override object GetPostData(string query, IList<string> documents, int top_n = 0)
		=> new {
			model, //rerank-v3.5, rerank-english-v3.0
			query,
			documents,
			top_n = Math.Min(top_n > 0 ? top_n : int.MaxValue, documents.Count),
			//max_tokens_per_doc //4096 is default and max
		};
	
	public override IEnumerable<AiRerankResult> GetResults(JsonNode j)
		=> j["results"].AsArray().Select(o => new AiRerankResult((int)o["index"], (float)o["relevance_score"]));
}

#endregion

#region Voyage

record class ModelVoyageEmbed : AiEmbeddingModel {
	//public ModelVoyageEmbed() : base("Voyage", "https://api.voyageai.com/v1/embeddings", "voyage-3.5", 1024, "float", new(3300, 1000, requestPeriod: 20500)) { } //free tier rate: 10000 TPM, 3 RPM
	public ModelVoyageEmbed() : base("Voyage", "https://api.voyageai.com/v1/embeddings", "voyage-3.5", 1024, "float", new(32000, 1000, requestPeriod: 1000)) { } //rate: 2000000 TPM, 2000 RPM
	
	public override object GetPostData(IList<string> input, bool isQuery)
		=> new {
			model, //voyage-3.5, voyage-3.5-lite
			input,
			output_dimension = dimensions, //2048, 1024 (default), 512, and 256
			output_dtype = emType,
			encoding_format = "base64",
			input_type = isQuery ? "query" : "document"
		};
	
	public override IEnumerable<JsonNode> GetVectors(JsonNode j)
		=> j["data"].AsArray().Select(o => o["embedding"]);
	
	public override int GetTokens(JsonNode j)
		=> (int)j["usage"]["total_tokens"];
}

record class ModelVoyageEmbed2 : ModelVoyageEmbed {
	public ModelVoyageEmbed2() { isCompact = true; dimensions = 512; emType = "int8"; }
}

record class ModelVoyageEmbedImage : AiImageEmbeddingModel {
	public ModelVoyageEmbedImage() : base("Voyage", "https://api.voyageai.com/v1/multimodalembeddings", "voyage-multimodal-3", 1024, null, new(32000, 1000, requestPeriod: 1000)) { isCompact = true; }
	
	public override object GetPostData(IList<object[]> input, bool isQuery) {
		List<object> a = new(input.Count);
		List<object> aItem = [];
		foreach (var oa in input) {
			aItem.Clear();
			foreach (var o in oa) {
				if (o is string s) aItem.Add(new { type = "text", text = s });
				else if (o is byte[] png) aItem.Add(new { type = "image_base64", image_base64 = "data:image/png;base64," + Convert.ToBase64String(png) });
				else throw new ArgumentException();
			}
			a.Add(new { content = aItem.ToArray() });
		}
		
		return new {
			model, //voyage-multimodal-3
			input = a,
			output_encoding = "base64",
			input_type = isQuery ? "query" : "document"
		};
	}
	
	public override object GetPostData(IList<string> input, bool isQuery)
		=> new {
			model,
			input = input.Select(s => new { content = (object[])[new { type = "text", text = s }] }),
			output_encoding = "base64",
			input_type = isQuery ? "query" : "document"
		};
	
	public override IEnumerable<JsonNode> GetVectors(JsonNode j)
		=> j["data"].AsArray().Select(o => o["embedding"]);
	
	public override int GetTokens(JsonNode j)
		=> (int)j["usage"]["total_tokens"];
}

record class ModelVoyageRerank : AiRerankModel {
	//public ModelVoyageRerank(string model) : base("Voyage", "https://api.voyageai.com/v1/rerank", model, new(3300, 1000, requestPeriod: 20500)) { } //free tier
	public ModelVoyageRerank(string model) : base("Voyage", "https://api.voyageai.com/v1/rerank", model, new(8000, 1000, requestPeriod: 1000)) { }
	
	public override object GetPostData(string query, IList<string> documents, int top_n = 0)
		=> new {
			model, //rerank-2.5, rerank-2.5-lite
			query,
			documents,
			top_k = Math.Min(top_n > 0 ? top_n : int.MaxValue, documents.Count),
			//truncation //true (default) - truncates at 8000 tok; false - error if > 8000 tok.
		};
	
	public override IEnumerable<AiRerankResult> GetResults(JsonNode j)
		=> j["data"].AsArray().Select(o => new AiRerankResult((int)o["index"], (float)o["relevance_score"]));
}

#endregion

#region Claude

//no embedding API

record class ModelClaudeChat : AiChatModel {
	public ModelClaudeChat(string model) : base("Claude", "https://api.anthropic.com/v1/messages", model, new(200000, 100000, 1250)) { } //200K tokens, 50 requests/minute, 30000 input tokens/minute, 8000 output tokens/minute
	
	private protected override IEnumerable<string> _GetHeaders(string apiKey) => ["x-api-key: " + apiKey, "anthropic-version: 2023-06-01"];
	
	public override object GetPostData(string systemInstruction, List<AiChatMessage> messages, double? temperature = null)
		=> new {
			model,
			system = systemInstruction,
			messages = messages.Select(o => new { role = o.role.ToString(), content = o.text }),
			max_tokens = 32000, //opus-4.1 32000, sonnet-4 64000
			temperature = temperature ?? 1, //TODO: error if null. What is the default?
		};
	
	public override AiChatMessage GetAnswer(JsonNode j)
		=> new AiChatMessage(ACMRole.assistant, (string)j["content"][0]["text"]);
}

#endregion
