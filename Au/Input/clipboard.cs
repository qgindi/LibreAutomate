namespace Au;

/// <summary>
/// Clipboard functions. Copy, paste, get and set clipboard text and other data.
/// </summary>
/// <remarks>
/// This class is similar to the .NET <see cref="System.Windows.Forms.Clipboard"/> class, which uses OLE API, works only in STA threads and does not work well in automation scripts. This class uses non-OLE API and works well in automation scripts and any threads.
/// 
/// To set/get clipboard data of non-text formats, use class <see cref="clipboardData"/>; to paste, use it with <see cref="pasteData"/>; to copy (get from the active app), use it with <see cref="copyData{T}(Func{T}, bool, OKey, KHotkey, int)"/>.
/// 
/// Don't copy/paste in windows of own thread. Call it from another thread. Example in <see cref="keys.send"/>.
/// </remarks>
public static class clipboard {
	/// <summary>
	/// Clears the clipboard.
	/// </summary>
	/// <exception cref="AuException">Failed to open clipboard (after 10 s of wait/retry).</exception>
	public static void clear() {
		using (new OpenClipboard_(false)) EmptyClipboard_();
	}

	internal static void EmptyClipboard_() {
		if (!Api.EmptyClipboard()) Debug.Assert(false);
	}

	/// <summary>
	/// Gets or sets clipboard text.
	/// </summary>
	/// <value><c>null</c> if there is no text.</value>
	/// <exception cref="AuException">Failed to open clipboard (after 10 s of wait/retry) or set clipboard data.</exception>
	/// <exception cref="OutOfMemoryException">The <c>set</c> function failed to allocate memory.</exception>
	/// <remarks>
	/// The <c>get</c> function calls <see cref="clipboardData.getText"/>.
	/// 
	/// Gets/sets only data of text format. For other formats (files, HTML, image, etc) use <see cref="clipboardData"/> class.
	/// </remarks>
	public static string text {
		get => clipboardData.getText();
		set {
			using (new OpenClipboard_(true)) {
				EmptyClipboard_();
				if (value != null) clipboardData.SetText_(value);
			}
		}
	}

	//Sets text (string) or multi-format data (Data). Clipboard must be open.
	static void _SetClipboard(object data, bool renderLater) {
		switch (data) {
		case clipboardData d:
			d.SetOpenClipboard(renderLater);
			break;
		case string s:
			if (renderLater) Api.SetClipboardData(Api.CF_UNICODETEXT, default);
			else clipboardData.SetText_(s);
			break;
		}
	}

	/// <summary>
	/// Calls API <c>SetClipboardData("Clipboard Viewer Ignore")</c>. Clipboard must be open.
	/// Then clipboard manager/viewer/etc programs that are aware of this convention don't try to get our clipboard data while we are pasting.
	/// Tested apps that support it: Ditto, Clipdiary. Other 5 tested apps don't. Windows 10 Clipboard History doesn't.
	/// </summary>
	static void _SetClipboardData_ClipboardViewerIgnore() {
		Api.SetClipboardData(ClipFormats.ClipboardViewerIgnore, Api.GlobalAlloc(Api.GMEM_MOVEABLE | Api.GMEM_ZEROINIT, 1));
		//tested: hMem cannot be default(IntPtr) or 0 bytes.
	}

	/// <summary>
	/// Gets the selected text from the focused app using the clipboard.
	/// </summary>
	/// <param name="cut">Use <c>Ctrl+X</c>.</param>
	/// <param name="options">
	/// Options. If <c>null</c> (default), uses <see cref="opt.key"/>.
	/// Uses <see cref="OKey.RestoreClipboard"/>, <see cref="OKey.NoBlockInput"/>, <see cref="OKey.KeySpeedClipboard"/>. Does not use <see cref="OKey.Hook"/>.
	/// </param>
	/// <param name="hotkey">Keys to use instead of <c>Ctrl+C</c> or <c>Ctrl+X</c>. Example: <c>hotkey: "Ctrl+Shift+C"</c>. Overrides <i>cut</i>.</param>
	/// <param name="timeoutMS">Max time to wait until the focused app sets clipboard data, in milliseconds. If 0 (default), the timeout is 3000 ms. The function waits up to 10 times longer if the window is hung.</param>
	/// <exception cref="AuException">Failed. Fails if there is no focused window or if it does not set clipboard data.</exception>
	/// <exception cref="InputDesktopException"></exception>
	/// <remarks>
	/// Also can get file paths, as multiline text.
	/// 
	/// Sends keys <c>Ctrl+C</c>, waits until the focused app sets clipboard data, gets it, finally restores clipboard data.
	/// Fails if the focused app does not set clipboard text or file paths, for example if there is no selected text/files.
	/// Works with console windows too, even if they don't support <c>Ctrl+C</c>.
	/// </remarks>
	public static string copy(bool cut = false, OKey options = null, [ParamString(PSFormat.Hotkey)] KHotkey hotkey = default, int timeoutMS = 0) {
		return _Copy(cut, hotkey, options, timeoutMS);
		//rejected: 'format' parameter. Not useful.
	}
	
