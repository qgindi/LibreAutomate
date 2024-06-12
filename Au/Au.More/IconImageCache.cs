using System.Drawing;
using Microsoft.Win32;

namespace Au.More;

/// <summary>
/// Gets images as <see cref="Bitmap"/> of same logical size to be displayed as icons. Can get file icons or load images from files etc.
/// </summary>
/// <remarks>
/// Uses memory cache and optionally file cache to avoid loading same image multiple times. Getting images from cache is much faster.
///
/// <b>Bitmap</b> objects retrieved by this class are stored in memory cache. Don't dispose them before disposing the <b>IconImageCache</b> object. Usually don't need to dispose these <b>Bitmap</b> objects explicitly (GC will do it).
/// 
/// Thread-safe.
/// </remarks>
public sealed class IconImageCache : IDisposable {
	record class _DpiImages {
		readonly IconImageCache _cache;
		public readonly int dpi;
		public readonly string indexFile;
		public Dictionary<string, Hash.MD5Result> dNameHash; //index file data
		public readonly Dictionary<string, Bitmap> dNameBitmap = new(); //memory cache
		public readonly Dictionary<Hash.MD5Result, Bitmap> dHashBitmap = new(); //let all identical images share single Bitmap object
		
		//info: dictionary string keys are case-sensitive. We have not only paths but also base64 MD5. For paths we call Lower.
		
		public _DpiImages(IconImageCache cache, int dpi) {
			_cache = cache;
			this.dpi = dpi;
			if (_cache._dir is string s) indexFile = s + "\\" + dpi.ToS() + ".dpi";
		}
		
		/// <summary>
		/// If the index file for this DPI still not loaded, loads it into <b>dNameHash</b>.
		/// </summary>
		public void LoadIndexFile() {
			if (dNameHash == null && filesystem.exists(indexFile, useRawPath: true)) {
				try {
					bool save = false;
					Dictionary<string, Hash.MD5Result> d = new(StringComparer.OrdinalIgnoreCase);
					filesystem.waitIfLocked(() => {
						var fs = File.OpenRead(indexFile);
						using var br = new BinaryReader(fs);
						for (var len = fs.Length; fs.Position < len;) {
							var imageKey = br.ReadString();
							Hash.MD5Result hash = new(br.ReadInt64(), br.ReadInt64());
							ref var r = ref d.GetValueRefOrAddDefault_(imageKey, out bool exists);
							r = hash;
							if (exists) save = true; //save without duplicates. Keep the newest hash.
						}
					});
					dNameHash = d;
					if (save) _SaveIndex();
				}
				catch (Exception e1) { Debug_.Print(e1); }
			}
			dNameHash ??= new();
		}
		
		void _SaveIndex() {
			//print.it("<><c red>SaveIndex<>");
			filesystem.waitIfLocked(() => {
				var fs = File.Create(indexFile);
				using var bw = new BinaryWriter(fs);
				foreach (var v in dNameHash) {
					bw.Write(v.Key);
					bw.Write(v.Value.r1);
					bw.Write(v.Value.r2);
				}
			});
		}
		
		public void AppendToIndexFile(string imageKey, Hash.MD5Result hash) {
			filesystem.waitIfLocked(() => {
				var fs = File.Open(indexFile, FileMode.Append);
				using var bw = new BinaryWriter(fs);
				bw.Write(imageKey);
				bw.Write(hash.r1);
				bw.Write(hash.r2);
			});
		}
	}
	
	readonly string _dir;
	readonly List<_DpiImages> _aDpi = new(); //for each used DPI
	readonly int _imageSize;
	bool _disposed, _onceUsedFiles;
	readonly HashSet<string> _extDynamicIcon = new();
	
	static List<WeakReference<IconImageCache>> s_caches = new();
	
