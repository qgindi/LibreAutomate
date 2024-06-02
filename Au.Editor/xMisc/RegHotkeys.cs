static class RegHotkeys {
	static RegisteredHotkey[] _a = new RegisteredHotkey[9];
	
	public enum Id {
		QuickCaptureMenu, QuickCaptureDwnd, QuickCaptureDelm, QuickCaptureDuiimage,
		DebugNext, DebugStep, DebugStepOut, DebugContinue, DebugPause,
	}
	const Id _DebugFirst = Id.DebugNext, _DebugLast = Id.DebugPause;
	
	public static void RegisterPermanent() {
#if !IDE_LA
		(string keys, string menu)[] g = [
			(App.Settings.hotkeys.tool_quick, nameof(Menus.Code.Simple.Quick_capturing_info)),
			(App.Settings.hotkeys.tool_wnd, nameof(Menus.Code.wnd)),
			(App.Settings.hotkeys.tool_elm, nameof(Menus.Code.elm)),
			(App.Settings.hotkeys.tool_uiimage, nameof(Menus.Code.uiimage)),
		];
		for (int i = 0; i < (int)_DebugFirst; i++) {
			string keys = g[i].keys;
			if (!keys.NE()) {
				try {
					if (!_a[i].Register(i, keys, App.Hmain, noRepeat: true)) {
						print.warning($"Failed to register hotkey {keys}. Look in Options > Hotkeys.", -1);
						keys = null;
					}
				}
				catch (Exception ex) { print.it(ex); keys = null; }
			}
			if (i > 0) App.Commands[g[i].menu].MenuItem.InputGestureText = keys;
		}
#endif
	}
	
	public static void UnregisterPermanent() {
		for (int i = 0; i < (int)_DebugFirst; i++) _a[i].Unregister();
	}
	
	public static void RegisterDebug() {
		for (int i = (int)_DebugFirst; i <= (int)_DebugLast; i++) {
			string keys = (Id)i switch { Id.DebugNext => "F10", Id.DebugStep => "F11", Id.DebugStepOut => "Shift+F11", Id.DebugContinue => "F5", Id.DebugPause => "F6", _ => null };
			if (!_a[i].Register(i, keys, App.Hmain)) print.warning($"Failed to register hotkey {keys}.", -1);
		}
	}
	
	public static void UnregisterDebug() {
		for (int i = (int)_DebugFirst; i <= (int)_DebugLast; i++) _a[i].Unregister();
	}
	
	internal static void WmHotkey_(nint wParam) {
		var id = (Id)(int)wParam;
		switch (id) {
		case Id.QuickCaptureMenu: Au.Tools.QuickCapture.Menu(); break;
		case Id.QuickCaptureDwnd: Au.Tools.QuickCapture.AoolDwnd(); break;
		case Id.QuickCaptureDelm: Au.Tools.QuickCapture.ToolDelm(); break;
		case Id.QuickCaptureDuiimage: Au.Tools.QuickCapture.ToolDuiimage(); break;
		case >= _DebugFirst and <= _DebugLast: Panels.Debug.WmHotkey_(id); break;
		}
	}
}