	/// <summary>
	/// Calls <see cref="copy"/> and handles exceptions.
	/// </summary>
	/// <returns>Returns <c>false</c> if failed.</returns>
	/// <param name="text">Receives the copied text.</param>
	/// <param name="timeout">Max time to wait until the focused app sets clipboard data, in milliseconds. The function waits up to 10 times longer if the window is hung.</param>
	/// <param name="warning">Call <see cref="print.warning"/>.</param>
	/// <param name="osd">Call <see cref="osdText.showTransparentText"/> with text <c>"Failed to copy text"</c>.</param>
	/// <inheritdoc cref="copy" path="//param|//remarks"/>
	public static bool tryCopy(out string text, int timeout, bool warning = false, bool osd = false, OKey options = null, [ParamString(PSFormat.Hotkey)] KHotkey hotkey = default) {
		try {
			text = _Copy(false, hotkey, options, Math.Max(timeout, 25));
			return true;
		}
		catch (Exception e1) {
			if (warning) print.warning(e1);
			if (osd) osdText.showTransparentText("Failed to copy text");
			text = null;
			return false;
		}
	}

	/// <summary>
	/// Calls <see cref="copy"/> and handles exceptions.
	/// </summary>
	/// <returns>Returns <c>false</c> if failed.</returns>
	/// <param name="text">Receives the copied text.</param>
	/// <param name="warning">Call <see cref="print.warning"/>. Default <c>true</c>.</param>
	/// <param name="osd">Call <see cref="osdText.showTransparentText"/> with text <c>"Failed to copy text"</c>. Default <c>true</c>.</param>
	/// <inheritdoc cref="copy" path="//param|//remarks"/>
	[EditorBrowsable(EditorBrowsableState.Never)] //obsolete. Not optimal parameters.
	public static bool tryCopy(out string text, bool cut = false, OKey options = null, bool warning = true, bool osd = true, [ParamString(PSFormat.Hotkey)] KHotkey hotkey = default, int timeoutMS = 0) {
		try {
			text = _Copy(cut, hotkey, options, timeoutMS);
			return true;
		}
		catch (Exception e1) {
			if (warning) print.warning(e1);
			if (osd) osdText.showTransparentText("Failed to copy text");
			text = null;
			return false;
		}
	}

	/// <summary>
	/// Gets data of any formats from the focused app using the clipboard and a callback function.
	/// </summary>
	/// <returns>The return value of <i>callback</i>.</returns>
	/// <param name="callback">Callback function. It can get clipboard data of any formats. It can use any clipboard functions, for example <see cref="clipboardData"/> or <see cref="System.Windows.Forms.Clipboard"/>. Don't call copy/paste functions.</param>
	/// <remarks>
	/// Sends keys <c>Ctrl+C</c>, waits until the focused app sets clipboard data, calls the callback function that gets it, finally restores clipboard data.
	/// Fails if the focused app does not set clipboard data.
	/// Works with console windows too, even if they don't support <c>Ctrl+C</c>.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var image = clipboard.copyData(() => clipboardData.getImage());
	/// 
	/// var (text, files) = clipboard.copyData(() => (clipboardData.getText(), clipboardData.getFiles()));
	/// ]]></code>
	/// </example>
	/// <inheritdoc cref="copy"/>
	public static T copyData<T>(Func<T> callback, bool cut = false, OKey options = null, [ParamString(PSFormat.Hotkey)] KHotkey hotkey = default, int timeoutMS = 0) {
		Not_.Null(callback);
		T r = default;
		_Copy(cut, hotkey, options, timeoutMS, () => { r = callback(); });
		return r;
	}

	///
	[EditorBrowsable(EditorBrowsableState.Never)] //obsolete
	public static void copyData(Action callback, bool cut = false, OKey options = null, KHotkey hotkey = default) {
		Not_.Null(callback);
		_Copy(cut, hotkey, options, 0, callback);
	}

