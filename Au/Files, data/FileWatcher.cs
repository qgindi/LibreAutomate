namespace Au.More;

/// <summary>
/// Watches a file for external changes. An external change is when another process writes, creates, deletes, moves or renames the file.
/// </summary>
/// <remarks>
/// All functions are thread-safe.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// /*/ ifRunning run; /*/
/// 
/// var c = new C();
/// c.Load(@"C:\Test\FileWatcher.xml");
/// c.ModifyAndSave();
/// dialog.show("Testing FileWatcher");
/// 
/// class C {
/// 	string _file;
/// 	XElement _x;
/// 	FileWatcher _watcher;
/// 	
/// 	public void Load(string file) {
/// 		_file = file;
/// 		if (filesystem.exists(_file)) {
/// 			_x = XElement.Load(_file);
/// 		} else {
/// 			_x = XElement.Parse("""<a>example</a>""");
/// 		}
/// 		_watcher ??= FileWatcher.Watch(_file, _OnExternalChange);
/// 	}
/// 	
/// 	void _OnExternalChange() {
/// 		_x = XElement.Load(_file);
/// 		print.it("external change", _x);
/// 	}
/// 	
/// 	public void ModifyAndSave() {
/// 		_x.Value = Random.Shared.Next().ToString();
/// 		
/// 		_watcher?.Paused = true;
/// 		try { _x.Save(_file); }
/// 		finally { _watcher?.Paused = false; }
/// 	}
/// }
/// ]]></code>
/// </example>
public sealed class FileWatcher : IDisposable {
	readonly object _dirWatcher;
	readonly string _file;
	Action _action;
	CancellationTokenSource _cts;
	long _timestamp;
	readonly object _lock = new();
	
	static _Watchers s_watchers = new();
	
	FileWatcher(object dirWatcher, string file, Action action) {
		_dirWatcher = dirWatcher;
		_file = file;
		_action = action;
		_SetTimestamp();
	}
	
	/// <summary>
	/// Stops watching.
	/// </summary>
	/// <remarks>
	/// Don't need to ever call this, unless you want to stop watching before the process exits.
	/// </remarks>
	public void Dispose() {
		DisposeNoRemove_();
		s_watchers.Remove(this);
	}
	
	internal void DisposeNoRemove_() {
		_action = null;
		lock (_lock) {
			_cts?.Dispose();
			_cts = null;
		}
	}
	
	void _SetTimestamp() {
		filesystem.GetTime_(_file, out _timestamp);
	}
	
	/// <summary>
	/// When your app writes, creates, deletes, moves or renames the file, it must set this property = <b>true</b> during the file operation. It helps to distinguish internal and external changes. Example: <see cref="FileWatcher"/>.
	/// </summary>
	public bool Paused {
		get => field;
		set {
			if (value == field) return;
			field = value;
			if (!field && _action != null) _SetTimestamp();
		}
	}
	
	void _Event(FileSystemEventArgs e) {
		//print.it(e.ChangeType, e.Name);
		if (Paused || _action is null) return;
		
		try {
			lock (_lock) {
				_cts?.Cancel();
				_cts?.Dispose();
				_cts = new();
				
				_ = Task.Delay(100, _cts.Token).ContinueWith(t => { //to cancel previous duplicate events
					try {
						if (t.IsCanceled) return;
						bool exists = filesystem.GetProp_(_file, out var p);
						bool deleted = e.ChangeType == WatcherChangeTypes.Deleted && !exists;
						if (deleted) {
							_timestamp = 0;
						} else {
							Debug_.PrintIf(!exists); if (!exists) return; //maybe temporarily deleted; we should get 'deleted' notification afterwards
							Debug_.PrintIf(p.size == 0); if (p.size == 0) return; //eg VSCode saves like this: opens, clears, closes; then opens writes closes. Normally the first notification is canceled, but.
							if (p.time == _timestamp) return;
							_timestamp = p.time;
						}
						//print.it(e.ChangeType, e.Name);
						_action?.Invoke();
					}
					catch (Exception ex) { Debug_.Print(ex); }
				}, TaskScheduler.Default);
			}
		}
		catch (Exception ex) { Debug_.Print(ex); } //eg _cts disposed
	}
	
