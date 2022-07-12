using System.Drawing;

namespace Au.More
{
	/// <summary>
	/// Gets <see cref="Bitmap"/> images of same logical size to be displayed as icons. Can get file icons or load from files/resources/strings.
	/// </summary>
	/// <remarks>
	/// Uses memory cache and optionally file cache to avoid loading same image multiple times. Getting images from cache is much faster.
	/// Thread-safe.
	/// </remarks>
	public sealed class IconImageCache : IDisposable
	{
		record class _DpiImages
		{
			public readonly int dpi;
			public readonly string table;
			public readonly Dictionary<string, Bitmap> images = new(); //case-sensitive, because we have not only paths but also base64 MD5. For paths we call Lower.
			public sqliteStatement sGet, sInsert;
			public bool tableCreated;

			public _DpiImages(int dpi) {
				this.dpi = dpi;
				table = "icons" + dpi;
			}

			public void DisposeDB() {
				sGet?.Dispose(); sGet = null;
				sInsert?.Dispose(); sInsert = null;
			}
		}

		readonly List<_DpiImages> _aDpi = new(); //for each used DPI
		readonly Dictionary<Hash.MD5Result, Bitmap> _dHash = new(); //let all identical "icon.of" images (eg of all .txt files) share single Bitmap object
		readonly int _imageSize;
		readonly string _dbFile;
		sqlite _sqlite;
		sqliteStatement _sHashGet, _sHashInsert;
		readonly MemoryStream _ms = new();
		bool _disposed;

		/// <param name="imageSize">Width and height of images. Min 16, max 256.</param>
		/// <param name="file">Path of cache file (SQLite database). If null, will be used only memory cache.</param>
		public IconImageCache(int imageSize = 16, string file = null) {
			if (imageSize < 16 || imageSize > 256) throw new ArgumentOutOfRangeException(nameof(imageSize));
			_imageSize = imageSize;
			_dbFile = file;
		}

		/// <summary>
		/// Common cache for icons of size 16. Used by menus, toolbars and editor.
		/// </summary>
		/// <remarks>
		/// If <c>script.role != SRole.ExeProgram &amp;&amp; folders.thisAppDriveType == DriveType.Removable</c>, uses cache file <c>folders.ThisAppDataLocal + @"iconCache16.db"</c>. Else uses only memory cache.
		/// </remarks>
		public static IconImageCache Common => s_common.Value;

		static readonly Lazy<IconImageCache> s_common = new(() => {
			string file = script.role != SRole.ExeProgram && folders.thisAppDriveType == DriveType.Removable ? null : (folders.ThisAppDataLocal + @"iconCache16.db");
			return new(16, file);
		});