	static string _Copy(bool cut, KHotkey hotkey, OKey options, int timeoutMS, Action callback = null) {
		string R = null;
		var optk = options ?? opt.key;
		bool restore = optk.RestoreClipboard;
		_ClipboardListener listener = null;
		_DisableClipboardHistory disableCH = default;
		var bi = new inputBlocker() { ResendBlockedKeys = true };
		var oc = new OpenClipboard_(createOwner: true, noOpenNow: !restore);
		try {
			if (!optk.NoBlockInput) bi.Start(BIEvents.Keys);
			keys.Internal_.ReleaseModAndDisableModMenu();

			disableCH.Disable(); //fast

			var save = new _SaveRestore();
			if (restore) {
				save.Save();
				oc.Close(false); //close clipboard; don't destroy our clipboard owner window
			}

			wnd wFocus = keys.Internal_.GetWndFocusedOrActive(requireFocus: true);
			listener = new _ClipboardListener(false, null, oc.WndClipOwner, wFocus);

			if (!Api.AddClipboardFormatListener(oc.WndClipOwner)) throw new AuException();
			var ctrlC = new keys.Internal_.SendCopyPaste();
			try {
				if (wFocus.IsConsole) {
					wFocus.Post(Api.WM_SYSCOMMAND, 65520);
					//system menu &Edit > &Copy; tested on all OS; Windows 10 supports Ctrl+C, but it may be disabled.
				} else {
					if (hotkey.Key == 0) hotkey = new(KMod.Ctrl, cut ? KKey.X : KKey.C);
					ctrlC.Press(hotkey, optk, wFocus);
				}

				//wait until the app sets clipboard text
				listener.Wait(ref ctrlC, timeoutMS);
			}
			finally {
				ctrlC.Release();
				Api.RemoveClipboardFormatListener(oc.WndClipOwner);
			}

			wFocus.SendTimeout(500, out _, 0); //workaround: in SharpDevelop and ILSpy (both WPF), API GetClipboardData takes ~1 s. Need to sleep min 10 ms or send message.

			if (callback != null) {
				callback();
				if (restore) oc.Reopen();
			} else {
				oc.Reopen();
				R = clipboardData.GetText_(0);
			}

			if (restore) save.Restore();
		}
		finally {
			oc.Dispose();
			bi.Dispose();
			disableCH.Restore();
		}
		GC.KeepAlive(listener);
		if (R == null && callback == null) throw new AuException("*copy text"); //no text in the clipboard. Probably not a text control; if text control but empty selection, usually throws in Wait, not here, because the target app then does not use the clipboard.
		return R;
	}

	/// <summary>
	/// Pastes text or HTML into the focused app using the clipboard.
	/// </summary>
	/// <param name="text">Text. Can be <c>null</c> if <i>html</i> used.</param>
	/// <param name="html">
	/// HTML. Can be full HTML or fragment. See <see cref="clipboardData.AddHtml"/>. Can be <c>null</c>.
	/// Can be specified only <i>text</i> or only <i>html</i> or both. If both, will paste <i>html</i> in apps that support it, elsewhere <i>text</i>. If only <i>html</i>, in apps that don't support HTML will paste <i>html</i> as text.
	/// </param>
	/// <param name="options">
	/// Options. If <c>null</c> (default), uses <see cref="opt.key"/>.
	/// Uses <see cref="OKey.RestoreClipboard"/>, <see cref="OKey.PasteWorkaround"/>, <see cref="OKey.NoBlockInput"/>, <see cref="OKey.SleepFinally"/>, <see cref="OKey.Hook"/>, <see cref="OKey.KeySpeedClipboard"/>.
	/// </param>
	/// <param name="hotkey">Keys to use instead of <c>Ctrl+V</c>. Example: <c>hotkey: "Ctrl+Shift+V"</c>.</param>
	/// <param name="timeoutMS">Max time to wait until the focused app gets clipboard data, in milliseconds. If 0 (default), the timeout is 3000 ms. The function waits up to 10 times longer if the window is hung.</param>
	/// <exception cref="AuException">Failed. Fails if there is no focused window or if it does not get clipboard data.</exception>
	/// <exception cref="InputDesktopException"></exception>
	/// <remarks>
	/// Sets clipboard data, sends keys <c>Ctrl+V</c>, waits until the focused app gets clipboard data, finally restores clipboard data.
	/// Fails if nothing gets clipboard data in several seconds.
	/// Works with console windows too, even if they don't support <c>Ctrl+V</c>.
	/// 
	/// A clipboard viewer/manager program can make this function slower and less reliable, unless it supports <see cref="ClipFormats.ClipboardViewerIgnore"/> or gets clipboard data with a delay.
	/// Possible problems with some virtual PC programs. Either pasting does not work in their windows, or they use a hidden clipboard viewer that makes this function slower and less reliable.
	/// </remarks>
	/// <seealso cref="keys.sendt"/>
	/// <example>
	/// <code><![CDATA[
	/// clipboard.paste("Example\r\n");
	/// ]]></code>
	/// </example>
	public static void paste(string text, string html = null, OKey options = null, [ParamString(PSFormat.Hotkey)] KHotkey hotkey = default, int timeoutMS = 0) {
		if (text.NE() && html.NE()) return;
		object data = text;
		if (html != null) data = new clipboardData().AddHtml(html).AddText(text ?? html);
		_Paste(data, options, hotkey, timeoutMS);
	}
	//problem: fails to paste in VMware player. Could add an option to not sync, but fails anyway because VMware gets clipboard with a big delay.

	/// <summary>
	/// Calls <see cref="paste"/> and handles exceptions.
	/// </summary>
	/// <returns>Returns <c>false</c> if failed.</returns>
	/// <param name="timeout">Max time to wait until the focused app gets clipboard data, in milliseconds. The function waits up to 10 times longer if the window is hung.</param>
	/// <param name="warning">Call <see cref="print.warning"/>.</param>
	/// <param name="osd">Call <see cref="osdText.showTransparentText"/> with text <c>"Failed to paste text"</c>.</param>
	/// <inheritdoc cref="paste" path="//param|//remarks"/>
	public static bool tryPaste(string text, int timeout, bool warning = false, bool osd = false, string html = null, OKey options = null, [ParamString(PSFormat.Hotkey)] KHotkey hotkey = default) {
		try {
			paste(text, html, options, hotkey, Math.Max(timeout, 25));
			return true;
		}
		catch (Exception e1) {
			if (warning) print.warning(e1);
			if (osd) osdText.showTransparentText("Failed to paste text");
			return false;
		}
	}

