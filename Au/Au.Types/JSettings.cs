using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;

namespace Au.Types
{
	/// <summary>
	/// Base of record classes that contain various settings as public fields. Loads and lazily auto-saves from/to a JSON file.
	/// </summary>
	/// <remarks>
	/// All functions are thread-safe.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// MySettings sett = MySettings.Load(); //in a class you would use a static field or property, but this example uses a local variable for simplicity
	/// 
	/// print.it(sett.i);
	/// sett.i++;
	/// 
	/// if (dialog.showInput(out string s, "example", editText: sett.s)) {
	/// 	sett.s = s;
	/// 	
	/// 	print.it("old array:", sett.a);
	/// 	if ((!sett.a.Contains(s))) sett.a = sett.a.InsertAt(-1, s);
	/// 	
	/// 	print.it("old dictionary:", sett.d);
	/// 	sett.d[s] = DateTime.Now;
	/// }
	/// 
	/// 
	/// record class MySettings : JSettings {
	/// 	public static readonly string File = folders.ThisAppDocuments + @"MySettings.json";
	/// 
	/// 	public static MySettings Load() => Load<MySettings>(File);
	/// 	
	/// 	// examples of settings
	/// 	public int i;
	/// 	public string s = "default";
	/// 	public string[] a = Array.Empty<string>();
	/// 	public Dictionary<string, DateTime> d = new();
	/// }
	/// ]]></code>
	/// </example>
	public abstract record class JSettings : IDisposable
	{
		string _file;
		bool _loadedFile;
		byte[] _old;

		static readonly List<JSettings> s_list = new();
		static int s_loadedOnce;

		/// <summary>
		/// Loads JSON file and deserializes to new object of type T.
		/// Returns new empty object if file does not exist or failed to load or parse (invalid JSON) or <i>useDefault</i> true. If failed, writes error info in the output.
		/// Will auto-save soon after a property changed.
		/// </summary>
		/// <param name="file">Full path of .json file. If null, does not load and will not save.</param>
		/// <param name="useDefault">Use default settings, don't load from file. Delete file if exists.</param>
		protected static T Load<T>(string file, bool useDefault = false) where T : JSettings
			=> (T)_Load(file, typeof(T), useDefault);

		static JSettings _Load(string file, Type type, bool useDefault) {
			//using var p1 = perf.local();
			JSettings R = null;
			if (file != null) {
				if (filesystem.exists(file)) {
					try {
						if (useDefault) {
							filesystem.delete(file);
						} else {
							var b = filesystem.loadBytes(file);
							//using var p1 = perf.local(); //first time ~40 ms hot
							//p1.Next('f');
							R = JsonSerializer.Deserialize(b, type, s_jsOptions.Value) as JSettings;
							//p1.Next('d');
						}
					}
					catch (Exception ex) {
						string es = ex.ToStringWithoutStack();
						if (useDefault) {
							print.it($"Failed to delete settings file '{file}'. {es}");
						} else {
							string backup = file + ".backup";
							try { filesystem.move(file, backup, FIfExists.Delete); } catch { backup = "failed"; }
							print.it(
$@"Failed to load settings from {file}. Will use default settings.
	{es}
	Backup: {backup}");
						}
					}
				}
			}

			if (R == null) R = Activator.CreateInstance(type) as JSettings; else R._loadedFile = true;
			//p1.Next('c');

			if (file != null) {
				R._file = file;
				R._old = JsonSerializer.SerializeToUtf8Bytes(R, type, s_jsOptions.Value);
				//p1.Next('s');

				//autosave
				if (Interlocked.Exchange(ref s_loadedOnce, 1) == 0) {
					run.thread(() => {
						for (; ; ) {
							Thread.Sleep(2000);
							//Debug_.MemorySetAnchor_();
							_SaveAllIfNeed();
							//Debug_.MemoryPrint_(); //editor ~4 KB
						}
					}, sta: false);

					process.thisProcessExit += _ => _SaveAllIfNeed(); //info: .NET does not call finalizers when process exits
				}
				lock (s_list) s_list.Add(R);
			}

			return R;

			static void _SaveAllIfNeed() {
				lock (s_list) foreach (var v in s_list) v.SaveIfNeed();
			}
		}

		/// <summary>
		/// Call this when finished using the settings. Saves now if need, and stops autosaving.
		/// Don't need to call if the settings are used until process exit.
		/// </summary>
		public void Dispose() {
			lock (s_list) s_list.Remove(this);
			SaveIfNeed();
		}

		//repeated serialization speed with same options ~50 times better, eg 15000 -> 300 mcs cold. It's documented. Can be shared by multiple types.
		static readonly Lazy<JsonSerializerOptions> s_jsOptions = new(() => new() {
			//IgnoreNullValues = true, //obsolete, use DefaultIgnoreCondition
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			IncludeFields = true,
			IgnoreReadOnlyFields = true,
			IgnoreReadOnlyProperties = true,
			WriteIndented = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			AllowTrailingCommas = true,
		});

		/// <summary>
		/// Saves now if need.
		/// Don't need to call explicitly. Autosaving is every 2 s, also on process exit and <b>Dispose</b>.
		/// </summary>
		public void SaveIfNeed() {
			if (_file == null) return;
			try {
				//using var p1 = perf.local();
				var b = JsonSerializer.SerializeToUtf8Bytes(this, GetType(), s_jsOptions.Value);
				//p1.Next();
				bool same = b.AsSpan().SequenceEqual(_old);
				//p1.Next();
				//if (script.role == SRole.MiniProgram) print.it(same);
				if (same) return;
				filesystem.saveBytes(_file, b);
				//print.qm2.write(GetType().Name + " saved");
				_old = b;
			}
			catch (Exception ex) {
				print.it($"Failed to save settings to '{_file}'. {ex.ToStringWithoutStack()}");
			}
		}

		/// <summary>
		/// true if settings were loaded from file.
		/// </summary>
		/// <remarks>
		/// Returns false if <b>Load</b> did not find the file (the settings were not saved) or failed to load/parse or parameter <i>useDefault</i> = true or parameter <i>file</i> = null.
		/// </remarks>
		[JsonIgnore]
		public bool LoadedFile => _loadedFile;
	}
}