		/// <summary>
		/// Gets image from memory cache or file or resource.
		/// </summary>
		/// <param name="imageSource">File path, or resource path that starts with "resources/" or has prefix "resource:", etc. See <i>isImage</i> parameter.</param>
		/// <param name="dpi">DPI of window that will display the image. See <see cref="Dpi"/>.</param>
		/// <param name="isImage">
		/// false - get file/folder/filetype/url/etc icon with <see cref="icon.of"/>. If <i>imageSource</i> is relative path of a .cs file, gets its custom icon as image; returns null if no custom icon or if editor isn't running.
		/// true - load image from xaml/png/etc file, resource or string with <see cref="ImageUtil.LoadGdipBitmap"/> or <see cref="ImageUtil.LoadWpfImageElement"/>. Can be icon name like "*Pack.Icon color" (see menu Tools -> Icons).
		/// 
		/// To detect whether as string is an image, call <see cref="ImageUtil.HasImageOrResourcePrefix"/>; if it returns true, it is image.
		/// </param>
		/// <param name="onException">Action to call when fails to load image. If null, then silently returns null. Parameters are image source string and exception.</param>
		public unsafe Bitmap Get(string imageSource, int dpi, bool isImage, Action<string, Exception> onException = null) {
			if (_disposed) throw new ObjectDisposedException(nameof(IconImageCache));
			//var p1 = perf.local();
			lock (this) {
				bool isXaml = isImage && (imageSource.Starts('<') || imageSource.Ends(".xaml", true));
				if (!isImage && imageSource.Ends(".cs", true) && !pathname.isFullPath(imageSource, orEnvVar: true)) {
					imageSource = script.editor.GetIcon(imageSource, EGetIcon.PathToIconName);
					//p1.Next('x');
					if (imageSource == null) return null;
					isImage = true;
					//SHOULDDO: use Dictionary<imageSource, iconName> to avoid frequent GetIcon for same imageSource. It seems currently don't need it for this library.
					//rejected: Move this code to the caller that needs it (MTBase).
				}
				bool isIconName = isImage && !isXaml && imageSource.Starts('*');
				if (isIconName) isXaml = true;
				if (!isXaml) dpi = 96; //will scale when drawing, it's fast and not so bad
				string imageKey = imageSource;
				if (!isIconName) {
					if ((isXaml && imageKey.Starts('<')) || (isImage && ImageUtil.HasImageStringPrefix(imageKey))) imageKey = Hash.MD5(imageSource, base64: true);
					//else imageKey = imageKey.Lower(); //not necessary
				}

				//get _DpiData for dpi
				_DpiImages dd;
				foreach (var v in _aDpi) if (v.dpi == dpi) { dd = v; goto g1; }
				_aDpi.Add(dd = new _DpiImages(dpi));
				g1:

				//find or load
				if (!dd.images.TryGetValue(imageKey, out var b)) {
					bool useDB = _dbFile != null, inDB = false;
					Hash.MD5Result hash = default;
					if (useDB) {
						sqliteStatement sGet = null;
						try {
							if (_sqlite == null) {
								_sqlite = new(_dbFile, sql: @"PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=100;");
								process.thisProcessExit += _ => _Close();
							}

							if (!dd.tableCreated) {
								_sqlite.Execute($"CREATE TABLE IF NOT EXISTS {dd.table} (key TEXT PRIMARY KEY, data BLOB)");
								dd.tableCreated = true;
							}

							if (isImage) {
								sGet = dd.sGet ??= _sqlite.Statement($"SELECT data FROM {dd.table} WHERE key=?");
							} else {
								if (_sHashGet == null) {
									_sqlite.Execute($"CREATE TABLE IF NOT EXISTS hashed (hash BLOB PRIMARY KEY, data BLOB)");
									_sHashGet = _sqlite.Statement($"SELECT data,hash FROM hashed WHERE hash=(SELECT data FROM {dd.table} WHERE key=?)");
								}
								sGet = _sHashGet;
							}

							if (sGet.Bind(1, imageKey).Step()) {
								bool haveBitmap = false;
								if (!isImage) {
									hash = sGet.GetStruct<Hash.MD5Result>(1);
									haveBitmap = _dHash.TryGetValue(hash, out b);
								}
								if (!haveBitmap) {
									var blob = sGet.GetBlob(0, out int blobLen);
									if (blobLen > 0) {
										//p1.Next();
										_ms.Position = 0; _ms.Write(new(blob, blobLen));
										_ms.Position = 0; b = Image.FromStream(_ms, false, false) as Bitmap;
									}
									if (!isImage) _dHash[hash] = b;
								}
								inDB = true;
							}
						}
						catch (Exception e1) { Debug_.Print(e1); useDB = false; }
						finally { sGet?.Reset(); }
					}

					//p1.Next();
					if (!inDB) {
						//CONSIDER: don't use file cache in exe.
						//CONSIDER: Don't cache if non-literal (non-interned) string. Caller may generate many random strings, eg icon colors.
						//	But be careful and detect it earlier, because some our functions parse literal string.
						//	Probably can't detect, because the string may be retrieved from resources etc.

						try {
							if (!isImage) {
								b = icon.of(imageSource, _imageSize)?.ToGdipBitmap();
							} else {
								if (isIconName) {
									imageSource = script.editor.GetIcon(imageSource, EGetIcon.IconNameToXaml);
									//p1.Next('X');
									if (imageSource == null) return null;
								}
								if (isXaml) b = ImageUtil.LoadGdipBitmapFromXaml(imageSource, dpi, (_imageSize, _imageSize));
								else b = ImageUtil.LoadGdipBitmap(imageSource);
							}
						}
						catch (Exception ex) {
							if (onException != null) onException(imageSource, ex);
							//else print.warning("IconImageCache.Get() failed. " + ex.ToStringWithoutStack()); //no. Often prints while editing text.
						}
					}
					//p1.Next('L');

					Span<byte> span = default;
					if (!inDB && (useDB || !isImage) && b != null) {
						_ms.Position = 0; b.Save(_ms, System.Drawing.Imaging.ImageFormat.Png); //~300 mcs. It's fast if compared with icon.of etc.
						span = _ms.GetBuffer().AsSpan(0, (int)_ms.Position);
						if (!isImage) {
							Hash.MD5Context md = default; md.Add(span); hash = md.Hash; //fast
							if (!_dHash.TryAdd(hash, b)) { b.Dispose(); b = _dHash[hash]; }
							//also tested hashing bits from hicon directly, to avoid ToGdipBitmap for duplicates.
							//	It makes faster for duplicates, else slower.
							//	Anyway icon.of is much slower. Better use smaller code.
						}
					}

					dd.images[imageKey] = b;
					//print.it(imageKey, inDB, b);

					//p1.Next('b');
					if (useDB && !inDB) {
						try {
							using var tra = _sqlite.Transaction();

							if (!isImage && b != null) {
								_sHashInsert ??= _sqlite.Statement($"INSERT OR IGNORE INTO hashed VALUES (?, ?)");
								_sHashInsert.BindStruct(1, hash).Bind(2, span).Step();
							}

							var si = dd.sInsert ??= _sqlite.Statement($"INSERT OR IGNORE INTO {dd.table} VALUES (?, ?)");
							si.Bind(1, imageKey);
							if (b == null) si.BindNull(2); else if (isImage) si.Bind(2, span); else si.BindStruct(2, hash);
							si.Step();

							tra.Commit();
						}
						catch (Exception e1) { Debug_.Print(e1); }
						finally { dd.sInsert?.Reset(); _sHashInsert?.Reset(); }
					}
				}
				//p1.Next();
				//print.it($"{p1.ToString(),-50}  {imageKey}  {b?.GetHashCode() ?? 0}");
				return b;
			}
		}