	/// <summary>
	/// Starts watching a file for external changes. An external change is when another process writes, creates, deletes, moves or renames the file.
	/// </summary>
	/// <param name="file">Full path of a file, without environment variables.</param>
	/// <param name="onExternalChange">
	/// Called when detected that the file was changed externally.
	/// Important: when your app writes, creates, deletes, moves or renames the file, it must set <see cref="Paused"/> = <c>true</c> during the file operation.
	/// Runs in a thread pool thread.
	/// </param>
	/// <returns>
	/// A new <see cref="FileWatcher"/> instance used to manage the watcher.
	/// Don't need to dispose it, unless you want to stop watching before the process exits.
	/// Returns <c>null</c> and prints a warning if the watcher could not be created (for example the directory does not exist).
	/// </returns>
	/// <exception cref="NotSupportedException">
	/// Thrown if this method has already been called for the same file without a corresponding call to <see cref="Dispose"/>.
	/// If multiple notification handlers are needed, combine them into the <i>onExternalChange</i> delegate.
	/// </exception>
	/// <example><see cref="FileWatcher"/></example>
	public static FileWatcher Watch(string file, Action onExternalChange)
		=> s_watchers.Add(file ?? throw new ArgumentNullException(), onExternalChange ?? throw new ArgumentNullException());
	
	internal static void DisposeAll_() { s_watchers.DisposeAll(); }
	
	class _Watchers {
		class _DirectoryWatcher : FileSystemWatcher {
			public readonly Dictionary<string, FileWatcher> dFiles = new(StringComparer.OrdinalIgnoreCase);
			
			public _DirectoryWatcher(string dir) : base(dir) { }
		}
		
		readonly List<_DirectoryWatcher> _a = [];
		
		public FileWatcher Add(string file, Action modifiedExternally) {
			lock (this) {
				var dir = pathname.getDirectory(file);
				_DirectoryWatcher dw = null; foreach (var v in _a) if (v.Path.Eqi(dir)) { dw = v; break; }
				if (dw is null) {
					try {
						dw = new(dir) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
						dw.Deleted += _Event;
						dw.Created += _Event;
						dw.Changed += _Event;
						dw.Renamed += _Event;
						dw.EnableRaisingEvents = true;
						_a.Add(dw);
					}
					catch (Exception ex) { //eg directory does not exist
						dw?.Dispose();
						print.warning($"Cannot watch '{file}'. {ex}");
						return null;
					}
				}
				
				FileWatcher f = new(dw, file, modifiedExternally);
				if (!dw.dFiles.TryAdd(pathname.getName(file), f)) throw new NotSupportedException($"A FileWatcher for '{file}' already exists");
				
				return f;
			}
			
			static void _Event(object sender, FileSystemEventArgs e) {
				var dw = sender as _DirectoryWatcher;
				if (dw.dFiles.TryGetValue(e.Name, out var f)) f._Event(e);
			}
		}
		
		public void Remove(FileWatcher f) {
			lock (this) {
				var dw = f._dirWatcher as _DirectoryWatcher;
				dw.dFiles.Remove(pathname.getName(f._file));
				if (dw.dFiles.Count == 0) {
					_a.Remove(dw);
					dw.Dispose();
				}
			}
		}
		
		public void DisposeAll() {
			lock (this) {
				foreach (var dw in _a) {
					dw.Dispose();
					foreach (var f in dw.dFiles.Values) f.DisposeNoRemove_();
					dw.dFiles.Clear();
				}
				_a.Clear();
			}
		}
		
		public _Watchers() {
			//It's better to dispose/stop everything on process exit as soon as possible, to avoid invoking ModifiedExternally while/after clients execute process.thisProcessExit etc handlers.
			System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += c => DisposeAll(); //before thisProcessExit/ProcessExit. Not on unhandled exception.
			AppDomain.CurrentDomain.UnhandledException += (_, _) => DisposeAll(); //never mind: maybe not the first event handler. Unlikely something bad can happen. If using JSettings, its process.thisProcessExit calls DisposeAll before saving all.
		}
	}
}
