using System.Drawing;
using System.Reflection.Emit;

namespace Au {
	/// <summary>
	/// Gets icons from/of files etc. Contains native icon handle.
	/// </summary>
	/// <remarks>
	/// Native icons must be destroyed. An <b>icon</b> variable destroys its native icon when disposing. To dispose, call <b>Dispose</b> or use <c>using</c> statement. Or use functions like <see cref="ToGdipBitmap"/>, <see cref="ToWpfImage"/>; by default they dispose the <b>icon</b> variable. It's OK to not dispose if you use few icons; GC will do it.
	/// </remarks>
	public class icon : IDisposable { //rejected: base SafeHandle.
		IntPtr _handle;
		
		/// <summary>
		/// Sets native icon handle.
		/// The icon will be destroyed when disposing this variable or when converting to object of other type.
		/// </summary>
		public icon(IntPtr hicon) {
			Debug_.PrintIf(hicon == default, "hicon == default");
			
			//Don't allow to exceed the process handle limit when the program does not dispose them. Default limits are 10000, but min 200.
			//Icons are USER objects. They also usually create 3 GDI objects (bitmaps?). So a process can have max ~3300 icons by default.
			if (hicon != default) { GC_.UserHandleCollector.Add(); GC_.GdiHandleCollector.Add(); GC_.GdiHandleCollector.Add(); GC_.GdiHandleCollector.Add(); }
			//rejected: Add property for native icon ownership or methods for refcounting.
			//	Usually a process uses few icons. If many, the programmer knows the importance of disposing icons; or HandleCollector helps.
			
			_handle = hicon;
		}
		
		//FUTURE: FromGdipIcon, FromStream.
		
		static icon _New(IntPtr hi) => hi != default ? new(hi) : null;
		