	/// <param name="imageSize">Width and height of images. Min 16, max 256.</param>
	/// <param name="directory">Path of cache directory. If <c>null</c>, will be used only memory cache.</param>
	/// <exception cref="ArgumentException">Not full path.</exception>
	public IconImageCache(int imageSize = 16, string directory = null) {
		if (imageSize < 16 || imageSize > 256) throw new ArgumentOutOfRangeException(nameof(imageSize));
		_imageSize = imageSize;
		//directory = null; //test memory-only cache
		if (directory != null) _dir = pathname.normalize(directory);
		
		lock (s_caches) s_caches.Add(new(this));
		
		//_InitNotifyWindow();
		script.GetAuxThread_(); //ensure the aux thread is running. It calls ClearAll_ when receives message "clear image caches".
	}
	
	/// <summary>
	/// Common cache for icons of size 16. Used by menus, toolbars and editor.
	/// </summary>
	/// <remarks>
	/// If <c>script.role != SRole.ExeProgram &amp;&amp; folders.thisAppDriveType == DriveType.Removable</c>, uses cache directory <c>folders.ThisAppDataLocal + "iconCache"</c>. Else uses only memory cache.
	/// </remarks>
	public static IconImageCache Common => s_common.Value;
	
	static readonly Lazy<IconImageCache> s_common = new(() => {
		string dir = script.role != SRole.ExeProgram && folders.thisAppDriveType == DriveType.Removable ? null : (folders.ThisAppDataLocal + "iconCache");
		return new(16, dir);
	});
	
