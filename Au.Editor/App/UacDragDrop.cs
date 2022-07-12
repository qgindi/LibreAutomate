using System.Runtime.InteropServices.ComTypes;
using System.Windows.Interop;

class UacDragDrop
{
	public class AdminProcess
	{
		WinEventHook _hook; //SYSTEM_CAPTURESTART
		timer _timer; //tracks mouse etc
		bool _isDragMode; //is in drag-drop
		bool _isProcess2; //is our non-admin process started
		int _endCounter; //delays ending drag mode

		static AdminProcess s_inst;

		//Called with on=true when main form becomes visible.
		//Called with on=false when hidden or closed.
		public static void Enable(bool on) {
			if (on == (s_inst != null)) return;
			if (on) {
				if (uacInfo.ofThisProcess.Elevation != UacElevation.Full) return;
				s_inst = new AdminProcess();
			} else {
				s_inst._Dispose();
				s_inst = null;
			}
		}

		AdminProcess() {
			_timer = new timer(_Timer);

			//use hook to detect when drag-drop started
			_hook = new WinEventHook(EEvent.SYSTEM_CAPTURESTART, 0, d => {
				_EndedDragMode();
				if (0 == d.w.ClassNameIs("CLIPBRDWNDCLASS", "DragWindow")) return;
				_StartedDragMode();
			}, flags: EHookFlags.SKIPOWNPROCESS);

			//note: we don't use SYSTEM_CAPTUREEND. It's too early and sometimes missing.
			//tested: no EVENT_SYSTEM_DRAGDROPSTART events.
			//info: classname "DragWindow" is of Windows Store.
			//rejected: int pid = d.w.ProcessId; using(var u = uacInfo.ofProcess(pid)) { if(u == null || u.Elevation == UacElevation.Full) return; }
		}

		void _Dispose() {
			_hook.Dispose();
			_EndedDragMode();
		}

		void _StartedDragMode() {
			_isDragMode = true;
			_endCounter = 0;
			_wTransparent = default;
			_wWindow = default;
			_wTargetControl = default;
			_data = null;
			_timer.Every(30);
		}

		void _EndedDragMode() {
			if (!_isDragMode) return;
			_isDragMode = _isProcess2 = false;
			_timer.Stop();
			if (!_wTransparent.Is0) _wTransparent.Post(Api.WM_USER); //let it close self
		}

		//Every 30 ms while in drag mode.
		void _Timer(timer t) {
			if (!_isDragMode) return;

			//when mouse released, end drag mode with ~100 ms delay
			if (!mouse.isPressed(MButtons.Left | MButtons.Right)) { //calls GetKeyStateAsync. GetKeyState unreliable when in drag mode.
				if (++_endCounter == 4) _EndedDragMode();
				return;
			}
			_endCounter = 0;
			//end drag mode if _wTransparent died, eg did not find useful data on drag enter
			if (!_wTransparent.Is0 && !_wTransparent.IsAlive) {
				_EndedDragMode();
				return;
			}

			var w = wnd.fromMouse(WXYFlags.NeedWindow);
			if (!_isProcess2) {
				if (!w.IsOfThisProcess) return;
				_isProcess2 = true;
				_wWindow = w;
				new ProcessStarter_("Au.Editor.exe", "/dd " + CommandLine.MsgWnd.Handle.ToString()).StartUserIL();
			} else if (w != _wTransparent) {
				_wWindow = w;
				_SetTransparentSizeZorder();
			}
		}

		wnd _wTransparent; //our transparent non-admin window
		wnd _wWindow; //current our top-level admin window covered by _wTransparent

		//Called in admin process. Non-admin process may not be able to zorder its window above admin windows.
		void _SetTransparentSizeZorder() {
			if (_wWindow.IsOfThisProcess) {
				var r = _wWindow.Rect;
				_wTransparent.MoveL(r.left, r.top, r.Width, r.Height);
				_wTransparent.ZorderAbove(_wWindow);
			} else {
				_wTransparent.MoveL(0, 0, 0, 0);
			}
		}

		//SendMessage from _wTransparent on WM_CREATE.
		public static void OnTransparentWindowCreated(wnd wTransparent) {
			var x = s_inst;
			if (x?._isDragMode ?? false) {
				x._wTransparent = wTransparent;
				x._SetTransparentSizeZorder();
			} else wTransparent.Post(Api.WM_USER); //let it close self
		}

		public static int DragEvent(int event_, byte[] b) => s_inst?._DragEvent(event_, b) ?? 0;

