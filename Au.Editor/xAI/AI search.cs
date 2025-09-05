using System.Text.Json.Nodes;
using System.Net.Http;
using System.Buffers.Text;

namespace AI;

class Embeddings(AiEmbeddingModel model) {
	/// <summary>
	/// Calls AI embeddings API to get embedding vectors for any number of strings.
	/// </summary>
	/// <param name="input">0 or more strings.</param>
	/// <param name="forDatabase">true if for a vector database. false if for a query. Some API have optimization parameters for it.</param>
	/// <param name="getInt8"><c>true</c> - get list of <c>sbyte[]</c>. <c>false</c> - get list of <c>float[]</c>.</param>
	/// <exception cref="OperationCanceledException"></exception>
	/// <exception cref="Exception"></exception>
	public List<Array> CreateEmbeddings(IList<string> input, bool forDatabase, bool getInt8 = false, CancellationToken cancel = default) {
		if (input.Count == 0) return [];
		
		var headers = model.GetHeaders();
		var allVectors = new List<Array>();
		long downloadSize = 0;
		dialog pd = null;
		using CancellationTokenSource cts = cancel != default ? CancellationTokenSource.CreateLinkedTokenSource(cancel) : new();
		if (input.Count == 1) {
			_GetBatch(input);
		} else {
			try {
				var batch = new List<string>();
				int sizeSum = 0;
				int tokensToDisplay = 0;
				var lim = model.limits;
				
				foreach (var (i, s) in input.Index()) {
					int nextSize = sizeSum + s.Length;
					if ((lim.maxTokens > 0 && nextSize / 2 >= lim.maxTokens) || (lim.maxInputs > 0 && batch.Count == lim.maxInputs) || (lim.maxSize > 0 && nextSize >= lim.maxSize)) {
						if (batch.Count == 0) batch.Add(s); //try anyway
						if (pd == null) {
							var url = string.Concat(model.url.Chunk(50).Select(o => { var s = new string(o); return s.Length < 50 ? s : s.Insert(s.LastIndexOfAny('/', ':') + 1, "\n  "); }));
							pd = dialog.showProgress(false, "Creating AI search vectors", flags: DFlags.ExpandDown, expandedText: $"API: {model.api}\nModel: {model.model}\nURL: {url}", footer: " ");
							pd.Destroyed += _pd_Destroyed;
						}
						int batchTokens = _GetBatch(batch);
						bool noTokens = batchTokens == 0;
						tokensToDisplay += noTokens ? sizeSum / 3 : batchTokens;
						sizeSum = 0;
						batch.Clear();
						if (i < input.Count - 1) {
							if (!pd.IsOpen) { pd = null; throw new OperationCanceledException(); } //clicked Cancel
							pd.Send.Progress(Math2.PercentFromValue(input.Count, allVectors.Count));
							pd.Send.ChangeFooterText($"Tokens: {(noTokens ? "~" : null)}{tokensToDisplay.ToString("N0")}.  Downloaded: {downloadSize / (1024 * 1024)} MB.", false);
						}
					}
					batch.Add(s);
					sizeSum += s.Length;
				}
				if (batch.Count > 0) _GetBatch(batch);
			}
			finally {
				pd?.Destroyed -= _pd_Destroyed;
				pd?.Send.Close();
			}
			
			void _pd_Destroyed(DEventArgs _) { cts.Cancel(); }
		}
		return allVectors;
		
		int _GetBatch(IList<string> input) {
			var post = model.GetPostData(input, isQuery: !forDatabase);
			var r = model.Post(post, headers: headers, cts.Token);
			var bytes = r.Bytes();
			downloadSize += bytes.Length; //never mind: probably downloads less, because of compression
			var json = JsonNode.Parse(bytes);
			
			//print.it(json.ToJsonString(new(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
			//print.it(json["usage"].ToJsonString(new(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
			//print.it(json["data"].ToJsonString(new(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
			
			var ja = model.GetVectors(json);
			foreach (var v in ja) {
				float[] f = null; sbyte[] b = null;
				if (v is JsonArray aj) {
					if (aj.Count != model.dimensions) throw new InvalidOperationException($"Returned array length ({aj.Count} elements) does not match model's dimensions ({model.dimensions}).");
					if (aj.All(o => o.AsValue().TryGetValue(out int i_))) {
						b = new sbyte[aj.Count];
						for (int j = 0; j < aj.Count; j++) b[j] = (sbyte)(int)aj[j];
					} else {
						f = new float[aj.Count];
						for (int j = 0; j < aj.Count; j++) f[j] = (float)aj[j];
					}
				} else { //OpenAI and Cohere can return float[] as base64 string
					var base64 = v.GetValue<string>();
					var t = Convert.FromBase64String(base64);
					if (t.Length == model.dimensions) b = MemoryMarshal.Cast<byte, sbyte>(t).ToArray();
					else if (t.Length == model.dimensions * 4) f = MemoryMarshal.Cast<byte, float>(t).ToArray();
					else throw new InvalidOperationException($"Returned data length ({t.Length} bytes) does not match model's dimensions ({model.dimensions}).");
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
			
			return model.GetTokens(json);
		}
	}
	
	/// <summary>
	/// Calls AI embeddings API to get embedding vector for single string used for a query.
	/// </summary>
	/// <exception cref="OperationCanceledException"></exception>
	/// <exception cref="Exception"></exception>
	public float[] CreateEmbedding(string input, CancellationToken cancel = default) => CreateEmbeddings([input], forDatabase: false, cancel: cancel)[0] as float[];
	
	List<EmVector> _GetEmbeddings(string dbPath, bool compact, Func<(List<string> names, List<string> texts)> getData, CancellationToken cancel) {
		_EmHash newHash = _Hash(dbPath), oldHash = default;
		string emPath = folders.ThisAppDataCommon + $@"AI\Embedding\{model.GetType()}-{pathname.getNameNoExt(dbPath)}.bin";
		var emFile = new _EmStorageFile(emPath);
		List<EmVector> ems = null;
		bool retried = false; gRetry:
		if (filesystem.exists(emPath).File) {
			try { ems = emFile.Load(out oldHash); }
			catch (Exception ex) when (emFile.Reading && ex is not OutOfMemoryException) { print.warning(ex); }
			
			//emFile.PrintUploadIfAtHome(model, newHash);
		}
		bool exists = ems != null && oldHash.modelParams == newHash.modelParams;
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
		//print.it(names.Count);
		
		if (names.Count > 250 && !retried) {
			retried = true;
			if (emFile.TryDownload(model, newHash)) goto gRetry;
		}
		
		var vectors = CreateEmbeddings(texts, forDatabase: true, getInt8: compact, cancel);
		
		if (exists) {
			foreach (var v in aSame) { names.Add(v.name); texts.Add(v.text); vectors.Add(v.vec); }
		}
		
		emFile.Save(names, vectors, texts, newHash);
		
		emFile.PrintUploadIfAtHome(model, newHash);
		
		return emFile.Load(out _);
		
		_EmHash _Hash(string file) {
			var h = new Hash.MD5Context();
			h.Add(filesystem.loadBytes(file));
			return new(h.Hash, $"{model.model};{model.dimensions};{model.emType}");
		}
	}
	
	#region docs
	
	static string s_dbFileDocs = folders.ThisAppBS + "doc4ai.db";
	
	/// <summary>
	/// Loads or creates/updates embeddings for docs.
	/// </summary>
	/// <exception cref="OperationCanceledException"></exception>
	/// <exception cref="Exception"></exception>
	public List<EmVector> GetDocsEmbeddings(CancellationToken cancel = default) {
		_GetData();//TODO
		
		return _GetEmbeddings(s_dbFileDocs, false, _GetData, cancel);
		
		static (List<string> names, List<string> texts) _GetData() {
			List<string> names = [], texts = [];
			using var db = new sqlite(s_dbFileDocs, SLFlags.SQLITE_OPEN_READONLY);
			using var sta = db.Statement("SELECT name,text FROM doc");
			while (sta.Step()) {
				string name = sta.GetText(0), text = sta.GetText(1);
				if (name.Ends("] toc")) continue;
				names.Add(name);
				_ProcessText(name, ref text);
				texts.Add(text);
			}
			return (names, texts);
		}
		
		static void _ProcessText(string name, ref string s) {
			if (!name.Starts("[api]")) return;
			//TODO
			print.clear();
			
			int i = s.Find("\n\n##### Exceptions");
			//if (i > 0) s = s[..i];
			
			print.it(s);
			
			//if (!dialog.show("Continue?",)) Environment.Exit(0);
		}
	}
	
	public string[] GetDocsTexts(IEnumerable<string> names, bool summary) {
		string[] an = names.ToArray();
		var a = new string[an.Length];
		using var db = new sqlite(s_dbFileDocs, SLFlags.SQLITE_OPEN_READONLY);
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
	
	static string s_dbFileIcons = folders.ThisAppBS + "icons.db";
	
	/// <summary>
	/// Loads or creates/updates embeddings for icons.
	/// </summary>
	/// <exception cref="OperationCanceledException"></exception>
	/// <exception cref="Exception"></exception>
	public List<EmVector> GetIconsEmbeddings(CancellationToken cancel = default) {
		return _GetEmbeddings(s_dbFileIcons, true, _GetData, cancel);
		
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
				
				using var db = new sqlite(s_dbFileIcons, SLFlags.SQLITE_OPEN_READONLY);
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
	
#if DEBUG
	public List<string> GetIconNamesWithPack(IEnumerable<string> names) {
		string[] an = names.ToArray();
		var nameIn = string.Join(',', an.Select(_ => "?"));
		var d = an.ToDictionary(o => o, o => new List<string>());
		List<string> a = [];
		using var db = new sqlite(s_dbFileIcons, SLFlags.SQLITE_OPEN_READONLY);
		using var stTables = db.Statement("SELECT name FROM _tables");
		while (stTables.Step()) {
			var table = stTables.GetText(0);
			using var stNames = db.Statement($"SELECT name FROM {table} WHERE name COLLATE BINARY IN ({nameIn})");
			stNames.BindAll(an);
			while (stNames.Step()) {
				d[stNames.GetText(0)].Add(table);
			}
		}
		foreach (var (k, v) in d) {
			foreach (var t in v) a.Add($"{t}.{k}");
		}
		return a;
	}
#endif
	
	#endregion
	
	public List<(EmVector f, float score)> GetTopMatches(float[] queryVector, List<EmVector> ems, int take) {
		var a = ems
			.Select(f => (f, score: CosineSimilarity(queryVector, f.vec)))
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
		w.Write(hash.modelParams);
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
	
	public bool TryDownload(AiEmbeddingModel model, _EmHash hash) {
#if !SCRIPT
		if (App.IsAtHome) return false;
#endif
		if (_TryGetZipName(model, hash) is not { } zipName) return false;
		string zipFile = file + ".7z";
		try {
			var r = internet.http.Get($"https://www.libreautomate.com/download/ai/embedding/{zipName}", dontWait: true);
			if (!r.IsSuccessStatusCode) return false;
			r.Download(zipFile);
			var dir = pathname.getDirectory(file);
			if (!SevenZip.Extract(out var errors, zipFile, dir)) { Debug_.Print(errors); return false; }
		}
		catch (Exception ex) { Debug_.Print(ex); }
		finally { filesystem.delete(zipFile, FDFlags.CanFail); }
		return true;
	}
	
	string _TryGetZipName(AiEmbeddingModel model, _EmHash hash) {
		if (!(file.Ends("-icons.bin") && model.isCompact && model.GetType().Assembly == GetType().Assembly)) return null;
		
		var md5 = new Hash.MD5Context();
		md5.Add(hash.hash);
		md5.Add(hash.modelParams);
		return $"{pathname.getNameNoExt(file)}-{md5.Hash.ToStringBase64Url()}.zip";
	}
	
	public void PrintUploadIfAtHome(AiEmbeddingModel model, _EmHash hash) {
#if !SCRIPT
		if (!App.IsAtHome) return;
#endif
		if (_TryGetZipName(model, hash) is not { } zipName) return;
		print.it($"<><script AI upload.cs|{file}|{zipName}>Upload<> AI embedding vectors.");
	}
}

file record struct _EmHash(Hash.MD5Result hash, string modelParams);

