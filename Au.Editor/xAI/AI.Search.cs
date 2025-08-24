using System.Text.Json.Nodes;
using System.Net.Http;

namespace AI;

class Embeddings(AiSettings.EmbeddingModel sett) {
	/// <summary>
	/// Calls AI embeddings API to get embedding vectors for any number of strings.
	/// </summary>
	/// <param name="input">0 or more strings.</param>
	/// <param name="forDatabase">true if for a vector database. false if for a query. Some API have optimization parameters for it.</param>
	/// <param name="getInt8"><c>true</c> - get list of <c>sbyte[]</c>. <c>false</c> - get list of <c>float[]</c>.</param>
	/// <returns><c>null</c> if canceled.</returns>
	/// <exception cref="Exception"></exception>
	public List<Array> GetEmbeddings(ICollection<string> input, bool forDatabase, bool getInt8 = false) {
		if (input.Count == 0) return [];
		
		var authHeader = sett.GetAuthHeader();
		
		dialog pd = null;
		int waitRetryS = 0;
		bool retryLinkClicked = false;
		long downloadSize = 0;
		
		var allVectors = new List<Array>();
		try {
			if (input.Count == 1) {
				_GetBatch(input);
			} else {
				var batch = new List<string>();
				int tokenSum = 0;
				int tokensToDisplay = 0;
				
				foreach (var (i, s) in input.Index()) {
					int estTokens = s.Length / 2;
					if ((tokenSum + estTokens > sett.maxTokens && batch.Count > 0) || batch.Count == sett.maxInputs) {
						_EnsureProgressDialog();
						int batchTokens = _GetBatch(batch);
						if (batchTokens < 0) return null; //Cancel
						tokensToDisplay += batchTokens;
						batch.Clear();
						tokenSum = 0;
						if (i < input.Count - 1) {
							if (!pd.IsOpen) return null; //Cancel
							pd.Send.Progress(Math2.PercentFromValue(input.Count, allVectors.Count));
							pd.Send.ChangeFooterText($"Tokens: {(sett.resultTokensPath.NE() ? "~" : null)}{tokensToDisplay.ToString("N0")}.  Downloaded: {downloadSize / (1024 * 1024)} MB.", false);
						}
					}
					batch.Add(s);
					tokenSum += estTokens;
				}
				if (batch.Count > 0) _GetBatch(batch);
			}
		}
		finally { pd?.Send.Close(); }
		return allVectors;
		
		void _EnsureProgressDialog() {
			pd ??= dialog.showProgress(false,
				"Creating AI search vectors",
				" ",
				footer: " ",
				onLinkClick: o => { if (o.LinkHref is "retry") retryLinkClicked = true; });
		}
		
		int _GetBatch(ICollection<string> input) {
			var post = sett.GetPostJson(input, isQuery: !forDatabase);
			//var postContent = internet.jsonContent(post); //no, ObjectDisposedException on retry
			bool waitedFull = false;
			gRetry:
			var r = internet.http.Post(sett.url, internet.jsonContent(post), headers: [authHeader]);
			if (r.StatusCode == System.Net.HttpStatusCode.TooManyRequests) {
				if (waitedFull) waitRetryS += 5; else waitRetryS = Math.Max(5, waitRetryS);
				_EnsureProgressDialog();
				retryLinkClicked = false;
				pd.Send.ChangeText2($"Too many requests. Waiting {waitRetryS} s and will <a href=\"retry\">retry</a>.", false);
				int waitedS = 0;
				for (int i = 0; i < waitRetryS * 10 && !retryLinkClicked; i++, waitedS = i / 10) Thread.Sleep(100);
				pd.Send.ChangeText2(" ", false);
				if (!pd.IsOpen) return -1;
				waitedFull = !retryLinkClicked;
				if (retryLinkClicked) waitRetryS = Math.Min(waitRetryS, waitedS);
				goto gRetry;
			}
			if (!r.IsSuccessStatusCode) {
				var s = r.Text(ignoreError: true);
				throw new HttpRequestException($"{r.StatusCode}. {s}");
			}
			
			var bytes = r.Bytes();
			downloadSize += bytes.Length; //never mind: probably downloads less, because of compression
			var json = JsonNode.Parse(bytes);
			
			//print.it(json.ToJsonString(new(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
			//print.it(json["usage"].ToJsonString(new(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
			//print.it(json["data"].ToJsonString(new(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
			
			var ja = _GetResultsArray();
			foreach (var v in ja) {
				float[] f = null; sbyte[] b = null;
				if (v is JsonArray aj) {
					if (aj.All(o => o.AsValue().TryGetValue(out int i_))) {
						b = new sbyte[aj.Count];
						for (int j = 0; j < aj.Count; j++) b[j] = (sbyte)(int)aj[j];
					} else {
						f = new float[aj.Count];
						for (int j = 0; j < aj.Count; j++) f[j] = (float)aj[j];
					}
				} else { //OpenAI and Cohere can return float[] as base64 string
					var base64 = v.GetValue<string>();
					f = MemoryMarshal.Cast<byte, float>(Convert.FromBase64String(base64)).ToArray();
				}
				if (getInt8) {
					if (b is null) {
						b = new sbyte[f.Length];
						for (int j = 0; j < f.Length; j++) b[j] = (sbyte)Math.Round(f[j] * 127);
#if DEBUG
						sbyte min = b.Min(), max = b.Max();
						if (min < -100 || max > 100) Debug_.Print($"{min}, {max}");
#endif
					}
					allVectors.Add(b);
				} else {
					if (f is null) {
						f = new float[b.Length];
						for (int j = 0; j < b.Length; j++) f[j] = b[j] / 127f;
					}
					allVectors.Add(f);
				}
			}
			
			JsonNode[] _GetResultsArray() {
				var resultPath = sett.resultItemPath;
				var ap = resultPath.Split('/');
				JsonNode j = json;
				JsonArray ja = null;
				int ip = 0;
				while (ip < ap.Length) {
					var part = ap[ip++];
					if (part == "*") {
						ja = j as JsonArray ?? throw new FormatException(resultPath);
						break;
					} else {
						if (j is not JsonObject n1) throw new FormatException(resultPath);
						j = j[part];
					}
				}
				if (ip < ap.Length - 1) throw new FormatException(resultPath);
				string member = ip == ap.Length ? null : ap[ip];
				var r = new JsonNode[ja.Count];
				for (int i = 0; i < r.Length; i++) {
					r[i] = (member != null ? ja[i][member] : ja[i]) ?? throw new FormatException(resultPath);
				}
				return r;
			}
			
			int _GetNTokens() {
				if (sett.resultTokensPath.NE()) return input.Sum(o => o.Length / 3);
				var j = json;
				foreach (var v in sett.resultTokensPath.Split('/')) j = j[v] ?? throw new FormatException(sett.resultTokensPath);
				return (int)j;
			}
			
			return _GetNTokens();
		}
	}
	