	/// <summary>
	/// Calls <see cref="paste"/> and handles exceptions.
	/// </summary>
	/// <returns>Returns <c>false</c> if failed.</returns>
	/// <param name="warning">Call <see cref="print.warning"/>. Default <c>true</c>.</param>
	/// <param name="osd">Call <see cref="osdText.showTransparentText"/> with text <c>"Failed to paste text"</c>. Default <c>true</c>.</param>
	/// <inheritdoc cref="paste" path="//param|//remarks"/>
	[EditorBrowsable(EditorBrowsableState.Never)] //obsolete. Not optimal parameters.
	public static bool tryPaste(string text, string html = null, OKey options = null, bool warning = true, bool osd = true, [ParamString(PSFormat.Hotkey)] KHotkey hotkey = default, int timeoutMS = 0) {
		try {
			paste(text, html, options, hotkey, timeoutMS);
			return true;
		}
		catch (Exception e1) {
			if (warning) print.warning(e1);
			if (osd) osdText.showTransparentText("Failed to paste text");
			return false;
		}
	}

	/// <summary>
	/// Pastes data added to a <see cref="clipboardData"/> variable into the focused app using the clipboard.
	/// </summary>
	/// <example>
	/// Paste data of two formats: HTML and text.
	/// <code><![CDATA[
	/// clipboard.pasteData(new clipboardData().AddHtml("<b>text</b>").AddText("text"));
	/// ]]></code>
	/// </example>
	/// <inheritdoc cref="paste"/>
	public static void pasteData(clipboardData data, OKey options = null, [ParamString(PSFormat.Hotkey)] KHotkey hotkey = default, int timeoutMS = 0) {
		Not_.Null(data);
		_Paste(data, options, hotkey, timeoutMS);
	}

	//rejected. Should use some UI-created/saved data containing all three formats.
	//public static void pasteRichText(string text, string rtf, string html = null, OKey options = null)
	//{
	//	var a = new List<(int, object)>();
	//	if(!text.NE()) a.Add((0, text));
	//	if(!rtf.NE()) a.Add((Lib.RtfFormat, rtf));
	//	if(!html.NE()) a.Add((Lib.HtmlFormat, html));
	//	if(a.Count == 0) return;
	//	_Paste(a, options);
	//}

	static void _Paste(object data, OKey options, KHotkey hotkey, int timeoutMS) {
		var wFocus = keys.Internal_.GetWndFocusedOrActive(requireFocus: true);
		var optk = options ?? opt.key;
		using (var bi = new inputBlocker { ResendBlockedKeys = true }) {
			if (!optk.NoBlockInput) bi.Start(BIEvents.Keys);
			keys.Internal_.ReleaseModAndDisableModMenu();
			optk = optk.GetHookOptionsOrThis_(wFocus);
			Paste_(data, optk, wFocus, hotkey, timeoutMS);
		}

		int sleepFinally = optk.SleepFinally;
		if (sleepFinally > 0) keys.Internal_.Sleep(sleepFinally);
	}

