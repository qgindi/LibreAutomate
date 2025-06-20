using System.Text.Json;
using System.Text.Json.Serialization;

namespace Au.Types;

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
/// 	public string[] a = [];
/// 	public Dictionary<string, DateTime> d = new();
/// }
/// ]]></code>
/// </example>
public abstract record class JSettings : IDisposable {
	string _file;
	bool _loadedFile;
	byte[] _old;
	readonly object _lock = new();
	
	static readonly List<JSettings> s_list = new();
	static int s_loadedOnce;
	
	/// <summary>
	/// Loads a JSON file and deserializes to an object of type <b>T</b>, or creates a new object of type <b>T</b>.
	/// </summary>
	/// <returns>An object of type <b>T</b>. Just creates a new object if the file does not exist or failed to load or parse (invalid JSON) or <i>useDefault</i> <c>true</c>.</returns>
	/// <param name="file">Full path of <c>.json</c> file. If <c>null</c>, does not load and will not save.</param>
	/// <param name="useDefault">Use default settings, don't load from <i>file</i>. Delete <i>file</i> if exists.</param>
	/// <param name="useDefaultOnError">What to do if failed to load or parse the file: <c>true</c> (default) - backup (rename) the file and use default settings; <c>false</c> - throw exception (for example <b>JsonException</b> if invalid JSON); <c>null</c> - show dialog with options to exit or use default settings.</param>
	/// <exception cref="ArgumentException">Not full path.</exception>
	/// <exception cref="NotSupportedException">Field type not supported by <b>JsonSerializer</b>.</exception>
	protected static T Load<T>(string file, bool useDefault = false, bool? useDefaultOnError = true) where T : JSettings
		=> (T)_Load(file, typeof(T), useDefault, useDefaultOnError);
	
	static JSettings _Load(string file, Type type, bool useDefault, bool? useDefaultOnError) {
		JSettings R = null;
		if (file != null) {
			file = pathname.normalize(file);
			if (filesystem.exists(file, true)) {
				try {
					if (useDefault) {
						filesystem.delete(file);
					} else {
						//using var p1 = perf.local(); //first time ~40 ms (similar hot and cold)
						var b = filesystem.loadBytes(file);
						//p1.Next('f');
						R = JsonSerializer.Deserialize(b, type, SerializerOptions) as JSettings;
						//p1.Next('d');
						R._loadedFile = true;
					}
				}
				catch (Exception ex) when (ex is not NotSupportedException) {
					if (!useDefault) {
						if (useDefaultOnError == false) throw;
						if (useDefaultOnError == true) {
							string backup = file + ".backup";
							filesystem.move(file, backup, FIfExists.Delete); //note: don't try/catch
							print.it($"<>Failed to load settings from {file}. Will use default settings.\r\n\t<\a>{ex.ToStringWithoutStack()}</\a>\r\n\tBackup: <explore>{backup}<>");
						} else {
							if (!Environment.UserInteractive) throw;
							int button = dialog.show("Failed to load settings",
								$"{ex.ToStringWithoutStack()}\n\n<a href=\"{file}\">{file}</a>",
								"1 Exit|2 Backup (rename) the file and use default settings",
								flags: DFlags.CommandLinks,
								icon: DIcon.Error,
								onLinkClick: o => { run.selectInExplorer(o.LinkHref); });
							if (button == 1) Environment.Exit(1);
							else if (filesystem.exists(file)) filesystem.move(file, file + ".backup", FIfExists.Delete);
						}
					}
				}
			}
		}
		
		R ??= Activator.CreateInstance(type) as JSettings;
		
		if (file != null) {
			R._Ctor2(file);
			
			//autosave
			if (Interlocked.Exchange(ref s_loadedOnce, 1) == 0) {
				run.thread(() => {
					for (; ; ) {
						Thread.Sleep(2000);
						//Debug_.MemorySetAnchor();
						_SaveAllIfNeed(true);
						//Debug_.MemoryPrint(); //editor ~6 KB
					}
				}, sta: false).Name = "Au.JSettings";
				
				process.thisProcessExit += _ => { //info: .NET does not call finalizers when process exits
					FileWatcher.DisposeAll_();
					_SaveAllIfNeed(false);
					s_list.Clear();
				};
			}
			
			lock (s_list) s_list.Add(R);
		}
		
		return R;
		
		static void _SaveAllIfNeed(bool timer) {
			lock (s_list) {
				foreach (var v in s_list) {
					if (v.NoAutoSave) continue;
					if (timer && v.NoAutoSaveTimer) continue;
					v._SaveIfNeed(disposingOrExit: !timer);
				}
			}
		}
	}
	