	/// <summary>
	/// Calls AI embeddings API to get embedding vector for single string used for a query.
	/// </summary>
	/// <exception cref="Exception"></exception>
	public float[] GetEmbedding(string input) => (float[])GetEmbeddings([input], forDatabase: false)[0];
	
	enum _GE { Docs, Icons }
	
	List<EmVector> _GetEmbeddings(_GE what, Func<(List<string> names, List<string> texts)> getData) {
		string dbPath = what switch { _GE.Docs => _dbFileDocs, _GE.Icons => _dbFileIcons, _ => null };
		_EmHash newHash = _Hash(dbPath), oldHash = default;
		string emPath = folders.ThisAppDataCommon + $@"AI\Embedding\{sett.name}.bin";
		//if (AiSettings.test) emPath = emPath.Insert(^4, " - Copy");//TODO
		var emFile = new _EmStorageFile(emPath);
		List<EmVector> ems = null;
		if (filesystem.exists(emPath).File) {
			try { ems = emFile.Load(out oldHash); }
			catch (Exception ex) when (emFile.Reading && ex is not OutOfMemoryException) { print.warning(ex); }
		}
		bool exists = ems != null && oldHash.sett == newHash.sett;
		if (exists && oldHash.hash == newHash.hash) return ems;
		
		var (names, texts) = getData();
		List<(string name, string text, Array vec)> aSame = null;
		
		if (exists) { //get embeddings only for new and changed texts
			var dOld = emFile.LoadForUpdate();
			aSame = new(dOld.Count);
			int j = 0;
			for (int i = 0; i < names.Count; i++) {
				string name = names[i], text = texts[i];
				if (dOld.TryGetValue(name, out var old) && old.hash == Hash.MD5(text)) {
					aSame.Add((name, text, old.vec));
				} else {
					names[j] = name;
					texts[j++] = text;
				}
			}
			names.RemoveRange(j, names.Count - j);
			texts.RemoveRange(j, texts.Count - j);
		}
		//print.it(names.Count);//TODO
		
		var vectors = GetEmbeddings(texts, forDatabase: true, getInt8: what is _GE.Icons);
		if (vectors == null) return null;
		
		if (exists) {
			foreach (var v in aSame) { names.Add(v.name); texts.Add(v.text); vectors.Add(v.vec); }
		}
		
		emFile.Save(names, vectors, texts, newHash);
		
		return emFile.Load(out _);
		
		_EmHash _Hash(string file) {
			var h = new Hash.MD5Context();
			h.Add(filesystem.loadBytes(file));
			return new(h.Hash, string.Join(';', sett.url, sett.json, sett.jsonQ, sett.jsonItem, sett.jsonItemQ));
		}
	}
	
