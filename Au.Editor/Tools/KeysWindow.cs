using Au.Controls;
using System.Windows;
using System.Windows.Controls;

namespace Au.Tools;

class KeysWindow : InfoWindow //KPopup
{
	public KeysWindow() : base(0) {
		Size = (500, 240);
		WindowName = "Keys";
		Name = "Ci.Keys"; //prevent hiding when activated
		CloseHides = true;
	}

	protected override void OnHandleCreated() {
		var c = Control1;
		c.aaTags.AddLinkTag("+a", o => _Insert(o)); //link that inserts a key etc
		c.aaTags.SetLinkStyle(new SciTags.UserDefinedStyle { textColor = 0x0080FF, underline = false }); //remove underline from links

		_SetText();

		base.OnHandleCreated();
	}

	void _SetText() {
		var s = ResourceUtil.GetString("tools/keys.txt").RxReplace(@"\{(.+?)\}(?!\})", "<+a>$1<>");
		if (_format != 0) {
			bool mod = _format == PSFormat.TriggerMod;
			int i = s.Find(mod ? "\n" : "\n<b>Operator") + 1;
			if (i > 0) s = s.ReplaceAt(i.., "");
			s = s.Replace("  <+a>right ▾<>", "");
		}
		this.Text = s;
	}

	/// <summary>
	/// Inserts s in InsertInControl, which can be either Panels.Editor.ZActiveDoc or a TextBox (only for a hotkey).
	/// </summary>
	void _Insert(string s) {
		FrameworkElement con = InsertInControl;
		if (con is System.Windows.Controls.TextBox tb) {
			if (!_GetTrueKeyName()) return;
			int pos = tb.CaretIndex;
			if (pos > 0 && s[0] != '+') { //insert space between keys, even if used for a hotkey
				var code = tb.Text;
				char k1 = code[pos - 1];
				if (k1 > ' ' && k1 != '(' && !(k1 == '+' && !code.Eq(pos - 2, '#'))) s = " " + s;
			}
			if (s.Ends(false, "Alt", "Ctrl", "Shift", "Win") > 0) s += "+";
		} else {
			Debug.Assert(con == Panels.Editor.ZActiveDoc);
			if (!CodeInfo.GetDocumentAndFindToken(out var cd, out var token)) return;
			if (true != token.IsInString(cd.pos, cd.code, out var si)) return;
			int pos = cd.pos, from = si.textSpan.Start, to = si.textSpan.End;

			switch (s) {
			case "text": _AddArg(", \"!\b\""); return;
			case "html": _AddArg(", \"%\b\""); return;
			case "sleepMs": _AddArg(", 100"); return;
			case "keyCode": _AddArg(", KKey.Left"); return;
			case "scanCode": _AddArg(", new KKeyScan(1, false)"); return;
			case "action": _AddArg(", new Action(() => { mouse.rightClick(); })"); return;
			}

			var code = cd.code;
			bool addArg = code[from] is '!' or '%' || code[from..cd.pos].Contains('^');
			string prefix = null, suffix = null;
			char k1 = code[pos - 1], k2 = code[pos];
			if (!addArg) {
				if (s[0] is '*' or '+') {
					if (k1 is '*' or '+') cd.sci?.aaaSelect(true, pos - 1, pos); //eg remove + from Alt+ if now selected *down
				} else {
					if (pos > from && k1 > ' ' && k1 != '(' && !(k1 == '+' && !code.Eq(pos - 2, '#'))) prefix = " "; //insert space between keys
				}
			}
			if (s.Ends(false, "Alt", "Ctrl", "Shift", "Win") > 0) suffix = "+";
			else if (!addArg && pos < to && k2 > ' ' && k2 is not (')' or '+' or '*')) suffix = "\b ";

			if (!_GetTrueKeyName()) return;
			s = prefix + s + suffix;

			if (addArg) {
				_AddArg($", \"{s}\b\"");
				return;
			}

			void _AddArg(string s) {
				int stringEnd = si.stringNode.Span.End;
				if (to == stringEnd) s = "\"" + s;
				cd.sci.aaaGoToPos(true, stringEnd);
				InsertCode.TextSimplyInControl(cd.sci, s);
			}
		}

		InsertCode.TextSimplyInControl(con, s);

		bool _GetTrueKeyName() {
			bool ok = true;
			if (s.Length == 2 && s[0] != '#' && !s[0].IsAsciiAlpha()) s = s[0] == '\\' ? "|" : s[..1]; //eg 2@ or /? or \|
			else if (s.Starts("right")) ok = _Menu("RAlt", "RCtrl", "RShift", "RWin");
			else if (s.Starts("lock")) ok = _Menu("CapsLock", "NumLock", "ScrollLock");
			else if (s.Starts("other")) ok = _Menu(s_rare);
			return ok;

			bool _Menu(params string[] a) {
				int j = popupMenu.showSimple(a) - 1;
				if (j < 0) return false;
				s = a[j];
				j = s.IndexOf(' '); if (j > 0) s = s[..j];
				return true;
			}
		}
	}

	static string[] s_rare = {
"BrowserBack", "BrowserForward", "BrowserRefresh", "BrowserStop", "BrowserSearch", "BrowserFavorites", "BrowserHome",
"LaunchMail", "LaunchMediaSelect", "LaunchApp1", "LaunchApp2",
"MediaNextTrack", "MediaPrevTrack", "MediaStop", "MediaPlayPause",
"VolumeMute", "VolumeDown", "VolumeUp",
"IMEKanaMode", "IMEHangulMode", "IMEJunjaMode", "IMEFinalMode", "IMEHanjaMode", "IMEKanjiMode", "IMEConvert", "IMENonconvert", "IMEAccept", "IMEModeChange", "IMEProcessKey",
"Break  //Ctrl+Pause", "Clear  //Shift+#5", "Sleep",
//"F13", "F14", "F15", "F16", "F17", "F18", "F19", "F20", "F21", "F22", "F23", "F24", //rejected
  };

	public void SetFormat(PSFormat format) {
		if (format == PSFormat.Keys) format = 0;
		if (format == _format) return;
		_format = format;
		_SetText();
	}
	PSFormat _format;
}