		/// <summary>
		/// Destroys native icon handle.
		/// </summary>
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this); //never mind: actually this should be in Detach, but then intellisense gives 2 notes
		}
		
		///
		protected virtual void Dispose(bool disposing) {
			var h = Detach();
			if (h != default) Api.DestroyIcon(h);
		}
		
		///
		~icon() { Dispose(false); }
		
		/// <summary>
		/// Clears this variable and returns its native icon handle.
		/// </summary>
		public IntPtr Detach() {
			var h = _handle;
			if (_handle != default) {
				_handle = default;
				GC_.UserHandleCollector.Remove(); GC_.GdiHandleCollector.Remove(); GC_.GdiHandleCollector.Remove(); GC_.GdiHandleCollector.Remove();
			}
			return h;
		}
		
		/// <summary>
		/// Gets native icon handle.
		/// </summary>
		public IntPtr Handle => _handle;
		
		/// <summary>
		/// Gets native icon handle.
		/// </summary>
		public static implicit operator IntPtr(icon i) => i?._handle ?? default;
		
		/// <summary>
		/// Gets icon that can be displayed for a file, folder, shell object, URL or file type.
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		/// <param name="file">
		/// Can be:
		/// <br/>• Path of any file or folder. Supports environment variables. If not full path, uses <see cref="folders.ThisAppImages"/> and <see cref="filesystem.searchPath"/>.
		/// <br/>• Any shell object, like <c>":: ITEMIDLIST"</c>, <c>@"::{CLSID-1}\::{CLSID-2}"</c>, <c>@"shell:AppsFolder\WinStoreAppId"</c>.
		/// <br/>• File type like <c>".txt"</c>, or protocol like <c>"http:"</c>. Use <c>"."</c> to get folder icon.
		/// <br/>• Path with icon resource index or negative id, like <c>"c:\file.dll,4"</c>, <c>"c:\file.exe,-4"</c>.
		/// <br/>• URL.
		/// </param>
		/// <param name="size">Icon width and height. Default 16.</param>
		/// <param name="flags"></param>
		/// <remarks>
		/// <b>ITEMIDLIST</b> can be of any file, folder, URL or a non-filesystem shell object. See <see cref="Pidl.ToHexString"/>.
		/// </remarks>
		public static icon of(string file, int size = 16, IconGetFlags flags = 0) {
			using var ds = new _DebugSpeed(file);
			return _OfFile(file, _NormalizeIconSizeArgument(size), flags);
		}
		
		static icon _OfFile(string file, int size = 16, IconGetFlags flags = 0) {
			if (file.NE()) return null;
			file = pathname.expand(file, strict: false);
			return _GetFileIcon(file, size, flags);
		}
		
		/// <summary>
		/// Gets icon of a file or other shell object specified as <b>ITEMIDLIST</b>.
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		/// <param name="pidl"><b>ITEMIDLIST</b>.</param>
		/// <param name="size">Icon width and height. Default 16.</param>
		public static icon ofPidl(Pidl pidl, int size = 16) {
			using var ds = new _DebugSpeed(pidl);
			return _OfPidl(pidl, _NormalizeIconSizeArgument(size));
		}
		
		static icon _OfPidl(Pidl pidl, int size) {
			if (pidl?.IsNull ?? true) return null;
			return _GetShellIcon(true, null, pidl, size);
		}
		
		static icon _GetFileIcon(string file, int size, IconGetFlags flags) {
			int index = 0;
			bool extractFromFile = false, isFileType = false, isURL = false, isShellPath = false, isPath = true;
			//bool getDefaultIfFails = 0!=(flags&IconGetFlags.DefaultIfFails);
			
			bool searchPath = 0 == (flags & IconGetFlags.DontSearch);
			
			if (0 == (flags & IconGetFlags.LiteralPath)) {
				//is ".ext" or "protocol:"?
				isFileType = pathname.IsExtension_(file) || (isURL = pathname.IsProtocol_(file));
				if (!isFileType) isURL = pathname.isUrl(file);
				if (isFileType || isURL || (isShellPath = file[0] == ':')) isPath = false;
				if (isPath) {
					//get icon index from "path,index" and remove ",index"
					extractFromFile = parsePathIndex(file, out file, out index);
					
					if (!searchPath) {
						if (!pathname.isFullPath(file)) file = folders.ThisAppImages + file;
						file = pathname.Normalize_(file, PNFlags.DontPrefixLongPath, noExpandEV: true);
					}
				}
			}
			
			if (isPath) {
				if (searchPath) {
					file = filesystem.searchPath(file, folders.ThisAppImages);
					if (file == null) return null; //ignore getDefaultIfFails
				}
				file = pathname.unprefixLongPath(file);
			}
			
			if (isPath /*&& (extractFromFile || 0==(flags&IconGetFlags.Shell))*/) {
				int ext = 0;
				if (!extractFromFile && file.Length > 4) ext = file.Ends(true, ".ico", ".exe", ".scr"/*, ".cur", ".ani"*/);
				if (extractFromFile || ext > 0) {
					var v = _Load(file, size, index);
					if (v != null || extractFromFile) return v;
					switch (filesystem.exists(file, true)) {
					case 0:
						return null;
					case 1:
						return stock(ext is 2 or 3 ? StockIcon.APPLICATION : StockIcon.DOCNOASSOC, size);
						//case 2: //folder name ends with .ico etc
					}
				} else if (file.Ends(".lnk", true)) {
					var v = _GetLnkIcon(file, size);
					if (v != null) return v;
					//print.it("_GetLnkIcon failed", file);
				}
				
				//note: here we don't cache icons.
				//Fast enough for where we use this. OS file buffers remain in memory for some time.
				//Where need, should instead use imagelists or some external cache that saves eg full toolbar bitmap.
				//SHGetFileInfo has its own cache. In some cases it makes faster (except first time in process), but using it to get all icons is much slower.
			}
			
			bool isExt = isFileType && !isURL;
			
			//Can use this code to avoid slow shell API if possible.
			//In some test cases can make ~2 times faster (with thread pool), especially in MTA thread.
			//But now, after other optimizations applied, in real life makes faster just 10-20%.
#if false
			//if(0==(flags&IconGetFlags.Shell)){
			string progId = isShellPath ? null : filesystem.more.getFileTypeOrProtocolRegistryKey(file, isFileType, isURL);

			RegistryKey rk = (progId == null) ? null : ARegistry.Open(progId, Registry.ClassesRoot);
			//print.it(file, progId, isFileType, isURL, rk != null);

			if(rk == null) {
				//Unregistered file type/protocol, no extension, folder, ::{CLSID}, shell:AppsFolder\WinStoreAppId, or no progId key in HKCR
				//print.it(@"unregistered", file, progId);
				if(progId != null) goto gr; //the file type is known, but no progid key in HKCR. Let shell API figure out. Rare.
				if(isExt || (isPath && filesystem.exists(file).File)) return Stock(StockIcon.DOCNOASSOC, size);
				goto gr;
			}

			//Registered file type/protocol.
			using(rk) {
				if(ARegistry.KeyExists(@"ShellEx\IconHandler", rk)) {
					//print.it(@"handler", file);
					goto gr;
				}

				string si;
				if(ARegistry.GetString(out si, "", @"DefaultIcon", rk) && si.Length > 0) {
					//print.it("registry: DefaultIcon", file, si);
					if(si[0] == '@') si = null; //eg @{Microsoft.Windows.Photos_16.622.13140.0_x64__8wekyb3d8bbwe?ms-resource://Microsoft.Windows.Photos/Files/Assets/PhotosLogoExtensions.png}
					else ParseIconLocation(ref si, out index);
				} else if(ARegistry.GetString(out si, "", @"shell\open\command", rk) && si.Length > 0) {
					//print.it(@"registry: shell\open\command", file, si);
					var a = si.SegSplit((si[0] == '"') ? "\"" : " ", StringSplitOptions.RemoveEmptyEntries);
					si = (a.Length == 0) ? null : a[0];
					if(si.Ends("rundll32.exe", true)) si = null;
				} else {
					si = null;
					//print.it("registry: no", file);
					//Usually shell API somehow gets si.
					//For example also looks in .ext > PerceivedType > HKCR\SystemFileAssociations.
					//We can use AssocQueryString(ASSOCSTR_DEFAULTICON), but it is slow and not always gets correct si.
				}

				//if(si != null) print.it(file, si);

				if(si == "%1") {
					//print.it(file);
					if(isPath) si = file;
					else si = null;
				}

				if(si != null) {
					si = pathname.expand(si);
					if(!pathname.isFullPath(si)) si = folders.System + si;
					vat v = _Load(si, size, index);
					if(v != null) return v;
				}
			}
			//}
			gr:
#endif
			return _GetShellIcon(!isExt, file, null, size);
		}
		
		//usePidl - if pidl not null/IsNull, use pidl, else convert file to PIDL. If false, pidl must be null.
		static icon _GetShellIcon(bool usePidl, string file, Pidl pidl, int size, bool freePidl = false) {
			//info:
			//	We support everything that can have icon - path, URL, protocol (eg "http:"), file extension (eg ".txt"), shell item parsing name (eg "::{CLSID}"), "shell:AppsFolder\WinStoreAppId".
			//	We call PidlFromString here and pass it to SHGetFileInfo. It makes faster when using thread pool, because multiple threads can call PidlFromString (slow) simultaneously.
			//	PidlFromString does not support file extension. SHGetFileInfo does not support URL and protocol, unless used PIDL.
			//	SHGetFileInfo gets the most correct icons, but only of standard sizes, which also depends on DPI and don't know what.
			//	IExtractIcon for some types fails or gets wrong icon. Even cannot use it to get correct-size icons, because for most file types it uses system imagelists, which are DPI-dependent.
			//	SHMapPIDLToSystemImageListIndex+SHGetImageList also is not better.
			
			//FUTURE: make faster "shell:...". Now eg 100 ms for Settings first time (50 ms Pidl.FromString_ and 50 ms SHGetFileInfo).
			//	Alternatives (not tested): https://stackoverflow.com/questions/32122679/getting-icon-of-modern-windows-app-from-a-desktop-application
			//	Not a big problem when using caching.
			//	Also some icons have incorrect background, eg Calculator.
			
			var pidl2 = pidl?.UnsafePtr ?? default;
			if (usePidl) {
				if (pidl2 == default) {
					pidl2 = Pidl.FromString_(file);
					if (pidl2 == default) usePidl = false; else freePidl = true;
				}
			}
			
			if (!usePidl) {
				Debug.Assert(pidl2 == default && file != null);
				pidl2 = Marshal.StringToCoTaskMemUni(file);
				freePidl = true;
			}
			
			if (pidl2 == default) return null;
			
			//This is faster but fails with some files etc, randomly with others.
			//It means that shell API and/or extensions are not thread-safe, even if can run in MTA.
			//return _GetShellIcon2(pidl2, size, usePidl);
			
			icon R;
			try {
				if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA) {
					R = _GetShellIcon2(usePidl, pidl2, size);
				} else {
					//tested: switching thread does not make slower. The speed depends mostly on locking, because then thread pool threads must wait.
#if true
					R = Task.Factory.StartNew(() => _GetShellIcon2(usePidl, pidl2, size), default, 0, StaTaskScheduler_.Default).Result;
#else //old code, uses ThreadPoolSTA_
					using var work = ThreadPoolSTA_.CreateWork(null, o => { R = _GetShellIcon2(usePidl, pidl2, size); });
					work.Submit();
					work.Wait();
#endif
				}
			}
			finally { if (freePidl) Marshal.FreeCoTaskMem(pidl2); }
			GC.KeepAlive(pidl);
			return R;
		}
		
		static icon _GetShellIcon2(bool isPidl, IntPtr pidl, int size) {
			IntPtr il = default; int index = -1, ilIndex, realSize;
			
			if (size < (realSize = 16) * 5 / 4) ilIndex = Api.SHIL_SMALL;
			else if (size < (realSize = 32) * 5 / 4) ilIndex = Api.SHIL_LARGE;
			else if (size < 256) {
				ilIndex = Api.SHIL_EXTRALARGE; realSize = 48;
				//info: cannot resize from 256 because GetIcon(SHIL_JUMBO) gives 48 icon if 256 icon unavailable. Getting real icon size is either impossible or quite difficult and slow (not tested).
			} else { ilIndex = Api.SHIL_JUMBO; realSize = 256; }
			
			using var dac = new Dpi.AwarenessContext(-1); //unaware
			
			//Need to lock this part, or randomly fails with some file types.
			lock ("TK6Z4XiSxkGSfC14/or5Mw") {
				try {
					uint fl = Api.SHGFI_SYSICONINDEX | Api.SHGFI_SHELLICONSIZE;
					if (ilIndex == Api.SHIL_SMALL) fl |= Api.SHGFI_SMALLICON;
					if (isPidl) fl |= Api.SHGFI_PIDL; else fl |= Api.SHGFI_USEFILEATTRIBUTES;
					il = Api.SHGetFileInfo(pidl, out var x, fl);
					if (il != default) index = x.iIcon;
					//Marshal.Release(il); //undocumented, but without it IImageList refcount grows. Probably it's ok, because it is static, never deleted until process exits.
				}
				catch { Debug_.Print("exception"); }
				//Shell extensions may throw.
				//By default .NET does not allow to handle eg access violation exceptions.
				//	Previously we would add [HandleProcessCorruptedStateExceptions], but Core ignores it.
				//	Now our AppHost sets environment variable COMPlus_legacyCorruptedStateExceptionsPolicy=1 before loading runtime.
				//	Or could move the API call to the C++ dll.
			}
			if (index < 0) return null;
			
			//note: Getting icon from imagelist must be in STA thread too, else fails with some file types.
			//tested: This part works without locking. Using another lock here makes slower.
			
			try {
				if (ilIndex == Api.SHIL_SMALL || ilIndex == Api.SHIL_LARGE || _GetShellImageList(ilIndex, out il)) {
					//print.it(il, Debug_.GetComObjRefCount(il));
					var hi = Api.ImageList_GetIcon(il, index, 0);
					if (hi != default) {
						if (size != realSize) {
							//print.it(size, realSize, index, file);
							hi = Api.CopyImage(hi, Api.IMAGE_ICON, size, size, Api.LR_COPYDELETEORG | Api.LR_COPYRETURNORG);
							//never mind if fails, it's too rare.
							//TODO3: test LR_COPYFROMRESOURCE.
						}
						return _New(hi);
					}
				}
			}
			catch (Exception e) { Debug_.Print(e); }
			//finally { if(il != default) Marshal.Release(il); }
			return null;
			
			static bool _GetShellImageList(int ilIndex, out IntPtr R) {
				lock ("vK6Z4XiSxkGSfC14/or5Mw") { //the API fails if called simultaneously by multiple threads
					if (0 == Api.SHGetImageList(ilIndex, Api.IID_IImageList, out R)) return true;
				}
				Debug.Assert(false);
				return false;
			}
		}
		
		//Gets shortcut (.lnk) icon.
		//Much faster than other shell API.
		//Also gets correct icon where iextracticon fails and/or shgetfileinfo gets blank document icon, don't know why.
		//Usually fails only when target does not exist. Then iextracticon also fails, and shgetfileinfo gets blank document icon.
		//If fails, returns null. No exceptions.
		static icon _GetLnkIcon(string file, int size) {
			try {
				using var x = shortcutFile.open(file);
				var s = x.GetIconLocation(out int ii); if (s != null) return _Load(s, size, ii);
				s = x.TargetPathRawMSI; if (s != null) return _OfFile(s, size, IconGetFlags.DontSearch);
				//print.it("need IDList", file);
				using (var pidl = x.TargetPidl) return _OfPidl(pidl, size);
			}
			catch { return null; }
		}
		
		/// <summary>
		/// Extracts icon directly from file.
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		/// <param name="file"><c>.ico</c>, <c>.exe</c>, <c>.dll</c> or other file that contains one or more icons. Also supports cursor files - <c>.cur</c>, <c>.ani</c>. Must be full path, without icon index. Supports environment variables (see <see cref="pathname.expand"/>).</param>
		/// <param name="size">Icon width and height. Default 16.</param>
		/// <param name="index">Icon index or negative icon resource id in the <c>.exe</c>/<c>.dll</c> file.</param>
		public static icon load(string file, int size = 16, int index = 0) {
			using var ds = new _DebugSpeed(file);
			return _Load(file, _NormalizeIconSizeArgument(size), index);
		}
		
		static unsafe icon _Load(string file, int size, int index) {
			//We use SHDefExtractIcon because of better quality resizing (although several times slower) which matches shell and imagelist resizing.
			//With .ico it matches LoadImage speed (without resizing). PrivateExtractIcons is slightly slower.
			
			if (file.NE()) return null;
			IntPtr hi = default;
			int hr = Api.SHDefExtractIcon(file, index, 0, &hi, null, size);
			return hr == 0 ? _New(hi) : null;
			
			//if(Api.PrivateExtractIcons(file, index, size, size, out R, default, 1, 0) != 1) return null;
			
			//SHOULDOO: test LoadIconWithScaleDown. If .ico or negative index. But maybe SHDefExtractIcon uses it, that is why better quality.
		}
		
		/// <summary>
		/// Gets a shell stock icon.
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		/// <param name="id">Shell stock icon id.</param>
		/// <param name="size">Icon width and height. Default 16.</param>
		public static unsafe icon stock(StockIcon id, int size = 16) {
			if (!GetStockIconLocation_(id, out var path, out int index)) return null;
			return load(path, size, index);
			//note: don't cache, because of the quota of handles a process can have. Maybe only exe and document icons; maybe also folder and open folder.
			
			//tested: always gets 32x32 icon: Api.LoadImage(default, 32516, Api.IMAGE_ICON, 16, 16, Api.LR_SHARED); //OIC_INFORMATION
		}
		
		internal static unsafe bool GetStockIconLocation_(StockIcon id, out string path, out int index) {
			var x = new Api.SHSTOCKICONINFO(); x.cbSize = Api.SizeOf(x);
			if (0 == Api.SHGetStockIconInfo(id, 0, ref x)) {
				path = new string(x.szPath);
				index = x.iIcon;
				return true;
			} else {
				path = null;
				index = 0;
				return false;
			}
		}
		
		/// <summary>
		/// Gets icon from unmanaged resources of this program.
		/// </summary>
		/// <returns><c>null</c> if not found.</returns>
		/// <param name="size">Icon width and height. Default 16.</param>
		/// <param name="resourceId">Native resource id. Default <msdn>IDI_APPLICATION</msdn> (C# compilers add app icon with this id).</param>
		/// <remarks>
		/// If role <b>miniProgram</b> (default), at first looks in main assembly (<c>.dll</c>); if not found there, looks in <c>.exe</c> file. Else only in <c>.exe</c> file.
		/// 
		/// The icon is cached and protected from destroying. Don't need to destroy it, and not error to do it.
		/// </remarks>
		public static icon ofThisApp(int size = 16, int resourceId = Api.IDI_APPLICATION) {
			var hm = GetAppIconModuleHandle_(resourceId);
			return FromModuleHandle_(hm, size, resourceId);
			
			//This is not 100% reliable because the icon id 32512 (IDI_APPLICATION) is undocumented.
			//I could not find a .NET method to get icon directly from native resources of assembly.
			//Could use the resource emumeration API...
			//info: MSDN lies that with LR_SHARED gets a cached icon regardless of size argument. Caches each size separately. Tested on Win 10, 7, XP.
			
			//TEST: LoadIconWithScaleDown.
		}
		
		internal static icon FromModuleHandle_(IntPtr hm, int size = 16, int resourceId = Api.IDI_APPLICATION) {
			if (hm == default) return null;
			size = _NormalizeIconSizeArgument(size);
			return _New(Api.LoadImage(hm, resourceId, Api.IMAGE_ICON, size, size, Api.LR_SHARED));
		}
		
		
		/// <summary>
		/// Gets icon of tray icon size from unmanaged resources of this program or system.
		/// </summary>
		/// <param name="resourceId">Native resource id. Default <msdn>IDI_APPLICATION</msdn> (C# compilers add app icon with this id).</param>
		/// <remarks>
		/// Calls API <msdn>LoadIconMetric</msdn>.
		/// 
		/// The icon can be in main assembly (if role <b>miniProgram</b>) or in the program file (<c>.exe</c>). If not found, loads standard icon, see API <b>LoadIconMetric</b>.
		/// </remarks>
		public static icon trayIcon(int resourceId = Api.IDI_APPLICATION/*, bool big = false*/) {
#if true
			IntPtr hi = default; int hr = 1;
			if (script.role == SRole.MiniProgram) hr = Api.LoadIconMetric(Api.GetModuleHandle(AssemblyUtil_.GetEntryAssembly().Location), resourceId, 0, out hi);
			if (hr != 0) hr = Api.LoadIconMetric(Api.GetModuleHandle(null), resourceId, 0, out hi);
			if (hr != 0) hr = Api.LoadIconMetric(default, resourceId, 0, out hi);
			return hr == 0 ? _New(hi) : null;
#else //10% slower
			var h = GetAppIconModuleHandle_();
			return 0 == Api.LoadIconMetric(h, resourceId, /*big ? 1 :*/ 0, out var hi) ? new(hi) : null;
#endif
			//can load big icon too, but not very useful.
		}
		
		/// <summary>
		/// Loads icon of tray icon size from <c>.ico</c> file.
		/// </summary>
		/// <returns><c>null</c> if not found.</returns>
		/// <remarks>
		/// Calls API <msdn>LoadIconMetric</msdn>.
		/// </remarks>
		public static unsafe icon trayIcon(string icoFile) {
			fixed (char* p = icoFile) return 0 == Api.LoadIconMetric(default, (nint)p, 0, out var hi) ? _New(hi) : null;
			//LoadIconMetric bug: does not load large icon from ico file.
		}
		
		/// <summary>
		/// Gets native module handle of exe or dll that contains specified icon. Returns default if no icon.
		/// If role <b>miniProgram</b>, at first looks in main assembly (<c>.dll</c>).
		/// </summary>
		internal static IntPtr GetAppIconModuleHandle_(int resourceId) {
			if (script.role == SRole.MiniProgram) {
				var h1 = Api.GetModuleHandle(AssemblyUtil_.GetEntryAssembly().Location);
				if (default != Api.FindResource(h1, resourceId, Api.RT_GROUP_ICON)) return h1;
			}
			var h2 = Api.GetModuleHandle(null);
			if (default != Api.FindResource(h2, resourceId, Api.RT_GROUP_ICON)) return h2;
			return default;
		}
		
		/// <summary>
		/// Gets icon that is displayed in window title bar and in taskbar button.
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		/// <param name="w">A top-level window of any process.</param>
		/// <param name="size">Icon width and height. Default 16.</param>
		public static icon ofWindow(wnd w, int size = 16) {
			//int size = Api.GetSystemMetrics(big ? Api.SM_CXICON : Api.SM_CXSMICON);
			
			//support Windows Store apps
			var appId = WndUtil.GetWindowsStoreAppId(w, prependShellAppsFolder: true);
			if (appId != null) {
				var v = of(appId, size, IconGetFlags.DontSearch);
				if (v != null) return v;
			}
			
			bool big = size >= 24; //TODO3: make high-DPI-aware. How?
			bool ok = w.SendTimeout(2000, out nint R, Api.WM_GETICON, big ? 1 : 0);
			if (R == 0 && ok) w.SendTimeout(2000, out R, Api.WM_GETICON, big ? 0 : 1);
			if (R == 0) R = WndUtil.GetClassLong(w, big ? GCL.HICON : GCL.HICONSM);
			if (R == 0) R = WndUtil.GetClassLong(w, big ? GCL.HICONSM : GCL.HICON);
			//tested this code with DPI 125%. Small icon of most windows match DPI (20), some 16, some 24.
			//tested: undocumented API InternalGetWindowIcon does not get icon of winstore app.
			
			//Copy, because will DestroyIcon, also it resizes if need.
			if (R != 0) R = Api.CopyImage(R, Api.IMAGE_ICON, size, size, 0);
			return _New(R);
		}
		
		/// <summary>
		/// Sends <msdn>WM_SETICON</msdn> message.
		/// </summary>
		/// <param name="w"></param>
		/// <param name="big"><b>ICON_BIG</b>.</param>
		public void SetWindowIcon(wnd w, bool big) {
			w.Send(Api.WM_SETICON, big ? 1 : 0, _handle);
		}
		
		/// <summary>
		/// Creates icon at run time.
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="drawCallback">Called to draw icon. If <c>null</c>, the icon will be completely transparent.</param>
		public static icon create(int width, int height, Action<Graphics> drawCallback = null) {
			IntPtr hi;
			if (drawCallback != null) {
				using var b = new Bitmap(width, height);
				using var g = Graphics.FromImage(b);
				g.Clear(Color.Transparent); //optional, new bitmaps are transparent, but it is undocumented, and eg .NET Bitmap.MakeTransparent does it
				drawCallback(g);
				hi = b.GetHicon();
			} else {
				int nb = Math2.AlignUp(width, 32) / 8 * height;
				var aAnd = new byte[nb]; for (int i = 0; i < nb; i++) aAnd[i] = 0xff;
				var aXor = new byte[nb];
				hi = Api.CreateIcon(default, width, height, 1, 1, aAnd, aXor);
				
				//speed: ~20 mcs. About 10 times faster than above. Faster than CopyImage etc.
			}
			return _New(hi);
		}
		
		/// <summary>
		/// Creates <see cref="System.Drawing.Icon"/> object that shares native icon handle with this object.
		/// </summary>
		/// <returns><c>null</c> if <b>Handle</b> is <c>default(IntPtr)</c>.</returns>
		public Icon ToGdipIcon() {
			if (_handle == default) return null;
			var R = Icon.FromHandle(_handle);
			s_cwt.Add(R, this);
			return R;
		}
		static readonly ConditionalWeakTable<Icon, icon> s_cwt = new();
		
		/// <summary>
		/// Converts native icon to GDI+ bitmap object.
		/// </summary>
		/// <returns><c>null</c> if <b>Handle</b> is <c>default(IntPtr)</c> or if fails to convert.</returns>
		/// <param name="destroyIcon">
		/// If <c>true</c> (default), destroys the native icon object; also clears this variable and don't need to dispose it.
		/// If <c>false</c>, later will need to dispose this variable.
		/// </param>
		public Bitmap ToGdipBitmap(bool destroyIcon = true) {
			if (_handle != default) {
				using var ic = Icon.FromHandle(_handle);
				try { return ic.ToBitmap(); }
				catch (Exception e) { print.warning("ToGdipBitmap() failed. " + e.ToString(), -1); }
				finally { if (destroyIcon) Dispose(); }
			}
			return null;
			//note: don't use Bitmap.FromHicon. It just calls GdipCreateBitmapFromHICON which does not support alpha etc.
			//FUTURE: look for a faster way.
		}
		
		/// <summary>
		/// Converts native icon to WPF image object.
		/// </summary>
		/// <returns><c>null</c> if <b>Handle</b> is <c>default(IntPtr)</c> or if fails to convert.</returns>
		/// <param name="destroyIcon">
		/// If <c>true</c> (default), destroys the native icon object; also clears this variable and don't need to dispose it.
		/// If <c>false</c>, later will need to dispose this variable.
		/// </param>
		/// <remarks>
		/// The image is not suitable for WPF window icon. Instead use <see cref="SetWindowIcon"/> or WPF image loading functions.
		/// </remarks>
		public System.Windows.Media.Imaging.BitmapSource ToWpfImage(bool destroyIcon = true) {
			if (_handle != default) {
				try { return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(_handle, default, default); }
				catch (Exception e) { print.warning("ToWpfImage() failed. " + e.ToString(), -1); }
				finally { if (destroyIcon) Dispose(); }
			}
			return null;
			
			//why not suitable for WPF window icon:
			//1. Shadows in alpha area. As a workaround could get icon bits and call BitmapFrame.Create(), but
			//2. For window need 2 icons - small and big.
			//See script "HICON to ImageSource".
		}
		
		/// <summary>
		/// Gets icon size.
		/// </summary>
		/// <returns><c>default(SIZE)</c> if failed.</returns>
		public unsafe SIZE Size {
			get {
				if (_handle != default) {
					using Api.ICONINFO ii = new(_handle);
					Api.BITMAP b = default;
					bool hasColors = ii.hbmColor != default;
					Api.GetObject(hasColors ? ii.hbmColor : ii.hbmMask, sizeof(Api.BITMAP), &b);
					return new(b.bmWidth, hasColors ? b.bmHeight : b.bmHeight / 2);
				}
				return default;
			}
		}
		
		/// <summary>
		/// Parses icon location string.
		/// </summary>
		/// <returns><c>true</c> if it includes icon index or resource id.</returns>
		/// <param name="s">
		/// Icon location. Can be <c>"path,index"</c> or <c>"path,-id"</c> or just path.
		/// Supports path enclosed in <c>""</c> like <c>"\"path\",index"</c>, and spaces between comma and index like <c>"path, index"</c>.
		/// </param>
		/// <param name="path">Receives path without index and without <c>""</c>. Can be the same variable as <i>s</i>.</param>
		/// <param name="index">Receives index/id or 0.</param>
		public static bool parsePathIndex(string s, out string path, out int index) {
			path = s; index = 0;
			if (s.NE()) return false;
			if (s[0] == '"') path = s = s.Replace("\"", ""); //can be eg "path",index
			if (s.Length < 3) return false;
			if (!s[^1].IsAsciiDigit()) return false;
			int i = s.LastIndexOf(','); if (i < 1) return false;
			index = s.ToInt(i + 1, out int e); if (e != s.Length) return false;
			path = s[..i];
			return true;
			
			//note: API PathParseIconLocation has bugs. Eg splits "path,5moreText". Or from "path, 5" removes ", 5" and returns 0.
		}
		
		static int _NormalizeIconSizeArgument(int size) {
			if (size == 0) return 16;
			return (uint)size <= 256 ? size : throw new ArgumentOutOfRangeException("size", "Must be 0 - 256");
		}
		
		//rejected. Not per-monitor-DPI-aware, etc. Better use 16 etc and then auto-scale.
		///// <summary>
		///// Gets size of small icons displayed in UI.
		///// Depends on DPI; 16 when DPI 100%.
		///// </summary>
		//public static int sizeSmall => Dpi.OfThisProcess / 6; //eg 96/6=16
		
		///// <summary>
		///// Gets size of large icons displayed in UI.
		///// Depends on DPI; 32 when DPI 100%.
		///// </summary>
		//public static int sizeLarge => Dpi.OfThisProcess / 3;
		
		//static int _SizeExtraLarge => Dpi.OfThisProcess / 2;
		
		//tested: shell imagelist icon sizes match these.
		//note: don't use GetSystemMetrics(SM_CXSMICON/SM_CXICON). They are for other purposes, eg window title bar, tray icon. On Win7 they can be different because can be changed in Control Panel. Used by SystemInformation.SmallIconSize etc.
		
		/// <summary>
		/// If not 0, "get icon" functions of this class will print (in editor's output) their execution time in milliseconds when it &gt;= this value.
		/// </summary>
		/// <remarks>
		/// Icons are mostly used in toolbars and menus. Getting icons of some files can be slow. For example if antivirus program scans the file. Toolbars and menus that use slow icons may start with a noticeable delay. Use this property to find too slow icons. Then you can replace them with fast icons, for example <c>.ico</c> files.
		/// </remarks>
		public static int debugSpeed { get; set; }
		
		struct _DebugSpeed : IDisposable {
			long _time;
			object _file; //string or Pidl
			
			public _DebugSpeed(object file) {
				if (debugSpeed > 0) {
					_file = file;
					_time = perf.ms;
				} else {
					_time = 0;
					_file = null;
				}
				//TODO3: implement global icon cache here. File-based.
			}
			
			public void Dispose() {
				if (_time != 0) {
					long t = perf.ms - _time;
					if (t >= debugSpeed) {
						//if (_file is Pidl p) _file = p.ToShellString(SIGDN.NORMALDISPLAY);
						print.it($"icon.debugSpeed: {t} ms, {_file}");
					}
				}
			}
		}
		
		/// <summary>
		/// Gets icon path from code that contains string like <c>@"c:\windows\system32\notepad.exe"</c> or <c>@"%folders.System%\notepad.exe"</c> or URL/shell.
		/// Also supports code patterns like <c>folders.System + "notepad.exe"</c> or <c>folders.shell.RecycleBin</c>.
		/// </summary>
		/// <returns><c>null</c> if no such string/pattern.</returns>
		/// <param name="mi"></param>
		/// <param name="cs">The string is <c>.cs</c> filename or relative path, but not full path.</param>
		internal static string ExtractIconPathFromCode_(MethodInfo mi, out bool cs) {
			//support code pattern like 'folders.System + "notepad.exe"'.
			//	Opcodes: call(folders.System), ldstr("notepad.exe"), FolderPath.op_Addition.
			//also code pattern like 'folders.System' or 'folders.shell.RecycleBin'.
			//	Opcodes: call(folders.System), FolderPath.op_Implicit(FolderPath to string).
			//also code pattern like 'run.itSafe("notepad.exe")'.
			//print.it(mi.Name);
			
			cs = false;
			var il = mi.GetMethodBody().GetILAsByteArray();
			if (il.Length > 100) return null;
			
			int i = 0, patternStart = -1; MethodInfo f1 = null; string filename = null, filename2 = null;
			try {
				var reader = new ILReader(mi, il);
				foreach (var instruction in reader.Instructions) {
					if (++i > 100) break;
					var op = instruction.Op;
					//print.it(op);
					if (op == OpCodes.Nop) {
						i--;
					} else if (op == OpCodes.Ldstr) {
						var s = instruction.Data as string;
						//print.it(s);
						//print.it(i, patternStart);
						if (i == patternStart + 1) filename = s;
						else {
							if (pathname.isFullPathExpand(ref s)) return s; //eg run.it(@"%folders.System%\notepad.exe");
							if (pathname.IsShellPathOrUrl_(s)) return s;
							filename = null; patternStart = -1;
							if (i == 1) filename2 = s;
						}
					} else if (op == OpCodes.Call && instruction.Data is MethodInfo f && f.IsStatic) {
						//print.it(f, f.DeclaringType, f.Name, f.MemberType, f.ReturnType, f.GetParameters().Length);
						var dt = f.DeclaringType;
						if (dt == typeof(folders) || dt == typeof(folders.shell)) {
							if (f.ReturnType == typeof(FolderPath) && f.GetParameters().Length == 0) {
								//print.it(1);
								f1 = f;
								patternStart = i;
							}
						} else if (dt == typeof(FolderPath)) {
							if (i == patternStart + 2 && f.Name == "op_Addition") {
								//print.it(2);
								var fp = (FolderPath)f1.Invoke(null, null);
								if (fp.Path == null) return null;
								return fp + filename;
							} else if (i == patternStart + 1 && f.Name == "op_Implicit" && f.ReturnType == typeof(string)) {
								//print.it(3);
								return (FolderPath)f1.Invoke(null, null);
							}
							//} else if (dt == typeof(script)) {
							//	print.it(filename);
						}
					}
				}
				if (filename2 != null) {
					if (filename2.Ends(".exe", true)) return filesystem.searchPath(filename2);
					if (cs = filename2.Ends(".cs", true) && !pathname.isFullPath(filename2, orEnvVar: true)) return filename2;
				}
			}
			catch (Exception ex) { Debug_.Print(ex); }
			return null;
		}
		
		/// <summary>
		/// Gets image of a Windows Store App.
		/// </summary>
		/// <returns><b>Bitmap</b> object, or <c>null</c> if failed. Its size may be != <i>size</i>; let the caller scale it when drawing.</returns>
		/// <param name="shellString">String like <c>@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"</c>.</param>
		/// <param name="size">Desired width and height.</param>
		public static Bitmap winStoreAppImage(string shellString, int size = 16) {
			using var idl = Pidl.FromString(shellString); if (idl == null) return null; //the slowest part, > 90%
			return winStoreAppImage(idl, size);
		}
		
		/// <summary>
		/// Gets image of a Windows Store App. This overload accepts a <b>Pidl</b> instead of a shell string.
		/// </summary>
		/// <returns><b>Bitmap</b> object, or <c>null</c> if failed. Its size may be != <i>size</i>; let the caller scale it when drawing.</returns>
		public static Bitmap winStoreAppImage(Pidl pidl, int size = 16) {
			var path = _GetWinStoreAppImagePath(pidl, size); if (path == null) return null;
			//var r = Image.FromFile(path) as Bitmap; //no, locks file
			using var stream = File.OpenRead(path);
			var r = Image.FromStream(stream) as Bitmap;
			r?.SetResolution(96, 96);
			return r;
		}
		
		static string _GetWinStoreAppImagePath(Pidl pidl, int size = 16) {
			if (0 != Api.SHBindToParent(pidl.UnsafePtr, typeof(Api.IShellFolder).GUID, out var isf, out var idItem)) return null;
			if (!isf.GetUIObjectOf(idItem, out Api.IExtractIcon extract)) return null;
			var sb = new StringBuilder(1000);
			if (0 != extract.GetIconLocation(0, sb, sb.Capacity, out int index, out uint rflags)) return null;
			var loc = sb.ToString();
			//print.it(loc);
			if (loc.Ends(".png")) return loc; //Win 8.0, 8.1
			Debug_.PrintIf(!loc.Ends(".png-100"), loc);
			int ipng = loc.LastIndexOf(".png", StringComparison.OrdinalIgnoreCase); if (ipng < 0) return null;
			loc = loc[..ipng];
			if (!_GetPngPathFromIconLoc(out string path)) return null;
			return path;
			
			bool _GetPngPathFromIconLoc(out string path) {
				path = null;
				var dir = pathname.getDirectory(loc);
				if (!filesystem.exists(dir).Directory) return false;
				var a1 = Directory.GetFiles(dir, pathname.getName(loc) + "*.png");
				int nTargetsize = 0, nScale = 0;
				for (int i = 0; i < a1.Length; i++) {
					var v = a1[i];
					if (v.Find("contrast-", loc.Length, true) >= 0) a1[i] = null;
					else if (v.Find("targetsize-", loc.Length, true) >= 0) nTargetsize++;
					else if (v.Find("scale-", loc.Length, true) >= 0) nScale++;
				}
				if (nTargetsize == 0 && nScale == 0) {
					path = a1.FirstOrDefault(o => o.Eqi(loc));
					loc += ".png";
					return path != null;
				}
				var a = new (string path, string q, int size)[nTargetsize > 0 ? nTargetsize : nScale];
				int j = 0;
				foreach (var v in a1) {
					if (v == null) continue;
					int i = v.Find(nTargetsize > 0 ? "targetsize-" : "scale-", loc.Length, true); if (i < 0) continue;
					int z = v.ToInt(i + (nTargetsize > 0 ? 11 : 6));
					if (nTargetsize == 0) z = Math2.MulDiv(z, 16, 100); //assume eg scale-100 == targetsize-16
					a[j++] = (v, v[loc.Length..^4], z);
				}
				//foreach (var k in a) print.it("\t" + k.q, k.size);
				int bestSize = int.MaxValue, maxSmallerSize = 0;
				foreach (var v in a) if (v.size == size) { bestSize = size; break; } else if (v.size > size) bestSize = Math.Min(bestSize, v.size); else maxSmallerSize = Math.Max(maxSmallerSize, v.size);
				if (bestSize == int.MaxValue) bestSize = maxSmallerSize;
				//print.it(bestSize);
				foreach (var v in a) if (v.size == bestSize && v.q.Find("altform-lightunplated", true) >= 0) { path = v.path; return true; }
				//foreach (var v in a) if(v.size==bestSize && v.q.Find("altform-unplated", true)>=0 && v.q.Find("theme-light", true)>=0) { path=v.path; return true; }
				//foreach (var v in a) if(v.size==bestSize && v.q.Find("theme-light", true)>=0) { path=v.path; return true; }
				foreach (var v in a) if (v.size == bestSize && v.q.Find("altform-unplated", true) >= 0) { path = v.path; return true; }
				foreach (var v in a) if (v.size == bestSize) { path = v.path; return true; }
				return false;
			}
		}
	}
}