		int _DragEvent(int event_, byte[] b) {
			if (!_isDragMode) return 0;
			DDEvent ev = (DDEvent)event_;
			if (ev == DDEvent.Leave) {
				if (!_wTargetControl.Is0) {
					_InvokeDT(_wTargetControl, ev, 0, 0, default);
					_wTargetControl = default;
				}
				return 0;
			}
			var a = Serializer_.Deserialize(b);
			int effect = a[0], keyState = a[1]; POINT pt = new(a[2], a[3]);
			if (ev == DDEvent.Enter) {
				_data = new System.Windows.DataObject();
				var t = new DDData { files = a[4], shell = a[5], text = a[6], linkName = a[7] };
				if (t.files != null) _data.SetData("FileDrop", t.files);
				if (t.shell != null) _SetDataBytes("Shell IDList Array", t.shell);
				if (t.text != null) _data.SetData("UnicodeText", t.text);
				if (t.linkName != null) _SetDataBytes("FileGroupDescriptorW", t.linkName);

				//workaround for: SetData writes byte[] in wrong format, probably serialized
				void _SetDataBytes(string name, byte[] a) => _data.SetData(name, new MemoryStream(a), false);
			}

			int ef = 0;
			var w = _wWindow.ChildFromXY(pt, WXYCFlags.ScreenXY);
			if (w.Is0) w = _wWindow;
			if (w != _wTargetControl && !_wTargetControl.Is0) {
				_InvokeDT(_wTargetControl, DDEvent.Leave, 0, 0, default);
				_wTargetControl = default;
			}
			if (!w.Is0 && w.IsOfThisProcess && w.IsEnabled(true)) {
				if (ev != 0 && _wTargetControl.Is0) {
					if (ev == DDEvent.Over) ev = 0;
					else _InvokeDT(w, DDEvent.Enter, effect, keyState, pt);
				}
				ef = _InvokeDT(_wTargetControl = w, ev, effect, keyState, pt);
			}

			if (ev == DDEvent.Drop) { _wTargetControl = default; }

			int _InvokeDT(wnd w, DDEvent ev, int effect, int keyState, POINT pt) {
				if (w.IsOfThisThread) return _InvokeDropTarget(w, ev, effect, keyState, pt);
				var d = HwndSource.FromHwnd(w.Window.Handle)?.Dispatcher;
				return d?.Invoke(() => _InvokeDropTarget(w, ev, effect, keyState, pt)) ?? 0;
			}

			return ef;
		}

		//data etc sent by _wTransparent on OnDragEnter. Then used here on enter/over/drop.
		System.Windows.DataObject _data;
		wnd _wTargetControl; //control or window from mouse

		int _InvokeDropTarget(wnd w, DDEvent ev, int effect, int keyState, POINT pt) {
			nint prop = w.Prop["OleDropTargetInterface"];
			if (prop == 0 && w != _wWindow) { //if w is of a HwndHost that does not register drop target, use that of the main window
				w = _wWindow;
				prop = w.Prop["OleDropTargetInterface"];
			}
			if (prop == 0) return 0;

			var data = ev == DDEvent.Enter || ev == DDEvent.Drop ? _data : null;
			int hr = Cpp.Cpp_CallIDroptarget(prop, (int)ev, data, keyState, pt, ref effect);
			if (hr != 0) effect = 0;
			return effect;
			//working with COM in C# often is difficult. Cannot call IDropTarget methods.
		}
	}

	//Drag-drop events.
	public enum DDEvent { Enter, Over, Drop, Leave } //don't change, used in C++

	//A window in non-admin process that accepts drag-drop events and relays to the admin process.
	//Covers our admin window. Almost transparent.
	public static class NonAdminProcess
	{
		static wnd _w; //our transparent window
		static wnd _msgWnd; //message-only IPC window in admin process
		static _DropTarget _dt; //GC
		static bool _enteredOnce;

		//Called from Main() in non-admin process when command line starts with /dd.
		public static void MainDD(string[] args) {
			_msgWnd = (wnd)args[1].ToInt();

			WndUtil.RegisterWindowClass("Au.Editor.DD", _WndProc);
			_w = WndUtil.CreateWindow("Au.Editor.DD", null, WS.POPUP | WS.DISABLED, WSE.LAYERED | WSE.NOACTIVATE | WSE.TOOLWINDOW | WSE.TOPMOST);
			Api.SetLayeredWindowAttributes(_w, 0, 1, 2);

			Thread.CurrentThread.TrySetApartmentState(ApartmentState.Unknown); //uninit MTA
			Api.OleInitialize(default);
			Api.RegisterDragDrop(_w, _dt = new _DropTarget());
			_msgWnd.Send(Api.WM_USER, 10, (nint)_w);
			Api.SetTimer(_w, 1, 1000, null);

			_w.ShowL(true);
			while (Api.GetMessage(out var m) > 0) Api.DispatchMessage(m);
			Api.OleUninitialize();
		}