	/// <summary>
	/// Used by <see cref="clipboard"/> and <see cref="keys"/>.
	/// The caller should block user input (if need), release modifier keys, get <i>optk</i>/<i>wFocus</i>, sleep finally (if need).
	/// </summary>
	/// <param name="data">string or <b>clipboardData</b>.</param>
	internal static void Paste_(object data, OKey optk, wnd wFocus, KHotkey hotkey = default, int timeoutMS = 0) {
		bool isConsole = wFocus.IsConsole;
		List<KKey> andKeys = null;

		if (optk.PasteWorkaround && data is string s && !isConsole) {
			var s2 = s.TrimEnd("\r\n\t ");
			if (s2 != s) {
				andKeys = new List<KKey>();
				for (int i = s2.Length; i < s.Length; i++) {
					char ch = s[i];
					if (ch == '\n') { if (i > 0 && s[i - 1] == '\r') continue; ch = '\r'; }
					andKeys.Add((KKey)ch);
				}
				if (s2.Length == 0) {
					keys.Internal_.SendCopyPaste.AndSendKeys(andKeys, optk);
					return;
				}
				data = s2;
				//rejected: alternative workaround - convert to RTF.
				//	It works in Word, WordPad, OO, LO.
				//	But then eg Word for it uses default formatting instead of current formatting.
				//	Also some apps may not fully support our RTF and add different text, eg '?' for non-ASCII chars.
			}
		}

		bool sync = true; //FUTURE: option to turn off, depending on window.
		_ClipboardListener listener = null;
		_DisableClipboardHistory disableCH = default;
		var oc = new OpenClipboard_(true);
		try {
			disableCH.Disable(); //fast

			bool restore = optk.RestoreClipboard;
			var save = new _SaveRestore();
			if (restore) save.Save();

			EmptyClipboard_();
			_SetClipboardData_ClipboardViewerIgnore();
			_SetClipboard(data, renderLater: sync);
			oc.Close(false); //close clipboard; don't destroy our clipboard owner window
			if (sync) listener = new _ClipboardListener(true, data, oc.WndClipOwner, wFocus);
			//info:
			//	oc ctor creates a temporary message-only clipboard owner window. Its wndproc initially is DefWindowProc.
			//	listener ctor subclasses it. Its wndproc receives WM_RENDERFORMAT which sets clipboard data etc.

			var ctrlV = new keys.Internal_.SendCopyPaste();
			try {
				if (isConsole) {
					wFocus.Post(Api.WM_SYSCOMMAND, 65521);
					//system menu &Edit > &Paste; tested on all OS; Windows 10 supports Ctrl+V, but it can be disabled.
				} else {
					if (hotkey.Key == 0) hotkey = new(KMod.Ctrl, KKey.V);
					ctrlV.Press(hotkey, optk, wFocus, andKeys: andKeys);
				}

				//wait until the app gets clipboard data
				if (sync) {
					listener.Wait(ref ctrlV, timeoutMS);
					if (listener.FailedToSetData != null) throw new AuException(listener.FailedToSetData.Message);
					if (listener.IsBadWindow) sync = false;
				}
				if (!sync) {
					keys.Internal_.Sleep(keys.Internal_.LimitSleepTime(optk.KeySpeedClipboard)); //if too long, may autorepeat, eg BlueStacks after 500 ms
				}
			}
			finally {
				ctrlV.Release();
			}

			if (restore && !save.IsSaved) restore = false;

			//CONSIDER: optk.SleepClipboard. If 0, uses smart sync, else simply sleeps.
			for (int i = 0, n = sync ? 3 : (restore ? 25 : 15); i < n; i++) {
				wFocus.SendTimeout(1000, out _, 0, flags: 0);
				keys.Internal_.Sleep(i + 3);

				//info: repeats this min 3 times as a workaround for this Dreamweaver problem:
				//	First time after starting DW, if several Paste called in loop, the first pasted text is of the second Paste.
			}

			if (restore && oc.Reopen(true)) save.Restore();
		}
		finally {
			oc.Dispose();
			disableCH.Restore();
		}
		GC.KeepAlive(listener);
	}

	/// <summary>
	/// Waits until the target app gets (when pasting) or sets (when copying) clipboard text.
	/// For it subclasses our clipboard owner window and uses clipboard messages. Does not unsubclass.
	/// </summary>
	class _ClipboardListener : WaitVariable_ {
		bool _paste; //true if used for paste, false if for copy
		object _data; //string or Data. null if !_paste.
		WNDPROC _wndProc;
		//wnd _wPrevClipViewer;
		wnd _wFocus;

		/// <summary>
		/// The clipboard message has been received. Probably the target window responded to the <c>Ctrl+C</c> or <c>Ctrl+V</c>.
		/// When pasting, it is unreliable because of clipboard viewers/managers/etc. The caller also must check <b>IsBadWindow</b>.
		/// </summary>
		public bool Success => waitVar;

		/// <summary>
		/// When pasting, <c>true</c> if probably not the target process retrieved clipboard data. Probably a clipboard viewer/manager/etc.
		/// Not used when copying.
		/// </summary>
		public bool IsBadWindow;

		/// <summary>
		/// Exception thrown/caught when failed to set clipboard data.
		/// </summary>
		public Exception FailedToSetData;

		/// <summary>
		/// Subclasses <i>clipOwner</i>.
		/// </summary>
		/// <param name="paste"><c>true</c> if used for paste, <c>false</c> if for copy.</param>
		/// <param name="data">If used for paste, can be string containing Unicode text or <b>int</b>/<b>string</b> dictionary containing clipboard format/data.</param>
		/// <param name="clipOwner">Our clipboard owner window.</param>
		/// <param name="wFocus">The target control or window.</param>
		public _ClipboardListener(bool paste, object data, wnd clipOwner, wnd wFocus) {
			_paste = paste;
			_data = data;
			_wndProc = _WndProc;
			_wFocus = wFocus;
			WndUtil.SubclassUnsafe_(clipOwner, _wndProc);

			//rejected: use SetClipboardViewer to block clipboard managers/viewers/etc. This was used in QM2.
			//	Nowadays most such programs don't use SetClipboardViewer. They use AddClipboardFormatListener+WM_CLIPBOARDUPDATE.
			//	known apps that have clipboard viewer installed with SetClipboardViewer:
			//		OpenOffice, LibreOffice: tested Writer, Calc.
			//		VLC: after first Paste.
			//_wPrevClipViewer = Api.SetClipboardViewer(clipOwner);
			//print.it(_wPrevClipViewer);
		}

