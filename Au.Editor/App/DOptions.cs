using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using Microsoft.Win32;
using System.Windows.Controls.Primitives;
using Au.Tools;

class DOptions : KDialogWindow {
	public static void ZShow() {
		if (s_dialog == null) {
			s_dialog = new();
			s_dialog.Show();
		} else {
			s_dialog.Hwnd().ActivateL(true);
		}
	}
	static DOptions s_dialog;

	protected override void OnClosed(EventArgs e) {
		s_dialog = null;
		base.OnClosed(e);
	}

	wpfBuilder _b;
	TabControl _tc;

	DOptions() {
		Title = "Options";
		Owner = App.Wmain;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ShowInTaskbar = false;

		_b = new wpfBuilder(this).WinSize(550);
		_b.Row(-1).Add(out _tc).Height(300..);
		_b.R.AddOkCancel(apply: "_Apply");

		_General();
		//_Files();
		_Font();
		_Templates();
		_Code();
		_Hotkeys();
		_OS();

		//_tc.SelectedIndex = 2;

		_b.End();
	}

	/// <summary>
	/// Adds new TabItem to _tc. Creates and returns new wpfBuilder for building the tab page.
	/// </summary>
	wpfBuilder _Page(string name, WBPanelType panelType = WBPanelType.Grid) {
		var tp = new TabItem { Header = name };
		_tc.Items.Add(tp);
		return new wpfBuilder(tp, panelType).Margin("3");
	}