		static nint _WndProc(wnd w, int msg, nint wParam, nint lParam) {
			switch (msg) {
			case Api.WM_DESTROY:
				Api.RevokeDragDrop(w);
				Api.PostQuitMessage(0);
				break;
			case Api.WM_TIMER:
				if (!_msgWnd.IsAlive) _Exit();
				break;
			case Api.WM_USER: //admin posts it when ended drag mode
				_Exit();
				break;
			}

			return Api.DefWindowProc(w, msg, wParam, lParam);
		}

		static void _Exit() { Api.DestroyWindow(_w); }

		class _DropTarget : Api.IDropTarget
		{
			void Api.IDropTarget.DragEnter(IDataObject d, int grfKeyState, POINT pt, ref int effect) {
				if (_enteredOnce) {
					(this as Api.IDropTarget).DragOver(grfKeyState, pt, ref effect);
					return;
				}

				DDData r = default;
				if (_enteredOnce = r.GetData(d)) {
					var b = Serializer_.Serialize(effect, grfKeyState, pt.x, pt.y, r.files, r.shell, r.text, r.linkName);
					effect = (int)WndCopyData.Send<byte>(_msgWnd, 110, b, (int)DDEvent.Enter);
				} else {
					_Exit();
				}
			}

			void Api.IDropTarget.DragOver(int grfKeyState, POINT pt, ref int effect) {
				if (!_enteredOnce) { effect = 0; return; }
				var b = Serializer_.Serialize(effect, grfKeyState, pt.x, pt.y);
				effect = (int)WndCopyData.Send<byte>(_msgWnd, 110, b, (int)DDEvent.Over);
			}

			void Api.IDropTarget.Drop(IDataObject d, int grfKeyState, POINT pt, ref int effect) {
				if (!_enteredOnce) { effect = 0; return; }
				_Exit();
				var b = Serializer_.Serialize(effect, grfKeyState, pt.x, pt.y);
				effect = (int)WndCopyData.Send<byte>(_msgWnd, 110, b, (int)DDEvent.Drop);
			}

			void Api.IDropTarget.DragLeave() {
				if (_enteredOnce) WndCopyData.Send<byte>(_msgWnd, 110, Serializer_.Serialize(), (int)DDEvent.Leave);
			}
		}
	}

}

struct DDData
{
	public string[] files;
	public byte[] shell, linkName;
	public string text;
	public bool scripts;

	public unsafe bool GetData(IDataObject d, bool getFileNodes = false) {
		try {
			FORMATETC fHdrop = default, fShell = default, fText = default, fDesc = default;
			var afe = new FORMATETC[1]; var fe = new int[1];
			var e = d.EnumFormatEtc(DATADIR.DATADIR_GET);
			while (e.Next(1, afe, fe) == 0 && fe[0] == 1) {
				if (afe[0].tymed != TYMED.TYMED_HGLOBAL) continue;
				int cf = (ushort)afe[0].cfFormat;
				if (cf == Api.CF_HDROP) fHdrop = afe[0];
				else if (cf == ClipFormats.ShellIDListArray_) fShell = afe[0];
				else if (cf == Api.CF_UNICODETEXT) fText = afe[0];
				else if (cf == ClipFormats.FileGroupDescriptorW_) fDesc = afe[0];
				else if (getFileNodes && cf >= 0xC000 && ClipFormats.GetName(cf) == "FileNode[]") return scripts = true;
			}
			if (fHdrop.cfFormat != 0) files = _GetFiles(ref fHdrop);
			else if (fShell.cfFormat != 0) shell = _GetBytes(ref fShell);
			else if (fText.cfFormat != 0) {
				text = _GetText(ref fText);
				if (fDesc.cfFormat != 0) linkName = _GetBytes(ref fDesc); //text is URL
			} else return false;
			return true;

			byte[] _GetBytes(ref FORMATETC fe) {
				d.GetData(ref fe, out var m);
				int n = (int)Api.GlobalSize(m.unionmember);
				var t = Api.GlobalLock(m.unionmember);
				try { return new Span<byte>((void*)t, n).ToArray(); }
				finally { Api.GlobalUnlock(m.unionmember); Api.ReleaseStgMedium(ref m); }
			}

			string _GetText(ref FORMATETC fe) {
				d.GetData(ref fe, out var m);
				var t = (char*)Api.GlobalLock(m.unionmember);
				int n = (int)Api.GlobalSize(m.unionmember) / 2; if (n > 0 && t[--n] != 0) n++;
				try { return new string(t, 0, n); }
				finally { Api.GlobalUnlock(m.unionmember); Api.ReleaseStgMedium(ref m); }
			}

			string[] _GetFiles(ref FORMATETC fe) {
				d.GetData(ref fe, out var m);
				try { return clipboardData.HdropToFiles_(m.unionmember); }
				finally { Api.ReleaseStgMedium(ref m); }
			}
		}
		catch (Exception ex) { Debug_.Print(ex); } //info: if from IE, IDataObject.GetData fails for all formats.
		return false;
	}
}