	#region docs
	
	static string _dbFileDocs = folders.ThisAppBS + "doc4ai.db";
	
	/// <summary>
	/// Loads or creates/updates embeddings for docs.
	/// </summary>
	/// <returns>null if canceled.</returns>
	/// <exception cref="Exception"></exception>
	public List<EmVector> GetDocsEmbeddings() {
		return _GetEmbeddings(_GE.Docs, _GetData);
		
		(List<string> names, List<string> texts) _GetData() {
			List<string> names = [], texts = [];
			using var db = new sqlite(_dbFileDocs, SLFlags.SQLITE_OPEN_READONLY);
			using var sta = db.Statement("SELECT name,text FROM doc");
			while (sta.Step()) {
				string name = sta.GetText(0), text = sta.GetText(1);
				if (name.Ends("] toc")) continue;
				names.Add(name);
				texts.Add(text);
			}
			return (names, texts);
		}
	}
	
	public string[] GetDocsTexts(IEnumerable<string> names, bool summary) {
		string[] an = names.ToArray();
		var a = new string[an.Length];
		using var db = new sqlite(_dbFileDocs, SLFlags.SQLITE_OPEN_READONLY);
		using var sta = db.Statement($"SELECT name,{(summary ? "summary" : "text")} FROM doc WHERE name IN ({string.Join(',', an.Select(_ => "?"))})");
		sta.BindAll(an);
		while (sta.Step()) {
			string name = sta.GetText(0), text = sta.GetText(1);
			if (summary && text is null) text = name[(name.IndexOf(' ') + 1)..];
			a[an.IndexOf(name)] = text;
		}
		if (a.Contains(null)) throw new ArgumentException();
		return a;
	}
	
	#endregion
	
	#region icons
	
	//TODO: upload icon vector files to libreautomate.com. With names = hash.
	//	Or maybe to github. Probably not icons, but maybe docs. Convert to text: line = name + vector base64 + hash.
	
	static string _dbFileIcons = folders.ThisAppBS + "icons.db";
	
	/// <summary>
	/// Loads or creates/updates embeddings for icons.
	/// </summary>
	/// <returns>null if canceled.</returns>
	/// <exception cref="Exception"></exception>
	public List<EmVector> GetIconsEmbeddings(bool? create = null) {
		return _GetEmbeddings(_GE.Icons, _GetData);
		
		(List<string> names, List<string> texts) _GetData() {
			List<string> names = _GetUniqueIconNames(), texts = new(names.Count);
			var rx1 = new regexp(@"([a-z0-9])([A-Z])");
			var rx2 = new regexp(@"([A-Z])([A-Z][a-z])");
			foreach (var name in names) {
				var s = name.Trim('_');
				//split PascalCase etc into words
				s = rx1.Replace(s, "$1 $2");
				s = rx2.Replace(s, "$1 $2");
				//print.it(s, table);
				
				//tested, bad: `One Two icon`, `icon "One Two"`
				
				texts.Add(s);
			}
			
			return (names, texts);
			
			static List<string> _GetUniqueIconNames() {
				HashSet<string> h = new(60000, StringComparer.OrdinalIgnoreCase);
				
				using var db = new sqlite(_dbFileIcons, SLFlags.SQLITE_OPEN_READONLY);
				using var stTables = db.Statement("SELECT name FROM _tables");
				while (stTables.Step()) {
					var table = stTables.GetText(0);
					using var stNames = db.Statement("SELECT name FROM " + table);
					while (stNames.Step()) {
						var s = stNames.GetText(0);
						h.Add(s);
					}
				}
				//print.it(h.Count);
				return h.ToList();
			}
		}
	}
	