namespace Au.Types {
	/// <summary>
	/// Flags for <see cref="icon.of"/> and similar functions.
	/// </summary>
	[Flags]
	public enum IconGetFlags {
		/// <summary>
		/// The <i>file</i> argument is literal full path. Don't parse <c>"path,index"</c>, don't support <c>".ext"</c> (file type icon), don't make fully-qualified, etc.
		/// </summary>
		LiteralPath = 1,
		
		/// <summary>
		/// Don't call <see cref="filesystem.searchPath"/>.
		/// </summary>
		DontSearch = 2,
		
#if false
		/// Use shell API for all file types, including exe and ico.
		Shell=8, //rejected because SHGetFileInfo does not get exe icon with shield overlay

		/// <summary>
		/// If file does not exist or fails to get its icon, get common icon for that file type, or default document icon if cannot get common icon.
		/// </summary>
		DefaultIfFails = 16, //rejected. Now for exe/ico/etc is like with shell API: if file exists, gets default icon (exe or document), else returns <c>default(IntPtr)</c>.
#endif
	}
	
#pragma warning disable 1591 //missing XML documentation
	/// <summary>See <see cref="icon.stock"/>, <msdn>SHSTOCKICONID</msdn>.</summary>
	public enum StockIcon {
		DOCNOASSOC,
		DOCASSOC,
		APPLICATION,
		FOLDER,
		FOLDEROPEN,
		DRIVE525,
		DRIVE35,
		DRIVEREMOVE,
		DRIVEFIXED,
		DRIVENET,
		DRIVENETDISABLED,
		DRIVECD,
		DRIVERAM,
		WORLD,
		SERVER = 15,
		PRINTER,
		MYNETWORK,
		FIND = 22,
		HELP,
		SHARE = 28,
		LINK,
		SLOWFILE,
		RECYCLER,
		RECYCLERFULL,
		MEDIACDAUDIO = 40,
		LOCK = 47,
		AUTOLIST = 49,
		PRINTERNET,
		SERVERSHARE,
		PRINTERFAX,
		PRINTERFAXNET,
		PRINTERFILE,
		STACK,
		MEDIASVCD,
		STUFFEDFOLDER,
		DRIVEUNKNOWN,
		DRIVEDVD,
		MEDIADVD,
		MEDIADVDRAM,
		MEDIADVDRW,
		MEDIADVDR,
		MEDIADVDROM,
		MEDIACDAUDIOPLUS,
		MEDIACDRW,
		MEDIACDR,
		MEDIACDBURN,
		MEDIABLANKCD,
		MEDIACDROM,
		AUDIOFILES,
		IMAGEFILES,
		VIDEOFILES,
		MIXEDFILES,
		FOLDERBACK,
		FOLDERFRONT,
		SHIELD,
		WARNING,
		INFO,
		ERROR,
		KEY,
		SOFTWARE,
		RENAME,
		DELETE,
		MEDIAAUDIODVD,
		MEDIAMOVIEDVD,
		MEDIAENHANCEDCD,
		MEDIAENHANCEDDVD,
		MEDIAHDDVD,
		MEDIABLURAY,
		MEDIAVCD,
		MEDIADVDPLUSR,
		MEDIADVDPLUSRW,
		DESKTOPPC,
		MOBILEPC,
		USERS,
		MEDIASMARTMEDIA,
		MEDIACOMPACTFLASH,
		DEVICECELLPHONE,
		DEVICECAMERA,
		DEVICEVIDEOCAMERA,
		DEVICEAUDIOPLAYER,
		NETWORKCONNECT,
		INTERNET,
		ZIPFILE,
		SETTINGS,
		DRIVEHDDVD = 132,
		DRIVEBD,
		MEDIAHDDVDROM,
		MEDIAHDDVDR,
		MEDIAHDDVDRAM,
		MEDIABDROM,
		MEDIABDR,
		MEDIABDRE,
		CLUSTEREDDRIVE,
		MAX_ICONS = 181
	}
#pragma warning restore 1591
}