		/// <summary>
		/// Removes images from memory cache, closes database file and makes this object unusable.
		/// Optional; if not called, the cache will be disposed by GC or on process exit.
		/// </summary>
		public void Dispose() {
			_disposed = true;
			_Close();
			GC.SuppressFinalize(this);
		}

		///
		~IconImageCache() => _Close();

		void _Close(bool clear = false) {
			lock (this) {
				if (clear && _dbFile != null) {
					//note: cannot simply delete file after closing DB. Fails if used by another process.
					try {
						_sqlite ??= new(_dbFile, sql: @"PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=100;");
						//_sqlite.Execute("DELETE FROM (SELECT name FROM sqlite_master WHERE type='table'); VACUUM;"); //error. SELECT is not supported here.
						using var tra = _sqlite.Transaction();
						using var sTables = _sqlite.Statement("SELECT name FROM sqlite_master WHERE type='table'");
						while (sTables.Step()) {
							//print.it(sTables.GetText(0));
							_sqlite.Execute("DELETE FROM " + sTables.GetText(0));
						}
						tra.Commit();
						_sqlite.Execute("VACUUM");
					}
					catch (Exception e1) { print.warning("Failed to clear icon cache. " + e1.ToStringWithoutStack()); }
				}

				if (_sqlite != null) {
					foreach (var v in _aDpi) v.DisposeDB();
					_sHashGet?.Dispose(); _sHashGet = null;
					_sHashInsert?.Dispose(); _sHashInsert = null;
					_sqlite.Dispose(); _sqlite = null;
				}
				_aDpi.Clear();
				_dHash.Clear();
				//note: can't dispose bitmaps. Somebody may still use.
			}
		}

		/// <summary>
		/// Removes images from memory cache and database file.
		/// </summary>
		/// <param name="redrawWindows">Redraw (asynchronously) all visible windows of this thread.</param>
		public unsafe void Clear(bool redrawWindows = false) {
			if (_disposed) throw new ObjectDisposedException(nameof(IconImageCache));
			_Close(clear: true);
			if (redrawWindows) {
				foreach (var w in wnd.getwnd.threadWindows(process.thisThreadId, onlyVisible: true))
					Api.RedrawWindow(w, flags: Api.RDW_INVALIDATE | Api.RDW_ALLCHILDREN);
				//FUTURE: redraw all windows that use this cache, of all processes. Not only redraw, but let they dispose their bitmaps and get from cache again. Now eg toolbars get bitmaps once.
			}
		}
	}
}