	void _Ctor2(string file) {
		_file = file;
		_old = JsonSerializer.SerializeToUtf8Bytes(this, GetType(), SerializerOptions);
	}
	
	/// <summary>
	/// Saves now if need, and releases used resources. In the future will not save or reload.
	/// Don't need to call if the settings are used until process exit.
	/// </summary>
	public void Dispose() {
		Dispose(true);
		//no finalizer. Users can call Dispose, but usually settings objects live until process exit.
	}
	
	///
	protected virtual void Dispose(bool disposing) {
		lock (_lock) {
			lock (s_list) if (!s_list.Remove(this)) return;
			_watcher?.Dispose();
			_watcher = null;
			_modifiedExternally = null;
			if (!NoAutoSave) _SaveIfNeed(disposingOrExit: true);
			_file = null;
		}
	}
	
	//repeated serialization speed with same options ~50 times better, eg 15000 -> 300 mcs cold. It's documented. Can be shared by multiple types.
	static readonly Lazy<JsonSerializerOptions> s_jsOptions = new(() => new() {
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		IncludeFields = true,
		IgnoreReadOnlyFields = true,
		IgnoreReadOnlyProperties = true,
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		AllowTrailingCommas = true,
	});
	
	/// <summary>
	/// Use but don't modify.
	/// </summary>
	internal static JsonSerializerOptions SerializerOptions => s_jsOptions.Value;
	
	/// <summary>
	/// <c>true</c> if settings were loaded from file.
	/// </summary>
	/// <remarks>
	/// Returns <c>false</c> if <b>Load</b> did not find the file (the settings were not saved) or failed to load/parse or parameter <i>useDefault</i> = <c>true</c> or parameter <i>file</i> = <c>null</c>.
	/// </remarks>
	[JsonIgnore]
	public bool LoadedFile => _loadedFile;
	
	/// <summary>
	/// Don't automatically call <see cref="SaveIfNeed"/>.
	/// If <c>false</c> (default), calls every 2 s (unless <see cref="NoAutoSaveTimer"/> <c>true</c>); also when disposing and when the process exits.
	/// </summary>
	protected bool NoAutoSave { get; set; }
	
	/// <summary>
	/// Don't call <see cref="SaveIfNeed"/> every 2 s.
	/// Default <c>false</c>.
	/// </summary>
	protected bool NoAutoSaveTimer { get; set; }
	
	/// <summary>
	/// Saves now if need.
	/// Call this when you want to save changed settings immediately; else the changes are auto-saved after max 2 s. See also <see cref="NoAutoSave"/> and <see cref="NoAutoSaveTimer"/>.
	/// </summary>
	public void SaveIfNeed() => _SaveIfNeed(false);
	
	void _SaveIfNeed(bool disposingOrExit) {
		lock (_lock) {
			if (_file is null) return;
			bool save = false;
			try {
				var b = JsonSerializer.SerializeToUtf8Bytes(this, GetType(), SerializerOptions);
				save = !b.AsSpan().SequenceEqual(_old);
				if (save) {
					_watcher?.Paused = true;
					filesystem.saveBytes(_file, b);
					_old = b;
				}
			}
			catch (Exception ex) { print.it($"Failed to save settings to '{_file}'. {ex}"); }
			finally { if (save) _watcher?.Paused = false; }
		}
	}
	