		/// <summary>
		/// Waits until the target app gets (when pasting) or sets (when copying) clipboard text.
		/// Throws <b>AuException</b> on timeout (default 3 s normally, 28 s if the target window is hung).
		/// </summary>
		/// <param name="ctrlKey">The variable that was used to send <c>Ctrl+V</c> or <c>Ctrl+C</c>. This function may call <b>Release</b> to avoid too long <c>Ctrl</c> down.</param>
		public void Wait(ref keys.Internal_.SendCopyPaste ctrlKey, int timeoutMS = 0) {
			//print.it(Success); //on Paste often already true, because SendInput dispatches sent messages
			int n = 6, t = 500; //max 3 s (6*500 ms). If hung, max 28 s.
			if (timeoutMS > 0) { n = (timeoutMS + 500 - 1) / 500; t = timeoutMS / n; }
			while (!Success) {
				wait.Wait_(t, WHFlags.DoEvents, stopVar: this);
				if (Success) break;
				if (--n == 0) throw new AuException(_paste ? "*paste" : "*copy");
				ctrlKey.Release();
				//is hung?
				_wFocus.SendTimeout(t * 10, out _, 0, flags: 0);
			}
		}

		nint _WndProc(wnd w, int message, nint wParam, nint lParam) {
			//WndUtil.PrintMsg(w, message, wParam, lParam);

			switch (message) {
			//case Api.WM_DESTROY:
			//	Api.ChangeClipboardChain(w, _wPrevClipViewer);
			//	break;
			case Api.WM_RENDERFORMAT:
				if (_paste && !Success) {
					IsBadWindow = !_IsTargetWindow();

					//note: need to set clipboard data even if bad window.
					//	Else the clipboard program may retry in loop. Eg Ditto. Then often pasting fails.
					//	If IsBadWindow, we'll then sleep briefly.
					//	Good clipboard programs get clipboard data with a delay. Therefore usually they don't interfere, unless the target app is very slow.
					//		Eg Windows Clipboard History 200 ms. Eg Ditto default is 100 ms and can be changed.
					//	Also, after setting clipboard data we cannot wait for good window, because we'll not receive second WM_RENDERFORMAT.

					try { _SetClipboard(_data, false); }
					catch (Exception ex) { FailedToSetData = ex; } //cannot throw in wndproc, will throw later
					waitVar = true;
				}
				return 0;
			case Api.WM_CLIPBOARDUPDATE:
				//posted, not sent. Once, not for each format. Added in WinVista. QM2 used SetClipboardViewer/WM_DRAWCLIPBOARD.
				if (!_paste) waitVar = true;
				return 0;
			}

			return Api.DefWindowProc(w, message, wParam, lParam);

			//Returns false if probably not the target app reads from the clipboard. Probably a clipboard viewer/manager/etc.
			bool _IsTargetWindow() {
				wnd wOC = Api.GetOpenClipboardWindow();

				//int color = 0; if(wOC != _wFocus) color = wOC.ProcessId == _wFocus.ProcessId ? 0xFF0000 : 0xFF;
				//print.it($"<><c {color}>{wOC}</c>");

				if (wOC == _wFocus) return true;
				if (wOC.Is0) return true; //tested: none of tested apps calls OpenClipboard(0)
				if (wOC.ProcessId == _wFocus.ProcessId) return true; //often classnamed "CLIPBRDWNDCLASS". Some clipboard managers too, eg Ditto.
				if (osVersion.minWin10 && 0 != _wFocus.Window.IsUwpApp) {
					var prog = wOC.ProgramName;
					if (prog.Eqi("svchost.exe")) return true;
					//if (prog.Eqi("RuntimeBroker.exe")) return true; //used to be Store apps
					//tested: no problems on Win8.1
				}
				//tested: WinUI3 (cn "WinUIDesktopWin32WindowClass"): wOC != _wFocus, but same process.

				//CONSIDER: option to return true for user-known windows, eg using a callback. Print warning that includes wOC info.

				Debug_.Print(wOC.ToString());
				return false;

				//BlueStacks problems:
				//	Uses an aggressive viewer. Always debugprints while it is running, even when other apps are active.
				//	Sometimes pastes old text, usually after starting BlueStacks or after some time of not using it.
				//		With or without clipboard restoring.
				//		Then starts to work correctly always. Difficult to debug.
				//		KeySpeedClipboard=100 usually helps, but sometimes even 300 does not help.
			}
		}
	}

	/// <summary>
	/// Opens and closes clipboard using API <b>OpenClipboard</b> and <b>CloseClipboard</b>.
	/// Constructor tries to open for 10 s, then throws <b>AuException</b>.
	/// If the <i>createOwner</i> parameter is <c>true</c>, creates temporary message-only clipboard owner window.
	/// If the <i>noOpenNow</i> parameter is <c>true</c>, does not open, only creates owner if need.
	/// Dispose() closes clipboard and destroys the owner window.
	/// </summary>
	internal struct OpenClipboard_ : IDisposable {
		bool _isOpen;
		wnd _w;

		public wnd WndClipOwner => _w;

		public OpenClipboard_(bool createOwner, bool noOpenNow = false) {
			_isOpen = false;
			_w = default;
			if (createOwner) {
				_w = WndUtil.CreateWindowDWP_(messageOnly: true);
				//MSDN says, SetClipboardData fails if OpenClipboard called with 0 hwnd. It doesn't, but better use hwnd.
			}
			if (!noOpenNow) Reopen();
		}

