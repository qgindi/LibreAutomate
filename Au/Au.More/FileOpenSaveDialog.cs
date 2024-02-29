namespace Au.More {
	/// <summary>
	/// Shows standard "Open", "Save As" or "Select Folder" dialog to select a file or folder.
	/// </summary>
	/// <remarks>
	/// This class exists because the similar .NET classes have these problems:
	/// - May disable the <see cref="AppDomain.UnhandledException"/> event.
	/// - As owner window they support only <b>Window</b> or <b>Form</b>. This class also supports window handles.
	/// - They support only filesystem items.
	/// - There is no option to not add the selected file to recent files that are displayed in the context menu of the taskbar button etc.
	/// - The WPF class does not have a <b>ClientGuid</b> property.
	/// 
	/// There are 2 main dialog types - open (<see cref="ShowOpen"/>) and save (<see cref="ShowSave"/>). All other functions of this class (properties, etc) are common to opening and saving.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var d = new FileOpenSaveDialog { FileTypes = "Text files|*.txt|All files|*.*" };
	/// if(d.ShowOpen(out string s)) print.it(s);
	/// ]]></code>
	/// </example>
	public class FileOpenSaveDialog {
		readonly string _clientGuid;
		readonly bool _clearClientData;

		/// <param name="clientGuid">A GUID used to save the recently used folder path and other data of this dialog. Optional. See <msdn>IFileDialog.SetClientGuid</msdn>.</param>
		/// <param name="clearClientData">Clear the recently used folder path and other data of this dialog.</param>
		public FileOpenSaveDialog(string clientGuid = null, bool clearClientData = false) {
			_clientGuid = clientGuid;
			_clearClientData = clearClientData;
		}

		/// <summary>
		/// Options common to all dialog types. Rarely used.
		/// </summary>
		public FOSFlags CommonFlags { get; set; }

		/// <summary>
		/// List of file types, like with <see cref="Microsoft.Win32.FileDialog.Filter"/>.
		/// Example: <c>"Text files|*.txt|Office files|*.doc;*.xls|All files|*.*"</c>
		/// </summary>
		public Strings FileTypes { get; set; }

		/// <summary>
		/// 1-based index of the selected file type (see <see cref="FileTypes"/>).
		/// </summary>
		public int FileTypeIndex { get; set; }

		/// <summary>
		/// Default extension to add to file names.
		/// Must be like <c>"txt"</c>, not like <c>".txt"</c>.
		/// If <c>null</c> (default), gets from <see cref="FileTypes"/>; set <c>""</c> to prevent it.
		/// </summary>
		public string DefaultExt { get; set; }

		/// <summary>
		/// Initial folder for the first time. Later is used the recently used folder instead.
		/// </summary>
		public string InitFolderFirstTime { get; set; }

		/// <summary>
		/// Initial folder to use now.
		/// In most cases it's recommended to use <see cref="InitFolderFirstTime"/> instead.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// d.InitFolderNow = @"C:\Test";
		/// d.InitFolderNow = /* This PC */ ":: 14001f50e04fd020ea3a6910a2d808002b30309d";
		/// ]]></code>
		/// </example>
		public string InitFolderNow { get; set; }

		/// <summary>
		/// Sets the initial file name text in the edit box.
		/// </summary>
		public string FileNameText { private get; set; }

		/// <summary>
		/// Sets the file name edit box label.
		/// </summary>
		public string FileNameLabel { private get; set; }

		/// <summary>
		/// Sets the OK button label.
		/// </summary>
		public string OkButtonLabel { private get; set; }

		/// <summary>
		/// Sets the dialog window name.
		/// </summary>
		public string Title { private get; set; }

		object _Show(bool save, _Api.FOS f, AnyWnd owner, string saveFile = null) {
			var w = owner.Hwnd;
			if (w.Is0) {
				w = wnd.active;
				if (!w.IsOfThisThread) w = default;
			}

			var d = (_Api.IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(save ? _Api.CLSID_FileSaveDialog : _Api.CLSID_FileOpenDialog));

			if (_clientGuid != null) d.SetClientGuid(new(_clientGuid));
			if (_clearClientData) d.ClearClientData();

			f |= (_Api.FOS)CommonFlags ^ (_Api.FOS.FOS_DONTADDTORECENT | _Api.FOS.FOS_PATHMUSTEXIST); //print.it(f);
			d.SetOptions(f);

			var defExt = DefaultExt;
			var ft = FileTypes.Value == null ? null : FileTypes.ToArray();
			if (ft != null) {
				int n = ft.Length / 2, i = FileTypeIndex;
				d.SetFileTypes(n, ft);
				if (i > 0) d.SetFileTypeIndex(i--);
				if (defExt == null && (uint)i < n) ft[i * 2 + 1].RxMatch(@"^\*\.([^*?;]+)", 1, out defExt);
			}
			if (!defExt.NE()) d.SetDefaultExtension(defExt);
			if (InitFolderFirstTime != null && _ShellItemFromString(InitFolderFirstTime, out var si1)) d.SetDefaultFolder(si1);
			if (InitFolderNow != null && _ShellItemFromString(InitFolderNow, out var si2)) d.SetFolder(si2);
			if (FileNameText != null) d.SetFileName(FileNameText);
			if (FileNameLabel != null) d.SetFileNameLabel(FileNameLabel);
			if (OkButtonLabel != null) d.SetOkButtonLabel(OkButtonLabel);
			if (Title != null) d.SetTitle(Title);

			if (saveFile != null && d is _Api.IFileSaveDialog sd && _ShellItemFromString(saveFile, out var si3)) sd.SetSaveAsItem(si3);

			//if (0!=d.Show(w)) return null;

			//API bug: after d.Show stops working AppDomain.UnhandledException event.
			//	Same with .NET wrappers; same when the native API called in C++; same with API GetOpenFileName.
			//	The API removes the .NET's unhandled exception filter and sets null filter. Need to restore it.
			//	AppModuleInit_ auto-restores, but in some apps it isn't called.
			//	Fixed in Win11.
			var uef = Api.SetUnhandledExceptionFilter(0);
			Api.SetUnhandledExceptionFilter(uef);
			try { if (0 != d.Show(w)) return null; }
			finally { Api.SetUnhandledExceptionFilter(uef); }

			if (ft != null) FileTypeIndex = d.GetFileTypeIndex();
			//FileNameText=d.GetFileName(); //fails here. And not useful.

			if (!f.Has(_Api.FOS.FOS_ALLOWMULTISELECT)) return _ShellItemToString(d.GetResult());
			var r = ((_Api.IFileOpenDialog)d).GetResults();
			var a = new string[r.GetCount()];
			for (int i = 0; i < a.Length; i++) a[i] = _ShellItemToString(r.GetItemAt(i));
			return a;
		}

		object _ShowOpen(AnyWnd owner = default, bool multiSelect = false, bool selectFolder = false, bool onlyFilesystem = true, bool fileMustExist = true, bool previewPane = false) {
			//default FOS_NOCHANGEDIR, FOS_PATHMUSTEXIST, FOS_FILEMUSTEXIST
			var f = _Api.FOS.FOS_NOCHANGEDIR;
			if (multiSelect) f |= _Api.FOS.FOS_ALLOWMULTISELECT;
			if (selectFolder) f |= _Api.FOS.FOS_PICKFOLDERS;
			if (onlyFilesystem) f |= _Api.FOS.FOS_FORCEFILESYSTEM; else f |= _Api.FOS.FOS_ALLNONSTORAGEITEMS;
			if (fileMustExist) f |= _Api.FOS.FOS_FILEMUSTEXIST;
			if (previewPane) f |= _Api.FOS.FOS_FORCEPREVIEWPANEON;

			return _Show(false, f, owner);
		}

		/// <summary>
		/// Shows "Open" or "Select Folder" dialog that allows to select single item.
		/// </summary>
		/// <param name="result">Full path of the selected file.</param>
		/// <param name="owner">Owner window. Optional.</param>
		/// <param name="selectFolder">Select folders, not files.</param>
		/// <param name="onlyFilesystem">The dialog allows to select only file system items (files, folders), not other shell items or URLs. Default <c>true</c>. If <c>false</c>, other shell items are returned like <c>":: ITEMIDLIST"</c>; see <see cref="Pidl"/>.</param>
		/// <param name="fileMustExist">The dialog can return only existing items. Default <c>true</c>.</param>
		/// <param name="previewPane">Display the preview pane.</param>
		/// <returns><c>true</c> on OK, <c>false</c> on Cancel or error.</returns>
		public bool ShowOpen(out string result, AnyWnd owner = default, bool selectFolder = false, bool onlyFilesystem = true, bool fileMustExist = true, bool previewPane = false) {
			result = _ShowOpen(owner, false, selectFolder, onlyFilesystem, fileMustExist, previewPane) as string;
			return result != null;
		}

		/// <summary>
		/// Shows "Open" or "Select Folder" dialog that allows to select multiple items.
		/// </summary>
		/// <param name="result">Full paths of the selected files.</param>
		/// <inheritdoc cref="ShowOpen(out string, AnyWnd, bool, bool, bool, bool)"/>
		public bool ShowOpen(out string[] result, AnyWnd owner = default, bool selectFolder = false, bool onlyFilesystem = true, bool fileMustExist = true, bool previewPane = false) {
			result = _ShowOpen(owner, true, selectFolder, onlyFilesystem, fileMustExist, previewPane) as string[];
			return result != null;
		}

		/// <summary>
		/// Shows "Save As" dialog.
		/// </summary>
		/// <param name="result">Full path of the selected file.</param>
		/// <param name="owner">Owner window. Optional.</param>
		/// <param name="overwritePrompt">If the selected file already exists, show a message box. Default <c>true</c>.</param>
		/// <param name="initFile">The initially selected file. Its name is displayed in the file name edit box, and the containing folder is opened. This would generally be used when the application is saving a file that already exists. For new files use <see cref="FileNameText"/>.</param>
		/// <returns><c>true</c> on OK, <c>false</c> on Cancel or error.</returns>
		public bool ShowSave(out string result, AnyWnd owner = default, bool overwritePrompt = true, string initFile = null) {
			//default FOS_OVERWRITEPROMPT, FOS_NOCHANGEDIR, FOS_PATHMUSTEXIST, FOS_NOREADONLYRETURN
			var f = _Api.FOS.FOS_NOCHANGEDIR;
			if (overwritePrompt) f |= _Api.FOS.FOS_OVERWRITEPROMPT;
			/*if(!overwriteReadonly)*/
			f |= _Api.FOS.FOS_NOREADONLYRETURN; //always works like with this flag

			//if(createPrompt) f|=_Api.FOS.FOS_CREATEPROMPT; //does not work. The .NET wrapper shows messagebox explicitly.
			//if(!testCreate) f|=_Api.FOS.FOS_NOTESTFILECREATE; //not important, not tested
			//if(strictFileTypes) f|=_Api.FOS.FOS_STRICTFILETYPES; //does not work

			result = _Show(true, f, owner, initFile) as string;
			return result != null;
		}

		static string _ShellItemToString(_Api.IShellItem r) {
			//if(!f.Has(_Api.FOS.FOS_FORCEFILESYSTEM)) {
			//	var k=r.GetAttributes(0xffffffff);
			//	if(0==(k&_Api.SFGAO_FILESYSTEM)) {
			//		print.it(k);
			//	}
			//}

			var s = r.GetDisplayName(SIGDN.FILESYSPATH | SIGDN.URL); //info: for a non-FS item, even with SIGDN.FILESYSPATH gets string like "::{GUID}"
			return Pidl.ClsidToItemidlist_(s);
		}

		static bool _ShellItemFromString(string path, out _Api.IShellItem si) {
			if (path.Starts(":: ")) {
				var p = Pidl.FromString(path); if (p == null) { si = null; return false; }
				return 0 == _Api.SHCreateItemFromIDList(p.UnsafePtr, typeof(_Api.IShellItem).GUID, out si);
			} else {
				return 0 == _Api.SHCreateItemFromParsingName(path, default, typeof(_Api.IShellItem).GUID, out si);
			}
		}

		unsafe class _Api {
			internal static Guid CLSID_FileOpenDialog = new(0xDC1C5A9C, 0xE88A, 0x4DDE, 0xA5, 0xA1, 0x60, 0xF8, 0x2A, 0x20, 0xAE, 0xF7);

			internal static Guid CLSID_FileSaveDialog = new(0xC0B4E2F3, 0xBA21, 0x4773, 0x8D, 0xBA, 0x33, 0x5E, 0xC9, 0x46, 0xEB, 0x8B);

			[ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
			internal interface IFileOpenDialog : IFileDialog {
				//IModalWindow
				[PreserveSig] new int Show(wnd hwndOwner);
				//IFileDialog
				new void SetFileTypes(int cFileTypes, [MarshalAs(UnmanagedType.LPArray)][In] string[] rgFilterSpec);
				new void SetFileTypeIndex(int iFileType);
				new int GetFileTypeIndex();
				[PreserveSig] new int Advise(/*IFileDialogEvents pfde, out uint pdwCookie*/);
				[PreserveSig] new int Unadvise(uint dwCookie);
				new void SetOptions(_Api.FOS fos);
				new _Api.FOS GetOptions();
				new void SetDefaultFolder(IShellItem psi);
				new void SetFolder(IShellItem psi);
				new IShellItem GetFolder();
				new IShellItem GetCurrentSelection();
				new void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
				[return: MarshalAs(UnmanagedType.LPWStr)] new string GetFileName();
				new void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
				new void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
				new void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
				new IShellItem GetResult();
				[PreserveSig] new int AddPlace(/*IShellItem psi, FDAP fdap*/);
				new void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
				[PreserveSig] new int Close(int hr);
				new void SetClientGuid(in Guid guid);
				new void ClearClientData();
				[PreserveSig] new int SetFilter(/*IShellItemFilter pFilter*/);
				//IFileOpenDialog
				IShellItemArray GetResults();
				IShellItemArray GetSelectedItems();
			}

			[ComImport, Guid("84bccd23-5fde-4cdb-aea4-af64b83d78ab"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
			internal interface IFileSaveDialog : IFileDialog {
				//IModalWindow
				[PreserveSig] new int Show(wnd hwndOwner);
				//IFileDialog
				new void SetFileTypes(int cFileTypes, [MarshalAs(UnmanagedType.LPArray)][In] string[] rgFilterSpec);
				new void SetFileTypeIndex(int iFileType);
				new int GetFileTypeIndex();
				[PreserveSig] new int Advise(/*IFileDialogEvents pfde, out uint pdwCookie*/);
				[PreserveSig] new int Unadvise(uint dwCookie);
				new void SetOptions(_Api.FOS fos);
				new _Api.FOS GetOptions();
				new void SetDefaultFolder(IShellItem psi);
				new void SetFolder(IShellItem psi);
				new IShellItem GetFolder();
				new IShellItem GetCurrentSelection();
				new void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
				[return: MarshalAs(UnmanagedType.LPWStr)] new string GetFileName();
				new void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
				new void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
				new void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
				new IShellItem GetResult();
				[PreserveSig] new int AddPlace(/*IShellItem psi, FDAP fdap*/);
				new void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
				[PreserveSig] new int Close(int hr);
				new void SetClientGuid(in Guid guid);
				new void ClearClientData();
				[PreserveSig] new int SetFilter(/*IShellItemFilter pFilter*/);
				//IFileSaveDialog
				void SetSaveAsItem(IShellItem psi);
				[PreserveSig] int SetProperties(/*IPropertyStore pStore*/);
				[PreserveSig] int SetCollectedProperties(/*IPropertyDescriptionList pList, [MarshalAs(UnmanagedType.Bool)] bool fAppendDefault*/);
				[PreserveSig] int GetProperties(/*out IPropertyStore ppStore*/);
				[PreserveSig] int ApplyProperties(/*IShellItem psi, IPropertyStore pStore, wnd hwnd, IFileOperationProgressSink pSink*/);
			}

			[ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
			internal interface IFileDialog {
				//IModalWindow
				[PreserveSig] int Show(wnd hwndOwner);
				//IFileDialog
				void SetFileTypes(int cFileTypes, [MarshalAs(UnmanagedType.LPArray)][In] string[] rgFilterSpec);
				void SetFileTypeIndex(int iFileType);
				int GetFileTypeIndex();
				[PreserveSig] int Advise(/*IFileDialogEvents pfde, out uint pdwCookie*/);
				[PreserveSig] int Unadvise(uint dwCookie);
				void SetOptions(_Api.FOS fos);
				_Api.FOS GetOptions();
				void SetDefaultFolder(IShellItem psi);
				void SetFolder(IShellItem psi);
				IShellItem GetFolder();
				IShellItem GetCurrentSelection();
				void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
				[return: MarshalAs(UnmanagedType.LPWStr)] string GetFileName();
				void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
				void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
				void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
				IShellItem GetResult();
				[PreserveSig] int AddPlace(/*IShellItem psi, FDAP fdap*/);
				void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
				[PreserveSig] int Close(int hr);
				void SetClientGuid(in Guid guid);
				void ClearClientData();
				[PreserveSig] int SetFilter(/*IShellItemFilter pFilter*/);
			}

			[ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
			internal interface IShellItem {
				[PreserveSig] int BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, void** ppv);
				[PreserveSig] int GetParent(out IShellItem ppsi);
				[return: MarshalAs(UnmanagedType.LPWStr)] string GetDisplayName(SIGDN sigdnName);
				uint GetAttributes(uint sfgaoMask);
				[PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
			}

			[ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
			internal interface IShellItemArray {
				[PreserveSig] int BindToHandler(/*IntPtr pbc, in Guid bhid, in Guid riid, void** ppvOut*/);
				[PreserveSig] int GetPropertyStore(/*GETPROPERTYSTOREFLAGS flags, in Guid riid, void** ppv*/);
				[PreserveSig] int GetPropertyDescriptionList(/*in PROPERTYKEY keyType, in Guid riid, void** ppv*/);
				[PreserveSig] int GetAttributes(/*SIATTRIBFLAGS AttribFlags, uint sfgaoMask, out uint psfgaoAttribs*/);
				int GetCount();
				IShellItem GetItemAt(int dwIndex);
				[PreserveSig] int EnumItems(/*out IEnumShellItems ppenumShellItems*/);
			}

			[Flags]
			internal enum FOS : uint {
				FOS_OVERWRITEPROMPT = 0x2,
				FOS_STRICTFILETYPES = 0x4,
				FOS_NOCHANGEDIR = 0x8,
				FOS_PICKFOLDERS = 0x20,
				FOS_FORCEFILESYSTEM = 0x40,
				FOS_ALLNONSTORAGEITEMS = 0x80,
				FOS_NOVALIDATE = 0x100,
				FOS_ALLOWMULTISELECT = 0x200,
				FOS_PATHMUSTEXIST = 0x800,
				FOS_FILEMUSTEXIST = 0x1000,
				FOS_CREATEPROMPT = 0x2000,
				FOS_SHAREAWARE = 0x4000,
				FOS_NOREADONLYRETURN = 0x8000,
				FOS_NOTESTFILECREATE = 0x10000,
				FOS_HIDEMRUPLACES = 0x20000,
				FOS_HIDEPINNEDPLACES = 0x40000,
				FOS_NODEREFERENCELINKS = 0x100000,
				FOS_OKBUTTONNEEDSINTERACTION = 0x200000,
				FOS_DONTADDTORECENT = 0x2000000,
				FOS_FORCESHOWHIDDEN = 0x10000000,
				FOS_DEFAULTNOMINIMODE = 0x20000000,
				FOS_FORCEPREVIEWPANEON = 0x40000000,
				FOS_SUPPORTSTREAMABLEITEMS = 0x80000000
			}

			[DllImport("shell32.dll", PreserveSig = true)]
			internal static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, in Guid riid, out IShellItem ppv);

			[DllImport("shell32.dll", PreserveSig = true)]
			internal static extern int SHCreateItemFromIDList(IntPtr pidl, in Guid riid, out IShellItem ppv);
		}
	}
}

namespace Au.Types {
	/// <summary>
	/// <see cref="FileOpenSaveDialog.CommonFlags"/>.
	/// </summary>
	[Flags]
	public enum FOSFlags : uint //the values are like in FOS, some xored
	{
		/// <summary>Add the selected file to recent documents. See API <msdn>SHAddToRecentDocs</msdn>.</summary>
		AddToRecent = 0x2000000,

		/// <summary>Do not check for situations that would prevent an application from opening the selected file, such as sharing violations or access denied errors.</summary>
		NoValidateAccess = 0x100,

		///// <summary>The user can enter a path that does not exist, like C:\does not exist\file.txt.</summary>
		//NoValidatePath = 0x800, //does not work. Always validates.

		/// <summary>Shortcuts should not be treated as their target items, allowing an application to open a .lnk file.</summary>
		NoDereferenceLinks = 0x100000,

		/// <summary>Show hidden and system items.</summary>
		ShowHidden = 0x10000000,
	}
}