	/// <summary>
	/// When detected that the settings file was externally modified or deleted (for example by another process of your program).
	/// </summary>
	/// <remarks>
	/// Your event handler can call <see cref="Reload"/>.
	/// 
	/// Runs in a thread pool thread.
	/// </remarks>
	/// <exception cref="NotSupportedException">Multiple <b>JSettings</b> objects in this process use the same file.</exception>
	public event Action ModifiedExternally {
		add {
			lock (_lock) {
				if (_file is null) return;
				_modifiedExternally += value ?? throw null;
				_watcher ??= FileWatcher.Watch(_file, _modifiedExternally);
			}
		}
		remove {
			lock (_lock) {
				_modifiedExternally -= value;
				if (_modifiedExternally == null) { _watcher?.Dispose(); _watcher = null; }
			}
		}
	}
	
	event Action _modifiedExternally;
	FileWatcher _watcher;
	
	/// <summary>
	/// Reloads settings from the file.
	/// </summary>
	/// <param name="newSettings">
	/// Receives a new settings object that inherits the behavior of this object but contains updated values of fields defined in the derived class. If the file does not exist, the fields have default values.
	/// If failed, receives this object (unchanged).
	/// </param>
	/// <returns><c>false</c> if failed.</returns>
	/// <remarks>
	/// Disposes this object. Does not change field values in it. It will no longer be tracked (e.g., for auto-save); tracking continues with the new object.
	/// </remarks>
	/// <example>
	/// Run this 2 times to test how a process of your app auto-reloads settings changed by another process of your app.
	/// <code><![CDATA[
	/// /*/ ifRunning run; /*/
	/// 
	/// var sett = Settings.Load();
	/// sett.ModifiedExternally += () => {
	/// 	if (!sett.Reload(out sett)) return;
	/// 	print.it($"ModifiedExternally. This process: {process.thisProcessId}. {sett}");
	/// };
	/// 
	/// var b = new wpfBuilder("JSettings example").WinSize(300).Columns(-1, -1, -1);
	/// b.R.AddButton("Print", _ => { print.it(sett); });
	/// b.AddButton("one++", _ => { sett.one++; sett.SaveIfNeed(); });
	/// b.AddButton("two++", _ => { sett.two++; sett.SaveIfNeed(); });
	/// b.Window.Topmost = true;
	/// b.ShowDialog();
	/// 
	/// record Settings : JSettings {
	/// 	public static Settings Load() => Load<Settings>(@"C:\Test\JSettings.json");
	/// 	
	/// #pragma warning disable CS0649
	/// 	public int one;
	/// 	public int two = 100;
	/// }
	/// ]]></code>
	/// </example>
	public bool Reload<T>(out T newSettings) where T : JSettings {
		bool ok = _Reload(out var js);
		newSettings = js as T;
		return ok;
	}
	
	bool _Reload(out JSettings newSettings) {
		newSettings = this;
		lock (_lock) {
			if (_file is null) {
				print.warning($"JSettings.Reload called for a disposed or momory-only settings object");
				return false;
			}
			
			JSettings R;
			try {
				if (filesystem.exists(_file, true)) {
					var b = filesystem.loadBytes(_file);
					R = JsonSerializer.Deserialize(b, GetType(), SerializerOptions) as JSettings;
					R._loadedFile = true;
				} else {
					R = Activator.CreateInstance(GetType()) as JSettings;
				}
			}
			catch (Exception ex) {
				print.warning($"Failed to reload settings from '{_file}'. {ex}", -1);
				return false;
			}
			
			R._Ctor2(_file);
			R.NoAutoSave = NoAutoSave;
			R.NoAutoSaveTimer = NoAutoSaveTimer;
			R._watcher = _watcher; _watcher = null;
			R._modifiedExternally = _modifiedExternally; _modifiedExternally = null;
			_file = null;
			lock (s_list) { s_list[s_list.IndexOf(this)] = R; }
			newSettings = R;
			return true;
		}
	}
}