	/// <summary>
	/// Gets image from cache or file etc.
	/// </summary>
	/// <param name="imageSource">File path, or resource path that starts with <c>"resources/"</c> or has prefix <c>"resource:"</c>, or icon name like <c>"*Pack.Icon color"</c>, etc. See <i>isImage</i> parameter.</param>
	/// <param name="dpi">DPI of window that will display the image. See <see cref="Dpi"/>.</param>
	/// <param name="isImage">
	/// <c>false</c> - get file/folder/filetype/url/etc icon with <see cref="icon.of"/>. If <i>imageSource</i> is relative path of a <c>.cs</c> file, gets its custom icon as image; returns <c>null</c> if no custom icon or if editor isn't running.
	/// <c>true</c> - load image from xaml/png/etc file, database, resource or string with <see cref="ImageUtil.LoadGdipBitmap"/> or <see cref="ImageUtil.LoadWpfImageElement"/>. Can be icon name like <c>"*Pack.Icon color"</c> (see menu <b>Tools > Icons</b>).
	/// 
	/// To detect whether a string is an image, call <see cref="ImageUtil.HasImageOrResourcePrefix"/>; if it returns <c>true</c>, it is image.
	/// </param>
	/// <param name="onException">Action to call when fails to load image. If <c>null</c>, then silently returns <c>null</c>. Parameters are image source string and exception.</param>
	public unsafe Bitmap Get(string imageSource, int dpi, bool isImage, Action<string, Exception> onException = null) {
		//print.it(imageSource, isImage);
		if (_disposed) throw new ObjectDisposedException(nameof(IconImageCache));
		bool isXaml = isImage && (imageSource.Starts('<') || imageSource.Ends(".xaml", true));
		bool isStore = !isImage && imageSource.Starts(@"shell:AppsFolder\"); //compare case-sensitive. Then users can pass eg "shell:appsFolder..." to display white icons in blue background.
		bool ofWorkspaceFile = false;
		if (!isImage && !isStore && imageSource.Ends(".cs", true) && !pathname.isFullPath(imageSource, orEnvVar: true)) { //eg `script.run(@"x.cs");`
			imageSource = ScriptEditor.GetIcon(imageSource, EGetIcon.PathToIconName);
			if (imageSource == null) return null;
			isImage = ofWorkspaceFile = true;
			//rejected: use Dictionary<imageSource, iconName> to avoid frequent GetIcon for same imageSource. In LA process fast, elsewhere not too slow.
			//rejected: Move this code to the caller that needs it (MTBase).
		}
		bool isIconName = isImage && !isXaml && imageSource.Starts('*');
		if (isIconName) {
			isXaml = true;
			imageSource = WpfUtil_.NormalizeIconStringColor(imageSource, false); //color can be "normal|highContrast"
		}
		if (!isXaml && !isStore) dpi = 96; //will scale when drawing, it's fast and not so bad. Tested scaling with Lanczos3 etc filters, but the result for icons isn't better.
		string imageKey = imageSource;
		if (!isIconName) {
			if ((isXaml && imageKey.Starts('<')) || (isImage && ImageUtil.HasImageStringPrefix(imageKey))) imageKey = Hash.MD5(imageSource, base64: true);
			else imageKey = imageKey.Lower();
		}
		bool isIco = !isImage && !isStore && imageKey.Ends(".ico");
		
		lock (this) {
			//get _DpiImages for dpi
			_DpiImages dd;
			foreach (var v in _aDpi) if (v.dpi == dpi) { dd = v; goto g1; }
			_aDpi.Add(dd = new _DpiImages(this, dpi));
			g1:
			
			if (dd.dNameBitmap.TryGetValue(imageKey, out var b)) return b;
			
			//print.it(imageSource, isImage);
			//using var p1 = perf.local();
			
			//bool useHash = !isImage && !isIco;
			bool useHash = isImage ? isIconName : !isIco; //use file cache for *icon too
			
			//use file cache for *icon too.
			//	Because:
			//		Loads XAML icon slowly first time (~100 ms; not measured after reboot), even in LA, and even later loads slower than from the cache file.
			//		Non-WPF process uses much more memory (because loads WPF), eg 14 -> 28 MB.
			//	Bad: when trying to find icons, users try many icons, colors, sizes. Then the cache is full of garbage.
			//		TODO3: remove from cache if not using anymore. Or add only if frequently using.
			//FUTURE: to make loading XAML icons faster etc, try Windows.UI.Xaml.Markup.XamlReader.Load. Use Microsoft.Windows.SDK.NET.dll, or directly COM if possible. When the library will not support Win7/8.
			
			bool useFile = _dir != null && useHash;
			if (useFile) {
				try {
					if (!_onceUsedFiles) {
						_onceUsedFiles = true;
						var fe = filesystem.exists(_dir);
						if (fe.File) filesystem.delete(_dir); //fbc (was .db file)
						if (!fe.Directory) filesystem.createDirectory(_dir);
						_InitFiles();
					}
					
					dd.LoadIndexFile();
					
					bool useExt = false; g2:
					if (dd.dNameHash.TryGetValue(imageKey, out var hash)) {
						if (dd.dHashBitmap.TryGetValue(hash, out var bb)) return bb;
						var path = _dir + "\\" + hash.ToString() + ".png";
						try {
							//b = (Bitmap)Image.FromFile(path); //no, locks file
							using (var stream = File.OpenRead(path)) b = (Bitmap)Image.FromStream(stream);
							dd.dNameBitmap[imageKey] = b;
							dd.dHashBitmap[hash] = b;
							return b;
						}
						catch (Exception e1) { Debug_.PrintIf(filesystem.exists(path), e1); }
					} else if (!useExt && !isImage && !isStore && !imageKey.Ends(".exe") && !imageKey.Ends(".lnk") && pathname.isFullPath(imageSource) && filesystem.exists(imageSource, useRawPath: true).File) {
						var ext = pathname.getExtension(imageKey);
						bool noExt = ext.Length == 0;
						if (noExt) ext = ".#"; //will get the icon of unknown file types
						if (dd.dNameBitmap.TryGetValue(ext, out var b1)) return b1;
						if (!_extDynamicIcon.Contains(ext)) {
							if (!noExt && _DynamicIcon(ext)) _extDynamicIcon.Add(ext); else { imageKey = imageSource = ext; useExt = true; goto g2; }
						}
					}
				}
				catch (Exception e1) { print.warning(e1); useFile = false; } //failed to create _dir
			}
			//p1.Next('C');
			
			try {
				//long t1 = perf.mcs;
				if (!isImage) {
					b = isStore ? icon.winStoreAppImage(imageSource, Dpi.Scale(_imageSize, dpi)) : null;
					b ??= icon.of(imageSource, _imageSize)?.ToGdipBitmap();
				} else {
					if (isIconName) {
						imageSource = ScriptEditor.GetIcon_(imageSource, EGetIcon.IconNameToXaml, skipResources: ofWorkspaceFile);
						//p1.Next('X');
						if (imageSource == null) return null;
					}
					if (isXaml) b = ImageUtil.LoadGdipBitmapFromXaml(imageSource, dpi, (_imageSize, _imageSize));
					else b = ImageUtil.LoadGdipBitmap(imageSource);
				}
				//if (useFile) useFile = perf.mcs - t1 > 1000; //reduces the index file size in worst cases, but makes significantly slower later
			}
			catch (Exception ex) {
				if (onException != null) onException(imageSource, ex);
				//else print.warning("IconImageCache.Get() failed. " + ex.ToStringWithoutStack()); //no. Often prints while editing text if editor shows images in text.
			}
			//p1.Next('L');
			
			if (b != null && (isImage ? useFile : !isIco)) {
				try {
					var ms = new MemoryStream();
					b.Save(ms, System.Drawing.Imaging.ImageFormat.Png); //~200 mcs. It's fast if compared with icon.of etc and saving.
					var hash = Hash.MD5(ms.GetBuffer().AsSpan(0, (int)ms.Position));
					
					ref var br = ref dd.dHashBitmap.GetValueRefOrAddDefault_(hash, out bool exists);
					if (!exists) br = b; else { b.Dispose(); b = br; }
					
					if (useFile) {
						//p1.Next();
						if (!exists) {
							//print.it("<><c green>save<>");
							filesystem.waitIfLocked(() => {
								using var fs = File.Create($@"{_dir}\{hash.ToString()}.png");
								fs.Write(ms.GetBuffer().AsSpan(0, (int)ms.Position));
							});
						}
						dd.dNameHash[imageKey] = hash;
						//p1.Next();
						dd.AppendToIndexFile(imageKey, hash);
					}
				}
				catch (Exception e1) { Debug_.Print(e1); }
			}
			
			dd.dNameBitmap[imageKey] = b;
			return b;
		}
		
		//rejected: Don't cache if non-literal (non-interned) string. Caller may generate many random strings, eg icon colors. Impossible to detect reliably.
		
		static bool _DynamicIcon(string ext) {
			if (Registry.GetValue(@"HKEY_CLASSES_ROOT\" + ext, "", null) is string s1) {
				//if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + ext + @"\UserChoice", "ProgId", null) is string s2) return _DI(s2); //it seems Windows ignores this when looking for icon handler or %1
				if (_DI(s1)) return true;
			}
			return false;
			
			static bool _DI(string progid)
				=> Registry.GetValue(@"HKEY_CLASSES_ROOT\" + progid + @"\ShellEx\IconHandler", "", null) is string
				|| Registry.GetValue(@"HKEY_CLASSES_ROOT\" + progid + @"\DefaultIcon", "", null) is "\"%1\"" or "%1"; //exe, ico, cur, ani
		}
	}
	
	void _InitFiles() {
		if (_dir == null) return;
		
		//delete cache files if changed OS/.NET/Au version
		var verPath = _dir + @"\version.txt";
		try {
			var sNew = osVersion.onaString;
			if (filesystem.exists(verPath, useRawPath: true)) {
				var sOld = filesystem.loadText(verPath);
				if (sNew == sOld) return;
				_ClearFiles();
			}
			filesystem.saveText(verPath, sNew);
		}
		catch (Exception e1) { Debug_.Print(e1); }
	}
	
	/// <summary>
	/// Removes images from memory cache (but does not dispose) and makes this object unusable.
	/// Optional.
	/// </summary>
	public void Dispose() {
		_disposed = true;
		_aDpi.Clear();
		_Dispose();
		GC.SuppressFinalize(this);
	}
	
	///
	~IconImageCache() { /*print.it("~IconImageCache");*/ _Dispose(); }
	
	void _Dispose() {
		lock (s_caches) {
			for (int i = s_caches.Count; --i >= 0;) {
				if (!s_caches[i].TryGetTarget(out var v) || v == this) s_caches.RemoveAt(i);
			}
		}
	}
	
	/// <summary>
	/// Clears the cache (removes images from memory cache and file cache).
	/// </summary>
	/// <param name="redrawWindows">Redraw (asynchronously) all visible windows of this process.</param>
	public void Clear(bool redrawWindows = false) {
		if (_disposed) throw new ObjectDisposedException(nameof(IconImageCache));
		if (_Clear() && redrawWindows) _RedrawWindowsOfThisProcess();
	}
	
	bool _Clear() {
		lock (this) {
			if (_disposed) return false;
			_aDpi.Clear();
			_extDynamicIcon.Clear();
		}
		_ClearFiles();
		Cleared?.Invoke();
		return true;
	}
	
	/// <summary>
	/// When the cache cleared.
	/// </summary>
	public event Action Cleared;
	
	void _ClearFiles() => _ClearFiles(_dir);
	
	static void _ClearFiles(string dir) {
		if (dir == null || !filesystem.exists(dir, true).Directory) return;
		try {
			foreach (var v in Directory.GetFiles(dir, "*.dpi")) filesystem.delete(v);
			foreach (var v in Directory.GetFiles(dir, "*.png")) Api.DeleteFile(v);
		}
		catch (Exception e1) { Debug_.Print(e1); }
	}
	
	static unsafe void _RedrawWindowsOfThisProcess() {
		foreach (var w in wnd.findAll(of: WOwner.Process(process.thisProcessId), flags: WFlags.CloakedToo))
			Api.RedrawWindow(w, flags: Api.RDW_INVALIDATE | Api.RDW_ALLCHILDREN);
	}
	
	/// <summary>
	/// Clears caches of all <b>IconImageCache</b> instances of this or all processes. Redraws (asynchronously) all visible windows of these processes.
	/// </summary>
	/// <param name="allProcesses">Clear in all processes of this user session.</param>
	public static void ClearAll(bool allProcesses = true) {
		ClearAll_();
		if (allProcesses) {
			//if called in LA process (eg menu Tools > Update icons), clear the cache dir of the common cache of scripts.
			//	Without it would clear only if some script processes already used the cache.
			if (script.role == SRole.EditorExtension && Common._dir is string s1) {
				Debug.Assert(s1.Ends(@"\iconCache"));
				_ClearFiles(s1.Insert(^10, @"\_script"));
			}
			
			for (var w = wnd.findFast(null, script.c_auxWndClassName, true); !w.Is0; w = wnd.findFast(null, script.c_auxWndClassName, true, w))
				if (!w.IsOfThisProcess) w.SendNotify(script.c_msg_IconImageCache_ClearAll);
		}
	}
	
	internal static void ClearAll_() {
		List<IconImageCache> a = new();
		lock (s_caches) foreach (var c in s_caches) if (c.TryGetTarget(out var v)) a.Add(v);
		bool redrawWindows = false;
		foreach (var v in a) redrawWindows |= v._Clear();
		if (redrawWindows) _RedrawWindowsOfThisProcess();
	}
	
	//static int s_auxInited;
	
	//static void _InitNotifyWindow() {
	//	//if (0 == Interlocked.Exchange(ref s_auxInited, 1)) {
	//	//	var t = script.GetAuxThread_();
	//	//	t.QueueAPC(_InitAuxThread);
	//	//}
	
	//	//static void _InitAuxThread() {
	//	//	//rejected. Assoc may be changed frequently. The cache probably even does not contain icons of those file types.
	//	//	//	Anyway cannot auto-clear if changed while cache not running.
	//	//	//	Also cannot auto-clear when icons changed not because of a changed assoc. Eg changed .exe icon, edited .ico, changed .lnk icon.
	//	//	//using var pidl = Pidl.FromString(":: ");
	//	//	//var e = new api.SHChangeNotifyEntry { pidl = pidl.UnsafePtr, fRecursive = true };
	//	//	//Api.SHChangeNotifyRegister(script.AuxWnd_, api.SHCNRF_ShellLevel, api.SHCNE_ASSOCCHANGED, script.c_msg_IconImageCache_ClearAll, 1, e);
	//	//	////undocumented: is it important to call SHChangeNotifyDeregister when this process ends?
	//	//}
	//}
}
