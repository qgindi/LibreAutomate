using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using Microsoft.Win32;
using System.Windows.Controls.Primitives;
using Au.Tools;
using System.Windows.Media;
using System.Windows.Documents;
using EStyle = CiStyling.EStyle;

class DOptions : KDialogWindow {
	public static void AaShow() {
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
		_b.R.AddOkCancel(out var bOK, out var bCancel, out _, apply: "_Apply");
		bOK.IsDefault = false; bCancel.IsCancel = false;
		
		_General();
		//_Files();
		_FontAndColors();
		_CodeEditor();
		_Templates();
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
		b.Add(out KCheckBox startWithWin, "Start with Windows"); //note: must be the first checkbox in Options, and don't change text, because used for the forum registration security question
		b.Add(out KCheckBox startHidden, "Start hidden; hide when closing");
		b.End();
		//right column
		b.StartStack(vertical: true);
		b.Add("Run scripts when this workspace loaded", out TextBox startupScripts).Multiline(110, TextWrapping.NoWrap)
			.Tooltip("Example:\nScript1.cs\n\\Folder\\Script2.cs\n//Disabled.cs\nDelay1.cs, 3s\nDelay2.cs, 300ms\n\"Comma, comma.csv\"")
			.Validation(_startupScripts_Validation);
		b.Add("Debugger script for script.debug", out TextBox debuggerScript, App.Model.DebuggerScript)
			.Tooltip("The script can automate attaching a debugger to the script process. args[0] is process id. Example in Cookbook.")
			.Validation(_ => debuggerScript.Text is string s && !s.NE() && null == App.Model.FindCodeFile(s) ? "Debugger script not found" : null);
		b.End();
		b.End();
		
		//b.Loaded += () => {
		
		//};
		const string c_rkRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
		bool init_startWithWin = Registry.GetValue(@"HKEY_CURRENT_USER\" + c_rkRun, "Au.Editor", null) is string s1 && filesystem.more.isSameFile(s1.Trim('\"'), process.thisExePath);
		startWithWin.IsChecked = init_startWithWin;
		if (App.IsPortable) startWithWin.Checked += (_, _) => dialog.showWarning("Portable mode warning", "This setting will be saved in the Registry. Portable apps should not do it.", owner: this);
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
			
			App.Model.DebuggerScript = debuggerScript.Text.NullIfEmpty_();
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
	
	void _FontAndColors() {
		var b = _Page("Font, colors", WBPanelType.Dock);
		b.Options(bindLabelVisibility: true);
		
		b.Add(out KScintilla sciStyles).Width(150);
		sciStyles.AaInitBorder = true;
		sciStyles.Name = "styles";
		//note: not readonly. Eg users may want to paste and see any character in multiple fonts.
		
		b.StartGrid().Columns(-1).Margin(20);
		b.R.StartGrid();
		var pFont = b.Panel as Grid;
		b.R.Add("Font", out ComboBox fontName).Editable();
		b.R.Add("Size", out TextBox fontSize).Width(40, "L");
		b.End();
		b.R.StartGrid();
		var pColor = b.Panel as Grid;
		b.R.Add(out KColorPicker colorPicker);
		b.R.Add(out KCheckBox cBold, "Bold");
		b.R.Add(out Label lAlpha, "Opacity 0-255", out TextBox tAlpha).Width(50, "L");
		b.End();
		b.Row(-1);
		b.R.AddSeparator();
		b.R.StartStack();
		//b.Add(out Button bInvert, "Invert");
		b.Add(out Button bInfo, "?").Width(20);
		b.End().Align("r");
		b.End();
		b.End();
		
		pColor.Visibility = Visibility.Collapsed;
		
		b.Loaded += () => {
			sciStyles.Call(Sci.SCI_SETCARETLINEFRAME, 1);
			sciStyles.aaaSetElementColor(Sci.SC_ELEMENT_CARET_LINE_BACK, 0xE0E0E0);
			sciStyles.Call(Sci.SCI_SETCARETLINEVISIBLEALWAYS, 1);
			
			var styles = CiStyling.TStyles.Settings;
			
			//font
			
			List<string> fonts = new(), fontsMono = new(), fontsVar = new();
			using (var dc = new ScreenDC_()) {
				unsafe {
					_Api.EnumFontFamiliesEx(dc, default, (lf, tm, fontType, lParam) => {
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
			
			const int indicHidden = 0;
			sciStyles.aaaIndicatorDefine(indicHidden, Sci.INDIC_HIDDEN);
			sciStyles.aaaMarginSetWidth(1, 0);
			styles.ToScintilla(sciStyles);
			
			bool ignoreColorEvents = false;
			int backColor = styles.BackgroundColor;
			
			var table = new _TableItem[] {
				new("Font", _StyleKind.Font, 0),
				
				new("Background", _StyleKind.Background, 0),
				
				new("Text", _StyleKind.Style, (int)EStyle.None),
				new("//Comment", _StyleKind.Style, (int)EStyle.Comment),
				new(@"""String"" 'c'", _StyleKind.Style, (int)EStyle.String),
				new(@"\r\n\t\0\\", _StyleKind.Style, (int)EStyle.StringEscape),
				new("1234567890", _StyleKind.Style, (int)EStyle.Number),
				new("()[]{},;:", _StyleKind.Style, (int)EStyle.Punctuation),
				new("Operator", _StyleKind.Style, (int)EStyle.Operator),
				new("Keyword", _StyleKind.Style, (int)EStyle.Keyword),
				new("Namespace", _StyleKind.Style, (int)EStyle.Namespace),
				new("Type", _StyleKind.Style, (int)EStyle.Type),
				new("Function", _StyleKind.Style, (int)EStyle.Function),
				new("Variable", _StyleKind.Style, (int)EStyle.Variable),
				new("Constant", _StyleKind.Style, (int)EStyle.Constant),
				new("GotoLabel", _StyleKind.Style, (int)EStyle.Label),
				new("#preprocessor", _StyleKind.Style, (int)EStyle.Preprocessor),
				new("#if-disabled", _StyleKind.Style, (int)EStyle.Excluded),
				new("/// doc text", _StyleKind.Style, (int)EStyle.XmlDocText),
				new("/// <doc tag>", _StyleKind.Style, (int)EStyle.XmlDocTag),
				new("Line number", _StyleKind.Style, (int)EStyle.LineNumber),
				
				new("Text highlight", _StyleKind.Indicator, SciCode.c_indicFound),
				new("Symbol highlight", _StyleKind.Indicator, SciCode.c_indicRefs),
				new("Brace highlight", _StyleKind.Indicator, SciCode.c_indicBraces),
				
				new("Selection", _StyleKind.Element, Sci.SC_ELEMENT_SELECTION_BACK),
				new("Sel. no focus", _StyleKind.Element, Sci.SC_ELEMENT_SELECTION_INACTIVE_BACK),
			};
			
			sciStyles.aaaText = string.Join("\r\n", table.Select(o => o.name));
			for (int i = 0; i < table.Length; i++) {
				int lineStart = sciStyles.aaaLineStart(false, i), lineEnd = sciStyles.aaaLineEnd(false, i);
				sciStyles.aaaIndicatorAdd(indicHidden, false, lineStart..lineEnd, i + 1);
				if (table[i].kind == _StyleKind.Style) {
					sciStyles.Call(Sci.SCI_STARTSTYLING, lineStart);
					sciStyles.Call(Sci.SCI_SETSTYLING, lineEnd - lineStart, table[i].index);
				} else if (table[i].kind == _StyleKind.Indicator) {
					sciStyles.aaaIndicatorAdd(table[i].index, false, lineStart..lineEnd);
				}
			}
			
			//when changed current line
			int currentItem = 0;
			sciStyles.AaNotify += (KScintilla c, ref Sci.SCNotification n) => {
				switch (n.code) {
				case Sci.NOTIF.SCN_UPDATEUI:
					int i = sciStyles.aaaIndicGetValue(indicHidden, sciStyles.aaaLineStartFromPos(false, sciStyles.aaaCurrentPos8)) - 1;
					if (i != currentItem && i >= 0) {
						currentItem = i;
						var k = table[i];
						if (k.kind == _StyleKind.Font) {
							pColor.Visibility = Visibility.Collapsed;
							pFont.Visibility = Visibility.Visible;
						} else {
							pFont.Visibility = Visibility.Collapsed;
							pColor.Visibility = Visibility.Visible;
							ignoreColorEvents = true;
							int col;
							cBold.Visibility = k.kind == _StyleKind.Style ? Visibility.Visible : Visibility.Collapsed;
							tAlpha.Visibility = k.kind == _StyleKind.Style ? Visibility.Collapsed : Visibility.Visible;
							if (k.kind == _StyleKind.Style) {
								col = ColorInt.SwapRB(sciStyles.Call(Sci.SCI_STYLEGETFORE, k.index));
								cBold.IsChecked = 0 != sciStyles.Call(Sci.SCI_STYLEGETBOLD, k.index);
							} else if (k.kind == _StyleKind.Indicator) {
								col = ColorInt.SwapRB(sciStyles.Call(Sci.SCI_INDICGETFORE, k.index));
								tAlpha.Text = sciStyles.Call(Sci.SCI_INDICGETALPHA, k.index).ToS();
							} else if (k.kind == _StyleKind.Element) {
								col = sciStyles.aaaGetElementColor(k.index).argb;
								tAlpha.Text = (col >>> 24).ToS();
								col &= 0xFFFFFF;
							} else { //Background
								col = backColor;
							}
							colorPicker.Color = col;
							ignoreColorEvents = false;
						}
					}
					break;
				}
			};
			
			//when changed values of controls
			TextChangedEventHandler textChanged = (sender, _) => _ChangeFont(sender);
			fontName.AddHandler(TextBoxBase.TextChangedEvent, textChanged);
			fontSize.AddHandler(TextBoxBase.TextChangedEvent, textChanged);
			void _ChangeFont(object control = null) {
				var (fname, fsize) = _GetFont();
				for (int i = 0; i <= Sci.STYLE_LINENUMBER; i++) {
					if (control == fontName) sciStyles.aaaStyleFont(i, fname);
					else sciStyles.aaaStyleFontSize(i, fsize);
				}
			}
			(string name, int size) _GetFont() {
				var s = fontName.Text; if (s == "" || s.Starts("[ ")) s = "Consolas";
				return (s, fontSize.Text.ToInt());
			}
			
			colorPicker.ColorChanged += _ => _UpdateSci();
			cBold.CheckChanged += (sender, _) => _UpdateSci(sender);
			tAlpha.TextChanged += (sender, _) => _UpdateSci(sender);
			
			void _UpdateSci(object control = null) {
				if (ignoreColorEvents || currentItem < 0) return;
				var k = table[currentItem];
				int col = colorPicker.Color;
				if (k.kind == _StyleKind.Style) {
					if (control == cBold) sciStyles.aaaStyleBold(k.index, cBold.IsChecked);
					else sciStyles.aaaStyleForeColor(k.index, col);
				} else if (k.kind == _StyleKind.Indicator) {
					if (control == tAlpha) sciStyles.Call(Sci.SCI_INDICSETALPHA, k.index, Math.Clamp(tAlpha.Text.ToInt(), 0, 255));
					else sciStyles.Call(Sci.SCI_INDICSETFORE, k.index, ColorInt.SwapRB(col));
				} else if (k.kind == _StyleKind.Element) {
					int m = sciStyles.aaaGetElementColor(k.index).argb;
					if (control == tAlpha) m = (m & 0xFFFFFF) | Math.Clamp(tAlpha.Text.ToInt(), 0, 255) << 24;
					else m = (m & ~0xFFFFFF) | (col & 0xFFFFFF);
					sciStyles.aaaSetElementColor(k.index, m);
				} else if (k.kind == _StyleKind.Background) {
					backColor = col;
					for (int i = 0; i <= Sci.STYLE_DEFAULT; i++) sciStyles.aaaStyleBackColor(i, col);
				}
			}
			
			_b.OkApply += e => {
				var styles = new CiStyling.TStyles(sciStyles); //gets colors, bold, indicators
				var (fname, fsize) = _GetFont();
				styles.FontName = fname;
				styles.FontSize = fsize;
				
				if (styles != CiStyling.TStyles.Settings) {
					CiStyling.TStyles.Settings = styles;
					foreach (var v in Panels.Editor.OpenDocs) {
						styles.ToScintilla(v);
						v.ESetLineNumberMarginWidth_();
					}
				}
			};
			
			//rejected. This code is unfinished, just to test. Inverted colors aren't good.
			//bInvert.Click += (_, _) => {
			//	styles.InvertAllColors();
			//	styles.ToScintilla(sciStyles);
			//	inverted = true;
			//};
			
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
	
	enum _StyleKind { Style, Indicator, Element, Font, Background }
	
	record struct _TableItem(string name, _StyleKind kind, int index);
	
	void _CodeEditor() {
		var b = _Page("Code editor").Columns(200, 20, -1);
		b.R.StartStack(vertical: true); //left
		
		b.StartGrid<GroupBox>("Completion list");
		b.R.Add(out CheckBox complParen, "Append ( )").Checked(App.Settings.ci_complParen switch { 1 => true, 2 => false, _ => null }, threeState: true)
			.Tooltip("Append () when selected a method or a keyword like 'if'.\nChecked - always; unchecked - never; else only when selected with the spacebar key.");
		b.End();
		
		b.StartGrid<GroupBox>("Formatting");
		b.R.Add(out KCheckBox formatCompact, "Compact").Checked(App.Settings.ci_formatCompact)
			.Tooltip("""
Brace in same line. Don't indent case in switch.
Examples:

void Checked() {
    if (true) {

    } else {

    }
}

void Unchecked()
{
    if (true)
    {

    }
    else
    {

    }
}
""");
		b.R.Add(out KCheckBox formatTabIndent, "Tab-indent").Checked(App.Settings.ci_formatTabIndent)
			.Tooltip("For indentation use tab character, not spaces");
		b.End();
		
		//b.StartGrid<GroupBox>("Insert code");
		//b.R.Add(out KCheckBox unexpandPath, "Unexpand path").Checked(App.Settings.ci_unexpandPath)
		//	.Tooltip("Insert file path like folders.System + \"file.exe\"");
		//b.End();
		
		b.End(); //left
		
		b.Skip().StartStack(vertical: true); //right
		
		b.StartGrid<GroupBox>("Find references/implemetations, rename");
		b.R.Add("Skip folders", out TextBox skipFolders, App.Model.WSSett.ci_skipFolders).Multiline(55, wrap: TextWrapping.NoWrap)
			.Tooltip(@"Don't search in these folders.
Example:
\Garbage
\Folder1\Folder2");
		b.End();
		
		b.End(); //right
		
		b.End();
		
		//b.Loaded += () => {
		
		//};
		
		_b.OkApply += e => {
			App.Settings.ci_complParen = complParen.IsChecked switch { true => 1, false => 2, _ => 0 };
			
			if (formatCompact.IsChecked != App.Settings.ci_formatCompact || formatTabIndent.IsChecked != App.Settings.ci_formatTabIndent) {
				App.Settings.ci_formatCompact = formatCompact.IsChecked;
				App.Settings.ci_formatTabIndent = formatTabIndent.IsChecked;
				ModifyCode.FormattingOptions = null; //recreate
				
				//note: don't SCI_SETUSETABS(false).
				//	Eg VS does not use it, and it's good; VSCode uses, and it's bad, eg cannot insert tab in raw strings.
				//	All autocorrect/autoindent/format code inserts spaces if need. Users rarely have to type indentation tabs.
				//	And don't add options to set tab/indentation size. Too many options isn't good.
			}
			
			//App.Settings.ci_unexpandPath = unexpandPath.IsChecked;
			//App.Settings.ci_shiftEnterAlways = (byte)(shiftEnter.IsChecked ? 0 : 1);
			//App.Settings.ci_shiftTabAlways = (byte)(shiftTab.IsChecked ? 0 : 1);
			//App.Settings.ci_breakString = (byte)breakString.SelectedIndex;
			
			App.Model.WSSett.ci_skipFolders = skipFolders.Text.NullIfEmpty_();
		};
		
		//CONSIDER: completion list: option to single-click.
	}
	
	void _Templates() {
		var b = _Page("Templates").Columns(0, 100, -1, 0, 100);
		b.R.Add("Template", out ComboBox template).Items("Script|Class")
			.Skip().Add("Use", out ComboBox use).Items("Default|Custom");
		b.Row(-1).Add(out KSciCodeBoxWnd sci); sci.AaInitBorder = true;
		//b.R.Add(out KCheckBox fold, "Fold script").Checked(0 == (1 & App.Settings.templ_flags));
		b.End();
		
		string[] customText = new string[2];
		var useCustom = (FileNode.ETempl)App.Settings.templ_use;
		
		template.SelectionChanged += _Combo_Changed;
		use.SelectionChanged += _Combo_Changed;
		sci.AaTextChanged += (_, _) => customText[template.SelectedIndex] = sci.aaaText;
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
			
			//int flags = App.Settings.templ_flags;
			//if (fold.IsChecked) flags &= ~1; else flags |= 1;
			//App.Settings.templ_flags = flags;
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
			sci.AaSetText(text, readonlyFrom: custom ? -1 : 0);
		}
	}
	
	void _Hotkeys() {
		var b = _Page("Hotkeys");
		b.R.Add("Capture wnd and show menu", out TextBox captureMenu, App.Settings.hotkeys.tool_quick).xValidateHotkey();
		b.R.Add("Capture wnd and show tool", out TextBox captureDwnd, App.Settings.hotkeys.tool_wnd).xValidateHotkey();
		b.R.Add("Capture elm and show tool", out TextBox captureDelm, App.Settings.hotkeys.tool_elm).xValidateHotkey();
		b.R.Add("Capture image and show tool", out TextBox captureDuiimage, App.Settings.hotkeys.tool_uiimage).xValidateHotkey();
		b.End();
		
		_b.OkApply += e => {
			AppSettings.hotkeys_t v = new() {
				tool_quick = captureMenu.Text,
				tool_wnd = captureDwnd.Text,
				tool_elm = captureDelm.Text,
				tool_uiimage = captureDuiimage.Text,
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
		b.R.Add<TextBlock>("Some Windows settings for all programs");
		if (App.IsPortable) b.R.Add<TextBlock>().Text(new Run("Portable mode warning: portable apps should not change Windows settings.") { Foreground = Brushes.Red });
		b.R.AddSeparator().Margin("T8B8");
		
		b.R.Add("Key/mouse hook timeout, ms", out TextBox hooksTimeout, WindowsHook.LowLevelHooksTimeout.ToS()).Width(70, "L")
			.Validation(o => ((o as TextBox).Text.ToInt() is >= 300 and <= 1000) ? null : "300-1000");
		bool disableLAW = 0 == Api.SystemParametersInfo(Api.SPI_GETFOREGROUNDLOCKTIMEOUT, 0);
		b.R.Add(out KCheckBox cDisableLAW, "Disable \"lock active window\"").Checked(disableLAW);
		bool underlineAK = Api.SystemParametersInfo(Api.SPI_GETKEYBOARDCUES);
		b.R.Add(out KCheckBox cUnderlineAK, "Underline menu/dialog item access keys").Checked(underlineAK);
		b.R.AddButton("Java...", _ => Delm.Java.EnableDisableJabUI(this)).Width(70, "L").Disabled(!Delm.Java.GetJavaPath(out _));
		b.End();
		
		_b.OkApply += e => {
			int t = hooksTimeout.Text.ToInt();
			if (t != WindowsHook.LowLevelHooksTimeout) {
				WindowsHook.LowLevelHooksTimeout = t;
				print.it("Info: The new hook timeout value will be used after restarting Windows.");
			}
			
			if (cDisableLAW.IsChecked != disableLAW)
				Api.SystemParametersInfo(Api.SPI_SETFOREGROUNDLOCKTIMEOUT, 0, (void*)(disableLAW ? 15000 : 0), save: true, notify: true);
			if (cUnderlineAK.IsChecked != underlineAK)
				Api.SystemParametersInfo(Api.SPI_SETKEYBOARDCUES, 0, (void*)(underlineAK ? 0 : 1), save: true, notify: true);
		};
	}
	
	static class _Api {
		[DllImport("gdi32.dll", EntryPoint = "EnumFontFamiliesExW")]
		internal static extern int EnumFontFamiliesEx(IntPtr hdc, in Api.LOGFONT lpLogfont, FONTENUMPROC lpProc, nint lParam, uint dwFlags);
		internal unsafe delegate int FONTENUMPROC(Api.LOGFONT* lf, IntPtr tm, uint fontType, nint lParam);
		
	}
}