	void _General() {
		var b = _Page("General").Columns(-1, -1);
		//left column
		b.StartStack(vertical: true);
		b.Add(out KCheckBox startWithWin, "Start with Windows"); //note: must be the first checkbox in Options, because used for the QM2 forum registration security question
		b.Add(out KCheckBox startHidden, "Start hidden; hide when closing");
		b.End();
		//right column
		b.StartStack(vertical: true);
		b.Add("Run scripts when this workspace loaded", out TextBox startupScripts).Multiline(110, TextWrapping.NoWrap)
			.Validation(_startupScripts_Validation);
		b.End();
		b.End();

		//b.Loaded += () => {

		//};
		const string c_rkRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
		bool init_startWithWin = Registry.GetValue(@"HKEY_CURRENT_USER\" + c_rkRun, "Au.Editor", null) is string;
		startWithWin.IsChecked = init_startWithWin;
		startHidden.IsChecked = App.Settings.runHidden;
		string init_startupScripts = App.Model.StartupScriptsCsv;
		startupScripts.Text = init_startupScripts;

		_b.OkApply += e => {
			if (startWithWin.IsChecked != init_startWithWin) {
				try {
					using var rk = Registry.CurrentUser.OpenSubKey(c_rkRun, true);
					if (init_startWithWin) rk.DeleteValue("Au.Editor");
					else rk.SetValue("Au.Editor", $"\"{process.thisExePath}\"");
				}
				catch (Exception ex) { print.it("Failed to change 'Start with Windows'. " + ex.ToStringWithoutStack()); }
			}
			App.Settings.runHidden = startHidden.IsChecked;

			var s = startupScripts.Text;
			if (s != init_startupScripts) App.Model.StartupScriptsCsv = s;
		};

		static string _startupScripts_Validation(FrameworkElement fe) {
			//print.it("validating");
			string text = (fe as TextBox).Text; if (text.NE()) return null;
			try {
				var t = csvTable.parse(text);
				if (t.ColumnCount > 2) return "Too many commas in a line. If script name contains comma, enclose in \"\".";
				regexp rxDelay = null;
				foreach (var v in t.Rows) {
					var s0 = v[0];
					if (s0.Starts("//")) continue;
					if (App.Model.FindCodeFile(s0) == null) return "Script not found: " + s0;
					var delay = v.Length == 1 ? null : v[1];
					if (!delay.NE()) {
						rxDelay ??= new regexp(@"(?i)^\d+ *m?s$");
						if (!rxDelay.IsMatch(delay)) return "Delay must be like 2 s or 500 ms";
					}
				}
			}
			catch (FormatException ex) { return ex.Message; }
			return null;
		}
	}

	//void _Files() {
	//	var b = _Page("Files");
	//	b.End();

	//	b.Loaded += () => {

	//	};

	//	_b.OkApply += e => {

	//	};
	//}

	void _Font() {
		var b = _Page("Font", WBPanelType.Dock);

		b.Add(out KScintilla sciStyles).Width(150);
		sciStyles.ZInitBorder = true;
		sciStyles.Name = "styles";
		//note: not readonly. Eg users may want to paste and see any character in multiple fonts.

		b.StartGrid().Columns(-1).Margin(20);
		b.R.StartGrid();
		var pFont = b.Panel as Grid;
		b.R.Add("Font", out ComboBox fontName).Editable();
		b.R.Add("Size", out TextBox fontSize).Width(40).Align("L");
		b.End();
		b.R.StartGrid();
		var pColor = b.Panel as Grid;
		b.R.Add(out KColorPicker color);
		b.R.Add(out KCheckBox bold, "Bold");
		b.End();
		b.Row(-1);
		b.R.Add(out Button bInfo, "?").Align("r").Width(20);
		b.End();
		b.End();

		pColor.Visibility = Visibility.Collapsed;

		b.Loaded += () => {
			var styles = CiStyling.TStyles.Settings;

			//font

			List<string> fonts = new(), fontsMono = new(), fontsVar = new();
			using (var dc = new ScreenDC_()) {
				unsafe {
					api.EnumFontFamiliesEx(dc, default, (lf, tm, fontType, lParam) => {
						if (lf->lfFaceName[0] != '@') {
							var fn = new string(lf->lfFaceName);
							if ((lf->lfPitchAndFamily & 0xf0) == 48) fontsMono.Add(fn); else fontsVar.Add(fn); //FF_MODERN=48
						}
						return 1;
					}, default, 0);
				}
			}
			fontsMono.Sort();
			fontsVar.Sort();
			fonts.Add("[ Fixed-width fonts ]");
			fonts.AddRange(fontsMono);
			fonts.Add("");
			fonts.Add("[ Variable-width fonts ]");
			fonts.AddRange(fontsVar);
			fontName.ItemsSource = fonts;
			var selFont = styles.FontName;
			fontName.SelectedItem = selFont; if (fontName.SelectedItem == null) fontName.Text = selFont;
			fontSize.Text = styles.FontSize.ToS();

			//styles

			sciStyles.zSetMarginWidth(1, 0);
			styles.ToScintilla(sciStyles);
			bool ignoreColorEvents = false;
			int backColor = styles.BackgroundColor;
			var s = @"Font
Background
None
//Comment
""String"" 'c'
\r\n\t\0\\
1234567890
()[]{},;:
Operator
Keyword
Namespace
Type
Function
Variable
Constant
GotoLabel
#preprocessor
#if-excluded
XML doc text
/// <doc tag>
Line number";
			sciStyles.zText = s;
			int i = -3;
			foreach (var v in s.Segments(SegSep.Line)) {
				i++;
				if (i < 0) { //Font, Background

				} else {
					if (i == (int)CiStyling.EToken.countUserDefined) i = Sci.STYLE_LINENUMBER;
					//print.it(i, s[v.start..v.end]);
					sciStyles.Call(Sci.SCI_STARTSTYLING, v.start);
					sciStyles.Call(Sci.SCI_SETSTYLING, v.end - v.start, i);
				}
			}
			//when selected line changed
			int currentLine = -1;
			sciStyles.ZNotify += (KScintilla c, ref Sci.SCNotification n) => {
				switch (n.nmhdr.code) {
				case Sci.NOTIF.SCN_UPDATEUI:
					int line = c.zLineFromPos(false, c.zCurrentPos8);
					if (line != currentLine) {
						currentLine = line;
						int tok = _SciStylesLineToTok(line);
						if (tok == -2) { //Font
							pColor.Visibility = Visibility.Collapsed;
							pFont.Visibility = Visibility.Visible;
						} else {
							pFont.Visibility = Visibility.Collapsed;
							pColor.Visibility = Visibility.Visible;
							ignoreColorEvents = true;
							int col;
							if (tok == -1) {
								col = backColor;
								bold.Visibility = Visibility.Collapsed;
							} else {
								col = ColorInt.SwapRB(sciStyles.Call(Sci.SCI_STYLEGETFORE, tok));
								bold.IsChecked = 0 != sciStyles.Call(Sci.SCI_STYLEGETBOLD, tok);
								bold.Visibility = Visibility.Visible;
							}
							color.Color = col;
							ignoreColorEvents = false;
						}
					}
					break;
				}
			};
			//when values of style controls changed
			TextChangedEventHandler textChanged = (sender, _) => _ChangeFont(sender);
			fontName.AddHandler(TextBoxBase.TextChangedEvent, textChanged);
			fontSize.AddHandler(TextBoxBase.TextChangedEvent, textChanged);
			void _ChangeFont(object control = null) {
				var (fname, fsize) = _GetFont();
				for (int i = 0; i <= Sci.STYLE_LINENUMBER; i++) {
					if (control == fontName) sciStyles.zStyleFont(i, fname);
					else sciStyles.zStyleFontSize(i, fsize);
				}
			}
			(string name, int size) _GetFont() {
				var s = fontName.Text; if (s == "" || s.Starts("[ ")) s = "Consolas";
				return (s, fontSize.Text.ToInt());
			}
			bold.CheckChanged += (sender, _) => { if (!ignoreColorEvents) _UpdateSci(sender); };
			color.ColorChanged += col => { if (!ignoreColorEvents) _UpdateSci(); };
			void _UpdateSci(object control = null) {
				int tok = _SciStylesLineToTok(sciStyles.zLineFromPos(false, sciStyles.zCurrentPos8));
				int col = color.Color;
				if (tok >= 0) {
					if (control == bold) sciStyles.zStyleBold(tok, bold.IsChecked);
					else sciStyles.zStyleForeColor(tok, col);
				} else if (tok == -1) {
					backColor = col;
					for (int i = 0; i <= Sci.STYLE_DEFAULT; i++) sciStyles.zStyleBackColor(i, col);
				}
			}

			int _SciStylesLineToTok(int line) {
				line -= 2; if (line < 0) return line;
				int tok = line, nu = (int)CiStyling.EToken.countUserDefined;
				if (tok >= nu) tok = tok - nu + Sci.STYLE_LINENUMBER;
				return tok;
			}

			_b.OkApply += e => {
				var styles = new CiStyling.TStyles(sciStyles); //gets colors and bold
				var (fname, fsize) = _GetFont();
				styles.FontName = fname;
				styles.FontSize = fsize;

				if (styles != CiStyling.TStyles.Settings) {
					CiStyling.TStyles.Settings = styles;
					foreach (var v in Panels.Editor.ZOpenDocs) {
						styles.ToScintilla(v);
						v.SetLineNumberMarginWidth_();
					}
				}
			};

			//[?] button
			bInfo.Click += (_, _) => {
				string link = CiStyling.TStyles.s_settingsFile;
				dialog.show(null, $@"Changed font/color settings are saved in file
<a href=""{link}"">{link}</a>

To reset: delete the file.
To reset some colors etc: delete some lines.
To change all: replace the file.
To backup: copy the file.

To apply changes after deleting etc, restart this application.
", icon: DIcon.Info, onLinkClick: e => { run.selectInExplorer(e.LinkHref); });
			};
		};
	}

	void _Templates() {
		var b = _Page("Templates").Columns(0, 100, -1, 0, 100);
		b.R.Add("Template", out ComboBox template).Items("Script|Class")
			.Skip().Add("Use", out ComboBox use).Items("Default|Custom");
		b.Row(-1).Add(out KSciCodeBoxWnd sci); sci.ZInitBorder = true;
		b.R.Add(out KCheckBox fold, "Fold script").Checked(0 == (1 & App.Settings.templ_flags));
		b.End();

		string[] customText = new string[2];
		var useCustom = (FileNode.ETempl)App.Settings.templ_use;

		template.SelectionChanged += _Combo_Changed;
		use.SelectionChanged += _Combo_Changed;
		sci.ZTextChanged += (_, _) => customText[template.SelectedIndex] = sci.zText;
		b.Loaded += () => {
			_Combo_Changed(template, null);
		};

		_b.OkApply += e => {
			for (int i = 0; i < customText.Length; i++) {
				string text = customText[i]; if (text == null) continue;
				var tt = (FileNode.ETempl)(1 << i);
				var file = FileNode.Templates.FilePathRaw(tt, true);
				try {
					if (text == FileNode.Templates.Load(tt, false)) {
						filesystem.delete(file);
					} else {
						filesystem.saveText(file, text);
					}
				}
				catch (Exception ex) { print.it(ex.ToStringWithoutStack()); }
			}
			App.Settings.templ_use = (int)useCustom;

			int flags = App.Settings.templ_flags;
			if (fold.IsChecked) flags &= ~1; else flags |= 1;
			App.Settings.templ_flags = flags;
		};

		void _Combo_Changed(object sender, SelectionChangedEventArgs e) {
			int i = template.SelectedIndex;
			FileNode.ETempl tt = i switch { 1 => FileNode.ETempl.Class, _ => FileNode.ETempl.Script, };
			if (sender == template) use.SelectedIndex = useCustom.Has(tt) ? 1 : 0;
			bool custom = use.SelectedIndex > 0;
			string text = null;
			if (e != null) {
				useCustom.SetFlag(tt, custom);
				if (custom) text = customText[i];
			}
			text ??= FileNode.Templates.Load(tt, custom);
			sci.ZSetText(text, readonlyFrom: custom ? -1 : 0);
		}
	}

	void _Code() {
		var b = _Page("Code", WBPanelType.VerticalStack);
		b.StartGrid<GroupBox>("Completion list").Columns(300, 20, -1);
		b.R.StartGrid(); //left
		b.R.Add(out ComboBox complParen).Items("Spacebar|Always|Never").Select(App.Settings.ci_complParen).Add<Label>("adds ( )");
		b.R.AddButton("Snippets ▾", _SnippetsButton).Width(70).Align("L");
		b.End();
		b.Skip().StartGrid(); //right
		b.End();
		b.End();
		b.StartGrid<GroupBox>("Insert code");
		b.R.Add(out KCheckBox unexpandPath, "Unexpand path").Checked(App.Settings.ci_unexpandPath).Tooltip("Insert file path like folders.System + \"file.exe\"");
		b.End();
		//b.StartGrid<GroupBox>("Auto correction").Columns(0, 100, -1);
		////b.R.StartStack().Add<TextBlock>("Need Shift to exit (...) with").Add(out KCheckBox shiftEnter, "Enter").Margin("T4").Add(out KCheckBox shiftTab, "Tab").Margin("T4").End(); //rejected
		////b.R.Add(@"Break ""string""", out ComboBox breakString).Items(@"""abc"" + """"|""abc\r\n"" + """"|@""multiline""").Span(1); //rejected. Rarely used.

		//b.End();
		//b.StartGrid<GroupBox>("");
		//b.End();
		b.End();

		//b.Loaded += () => {

		//};

		_b.OkApply += e => {
			App.Settings.ci_complParen = complParen.SelectedIndex;
			App.Settings.ci_unexpandPath = unexpandPath.IsChecked;
			//App.Settings.ci_shiftEnterAlways = (byte)(shiftEnter.IsChecked ? 0 : 1);
			//App.Settings.ci_shiftTabAlways = (byte)(shiftTab.IsChecked ? 0 : 1);
			//App.Settings.ci_breakString = (byte)breakString.SelectedIndex;
		};

		static void _SnippetsButton(WBButtonClickArgs o) {
			switch (popupMenu.showSimple("1 Edit snippets|2 Find default snippets")) {
			case 1: run.selectInExplorer(folders.ThisAppDocuments + @".settings\Snippets.xml"); break;
			case 2: run.selectInExplorer(folders.ThisApp + @"Default\Snippets.xml"); break;
			}
		}
	}

	void _Hotkeys() {
		var b = _Page("Hotkeys");
		b.R.Add("Capture wnd and show menu", out TextBox captureMenu, App.Settings.hotkeys.tool_quick).xValidateHotkey();
		b.R.Add("Capture wnd and show tool", out TextBox captureDwnd, App.Settings.hotkeys.tool_wnd).xValidateHotkey();
		b.R.Add("Capture elm and show tool", out TextBox captureDelm, App.Settings.hotkeys.tool_elm).xValidateHotkey();
		b.End();

		_b.OkApply += e => {
			AppSettings.hotkeys_t v = new() {
				tool_quick = captureMenu.Text,
				tool_wnd = captureDwnd.Text,
				tool_elm = captureDelm.Text,
			};
			if (v != App.Settings.hotkeys) {
				App.Settings.hotkeys = v;
				QuickCapture.UnregisterHotkeys();
				QuickCapture.RegisterHotkeys();
			}
		};
	}

	unsafe void _OS() {
		var b = _Page("OS");
		b.R.Add("Key/mouse hook timeout, ms", out TextBox hooksTimeout, WindowsHook.LowLevelHooksTimeout.ToS()).Validation(o => ((o as TextBox).Text.ToInt() is >= 300 and <= 1000) ? null : "300-1000");
		bool disableLAW = 0 == Api.SystemParametersInfo(Api.SPI_GETFOREGROUNDLOCKTIMEOUT, 0);
		b.R.Add(out KCheckBox cDisableLAW, "Disable \"lock active window\"").Checked(disableLAW);
		b.End();

		_b.OkApply += e => {
			int t = hooksTimeout.Text.ToInt();
			if (t != WindowsHook.LowLevelHooksTimeout) {
				WindowsHook.LowLevelHooksTimeout = t;
				print.it("Info: The new hook timeout value will be used after restarting Windows.");
			}

			if (cDisableLAW.IsChecked != disableLAW)
				Api.SystemParametersInfo(Api.SPI_SETFOREGROUNDLOCKTIMEOUT, 0, (void*)(disableLAW ? 15000 : 0), save: true, notify: true);
		};
	}

	static class api {
		[DllImport("gdi32.dll", EntryPoint = "EnumFontFamiliesExW")]
		internal static extern int EnumFontFamiliesEx(IntPtr hdc, in Api.LOGFONT lpLogfont, FONTENUMPROC lpProc, nint lParam, uint dwFlags);
		internal unsafe delegate int FONTENUMPROC(Api.LOGFONT* lf, IntPtr tm, uint fontType, nint lParam);

	}
}