	public List<string> GetIconNamesWithPack(IEnumerable<string> names) {
		//TODO
		return null;
	}
	
	#endregion
	
	public List<(EmVector f, float score)> GetTopMatches(float[] queryVector, List<EmVector> ems, int take) {
		var a = ems
			.Select(f => (f, score: CosineSimilarity(queryVector, f.vec)))
			.Where(o => o.score >= sett.minScore)
			.OrderByDescending(x => x.score)
			.Take(take)
			.ToList();
		return a;
	}
	
	public static float CosineSimilarity(float[] a, float[] b) {
		float dot = 0, normA = 0, normB = 0;
		for (int i = 0; i < a.Length; i++) {
			dot += a[i] * b[i];
			normA += a[i] * a[i];
			normB += b[i] * b[i];
		}
		return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
	}
}

record class EmVector(string name, float[] vec);

file class _EmStorageFile(string file) {
	public void Save(List<string> names, List<Array> ems, List<string> texts, _EmHash hash) {
		filesystem.createDirectoryFor(file);
		using var w = new BinaryWriter(File.Create(file));
		w.Write((byte)(ems[0] is float[]? 4 : 1));
		w.Write((ems[0] as Array).Length);
		w.Write(hash.hash.r1);
		w.Write(hash.hash.r2);
		w.Write(hash.sett);
		for (int i = 0; i < names.Count; i++) {
			w.Write(names[i]);
			switch (ems[i]) {
			case float[] a: w.Write(MemoryMarshal.AsBytes(a)); break;
			case sbyte[] a: w.Write(MemoryMarshal.AsBytes(a)); break;
			}
			var md5 = Hash.MD5(texts[i]);
			w.Write(md5.r1);
			w.Write(md5.r2);
		}
	}
	
	BinaryReader _Load(out int type, out int vectorLen, out _EmHash hash) {
		var r = new BinaryReader(filesystem.loadStream(file));
		type = r.ReadByte();
		Reading = true; //for the caller's exception handler to detect whether the exception is likely because of corrupt file
		if (!(type is 1 or 4)) throw new FileFormatException();
		vectorLen = r.ReadInt32();
		hash = new(new(r.ReadInt64(), r.ReadInt64()), r.ReadString());
		return r;
	}
	
	public bool Reading { get; private set; }
	
	public List<EmVector> Load(out _EmHash hash) {
		using var r = _Load(out int type, out int vectorLen, out hash);
		byte[] b = type == 1 ? new byte[vectorLen] : null;
		List<EmVector> a = new();
		while (r.BaseStream.Position < r.BaseStream.Length) {
			var name = r.ReadString();
			var vec = new float[vectorLen];
			if (type == 4) {
				r.Read(MemoryMarshal.AsBytes(vec.AsSpan()));
			} else {
				r.Read(b);
				for (int i = 0; i < vectorLen; i++) {
					vec[i] = (sbyte)b[i] / 127f;
				}
			}
			r.ReadInt64(); r.ReadInt64(); //hash
			a.Add(new(name, vec));
		}
		return a;
	}
	
	public Dictionary<string, (Array vec, Hash.MD5Result hash)> LoadForUpdate() {
		Dictionary<string, (Array vec, Hash.MD5Result hash)> d = new();
		using var r = _Load(out int type, out int vectorLen, out _);
		while (r.BaseStream.Position < r.BaseStream.Length) {
			var name = r.ReadString();
			Array vec;
			if (type == 4) {
				var f = new float[vectorLen];
				r.Read(MemoryMarshal.AsBytes(f.AsSpan()));
				vec = f;
			} else {
				var b = new sbyte[vectorLen];
				r.Read(MemoryMarshal.AsBytes(b.AsSpan()));
				vec = b;
			}
			Hash.MD5Result md5 = new(r.ReadInt64(), r.ReadInt64());
			d.Add(name, (vec, md5));
		}
		return d;
	}
}

file record struct _EmHash(Hash.MD5Result hash, string sett);