		/// <summary>
		/// Opens again.
		/// Must be closed.
		/// Owner window should be not destroyed; does not create again.
		/// </summary>
		/// <param name="noThrow">If fails, return <c>false</c>, no exception. Also then waits 1 s instead of 10 s.</param>
		/// <exception cref="AuException">Failed to open.</exception>
		public bool Reopen(bool noThrow = false) {
			Debug.Assert(!_isOpen);
			var loop = new WaitLoop(new(noThrow ? -1 : -10) { Period = 1 });
			while (!Api.OpenClipboard(_w)) {
				int ec = lastError.code;
				if (!loop.Sleep()) {
					Dispose();
					if (noThrow) return false;
					throw new AuException(ec, "*open clipboard");
				}
			}
			_isOpen = true;
			return true;
		}

		public void Close(bool destroyOwnerWindow) {
			if (_isOpen) {
				Api.CloseClipboard();
				_isOpen = false;
			}
			if (destroyOwnerWindow && !_w.Is0) {
				Api.DestroyWindow(_w);
				_w = default;
			}
		}

		public void Dispose() => Close(true);
	}

	/// <summary>
	/// Saves and restores clipboard data.
	/// Clipboard must be open. Don't need to call <b>EmptyClipboard</b> before <b>Restore</b>.
	/// </summary>
	struct _SaveRestore {
		Dictionary<int, byte[]> _data;

		public void Save(bool debug = false) {
			var p1 = new perf.Instance(); //will need if debug==true. Don't delete the perf statements, they are used by a public function.
			bool allFormats = OKey.RestoreClipboardAllFormats || debug;
			string[] exceptFormats = OKey.RestoreClipboardExceptFormats;

			for (int format = 0; 0 != (format = Api.EnumClipboardFormats(format));) {
				bool skip = false; string name = null;
				if (!allFormats) {
					skip = format != Api.CF_UNICODETEXT;
				} else {
					//standard, private
					if (format < Api.CF_MAX) { //standard
						switch (format) {
						case Api.CF_OEMTEXT: //synthesized from other text formats
						case Api.CF_BITMAP: //synthesized from DIB formats
						case Api.CF_PALETTE: //rare, never mind
							skip = true;
							break;
						case Api.CF_METAFILEPICT:
						case Api.CF_ENHMETAFILE:
							skip = true; //never mind, maybe in the future
							break;
						}
					} else if (format < 0xC000) { //CF_OWNERDISPLAY, DSP, GDI, private
						skip = true; //never mind. Not auto-freed, etc. Rare.
					} //else registered

					if (!skip && exceptFormats != null && exceptFormats.Length != 0) {
						name = ClipFormats.GetName(format);
						foreach (string s in exceptFormats) if (s.Eqi(name)) { skip = true; break; }
					}
				}

				if (debug) {
					name ??= ClipFormats.GetName(format);
					if (skip) print.it($"{name,-62}  restore=False");
					else p1.First();
					//note: we don't call GetClipboardData for formats in exceptFormats, because the conditions must be like when really saving. Time of GetClipboardData(format2) may depend on whether called GetClipboardData(format1).
				}
				if (skip) continue;

				var data = Api.GetClipboardData(format);

				int size = (data == default) ? 0 : (int)Api.GlobalSize(data);
				if (size == 0 || size > 10 * 1024 * 1024) skip = true;
				//If data == default, probably the target app did SetClipboardData(NULL) but did not render data on WM_RENDERFORMAT.
				//	If we try to save/restore, we'll receive WM_RENDERFORMAT too. It can be dangerous.

				if (debug) {
					p1.Next();
					print.it($"{name,-32}  time={p1.TimeTotal,-8}  size={size,-8}  restore={!skip}");
					continue;
				}
				if (skip) continue;

				var b = Api.GlobalLock(data);
				Debug.Assert(b != default); if (b == default) continue;
				try {
					_data ??= new Dictionary<int, byte[]>();
					var a = new byte[size];
					Marshal.Copy(b, a, 0, size);
					_data.Add(format, a);
				}
				finally { Api.GlobalUnlock(data); }
			}
		}

		public void Restore() {
			if (_data == null) return;
			EmptyClipboard_();
			foreach (var v in _data) {
				var a = v.Value;
				var h = Api.GlobalAlloc(Api.GMEM_MOVEABLE, a.Length);
				var b = Api.GlobalLock(h);
				if (b != default) {
					try { Marshal.Copy(a, 0, b, a.Length); } finally { Api.GlobalUnlock(h); }
					if (default == Api.SetClipboardData(v.Key, h)) b = default;
				}
				Debug.Assert(b != default);
				if (b == default) Api.GlobalFree(h);
			}
		}

		public bool IsSaved => _data != null;
	}

	/// <summary>
	/// Temporarily disables Windows 10 Clipboard History.
	/// Note: before disabling, we must open clipboard, else Clipboard History could be suspended while it has clipboard open.
	/// </summary>
	struct _DisableClipboardHistory {
		//Pasting is unreliable with Windows 10 Clipboard History (CH).
		//Sometimes does not paste because OpenClipboard fails in the target app, because then CH has it open.
		//Then also _IsTargetWindow debugprints. Often just debugprints and waits briefly, but pasting works.
		//CH is enabled by default. Can be disabled in Settings > System > Clipboard.
		//If enabled, CH opens clipboard and gets text after 200 ms, and then repeats every several ms, total ~15 times and 50 ms.
		//	When the target app fails to OpenClipboard, Paste_ waits briefly and the script continues. We receive WM_RENDERFORMAT because CH gets text.
		//If disabled, CH still opens clipboard after 200 ms, total 1-3 times.
		//	When the target app fails to OpenClipboard, Paste_ waits and fails, because does not receive WM_RENDERFORMAT. It seems CH does not get text.
		//Possible workarounds, maybe untested or unreliable or too crazy:
		//	Hook posted messages (in C++ dll) and block WM_CLIPBOARDUPDATE.
		//		Now using.
		//		Simple and fast if using only 64-bit hook.
		//		Less reliable because the message is posted async and can arrive after we remove hook. Never noticed, even with Task.Delay(1).
		//		Does not work if our process is 32-bit. Also does not block viewers of different bitness processes. Never mind, it's rare and not so important.
		//		Blocks CH when this process isn't admin too.
		//		Blocks most other 64-bit clipboard viewers too.
		//	Temporarily SuspendThread. Tested, simple, fast, reliable.
		//		Find all "CLIPBRDWNDCLASS" message-only windows and suspend their threads.
		//		The service process is in user session and not admin.
		//		But then cannot copy/paste in winstore apps, eg Calculator and Stickynotes.
		//	Temporarily stop service "Clipboard User Service_xxxxxxx". Tested. Disables CH completely.
		//		Would be simple and fast, but need to find service name, it is with random suffix.
		//		But need to run as admin.
		//		When pasting, OS autotarts it again after 400-500 ms.
		//		Pausing fails, but can stop/start.
		//		OS does not allow to set startup type "Disabled". And auto-starts when eg the Settings page opened.
		//	Inject a dll into the target process and hook OpenClipboard, let it wait until succeeds. Too crazy.
		//	Send WM_CLOSE to the CH clipboard window (wOC). Tested, works, but too crazy.
		//	Temporarily RemoveClipboardFormatListener. Does not work.
		//Plus there are other OS parts that use clipboard viewers.
		//	Eg the Settings app in the Clipboard page opens/gets text after 500 ms, usually 2 times.
		//	Note: these workarounds may not help while the Clipboard page is open. Then eg pasting often fails because the target app cannot open clipboard.
		//Tested with Ditto too.
		//	Without workaround sometimes fails because the target app cannot open clipboard.
		//	With this workaround (hook) never fails.

#if true
		IntPtr _hh;
		//static int s_nhooks; //test how many hooks when eg pasting in loop with small sleep

		public void Disable() {
			//if (!osVersion.minWin10_1809 || osVersion.Is32BitProcessAnd64BitOS) return; //no, let's block clipboard viewers on all OS
			if (osVersion.is32BitProcessAnd64BitOS) return;
			//if (keys.isScrollLock) return;
			_hh = Cpp.Cpp_Clipboard(default);
			Debug.Assert(_hh != default);
			//print.it(++s_nhooks); //max 8 when all delays removed in script. Max 4-5 with default options. Max 3-4 with 10.ms() in loop.
		}

		public void Restore() {
			if (_hh == default) return;
			var hh = _hh; _hh = default;
			//remove hook later, when all posted WM_CLIPBOARDUPDATE probably are received.
			//	In my quick tests it always worked reliably, even with delay 1 ms. But I did not test with many clipboard viewers and in stress conditions.
			Task.Delay(100).ContinueWith(_ => { Cpp.Cpp_Clipboard(hh); /*s_nhooks--;*/ }); //info: if this process ends sooner, OS removes the hook
		}
#else
		List<Handle_> _a;

		public void Disable() {
			if (!osVersion.minWin10_1809) return;
			for (wnd w = default; ;) {
				w = wnd.findFast(null, "CLIPBRDWNDCLASS", true, w); if (w.Is0) break;
				int tid = w.GetThreadProcessId(out int pid); if (tid == 0) continue;
				if (!process.getName(pid, noSlowAPI: true).Eqi("svchost.exe")) continue;
				var ht = Api.OpenThread(Api.THREAD_SUSPEND_RESUME, false, tid); if (ht.Is0) continue;
				if (Api.SuspendThread(ht) < 0) { ht.Dispose(); continue; }
				_a ??= new List<Handle_>();
				_a.Add(ht);
			}
			Debug_.PrintIf(_a == null, "no suspended threads");
		}

		public void Restore() {
			if (_a == null) return;
			foreach (var ht in _a) {
				Api.ResumeThread(ht);
				ht.Dispose();
			}
		}
#endif
	}

	internal static void PrintClipboard_() {
		print.it("---- Clipboard ----");
		using var oc = new OpenClipboard_(true);
		Api.GetClipboardData(0); //JIT
		var save = new _SaveRestore();
		save.Save(true);
	}
}
