using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using Microsoft.Win32;
using System.Windows.Controls.Primitives;
using Au.Tools;
using System.Windows.Media;
using System.Windows.Documents;
using EStyle = CiStyling.EStyle;
using System.Windows.Input;

class DOptions : KDialogWindow {
	public static void AaShow() {
		ShowSingle(() => new DOptions());
	}
	
	wpfBuilder _b;
	TabControl _tc;
	
	DOptions() {
		InitWinProp("Options", App.Wmain);
		
		_b = new wpfBuilder(this).WinSize(600);
		_b.Row(-1).Add(out _tc).Height(300..);
		_b.R.AddOkCancel(out var bOK, out var bCancel, out _, apply: "_Apply");
		bOK.IsDefault = false; bCancel.IsCancel = false;
		
		_Program();
		_Workspace();
		_FontAndColors();
		_CodeEditor();
		_Templates();
		_Hotkeys();
		_Other();
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
	
	void _Program() {
		var b = _Page("Program").Columns(-1, 20, -1);
		
		//left column
		b.StartGrid().Columns(-1, 0);
		b.R.Add(out KCheckBox startWithWin, "Start with Windows"); //note: must be the first checkbox in Options, and don't change text, because used for the forum registration security question
		b.R.Add(out KCheckBox startHidden, "Start hidden; hide when closing").Checked(App.Settings.runHidden);
		b.R.Add(out KCheckBox checkForUpdates, "Check for updates every day").Checked(App.Settings.checkForUpdates)
			.AddButton("Now", o => App.CheckForUpdates(o.Button));
		b.End();
		
		//right column
		b.Skip().StartStack(vertical: true);
		b.End();
		
		b.End();
		
		//b.Loaded += () => {
		
		//};
		const string c_rkRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
		bool init_startWithWin = Registry.GetValue(@"HKEY_CURRENT_USER\" + c_rkRun, "Au.Editor", null) is string s1 && filesystem.more.isSameFile(s1.Trim('"'), process.thisExePath);
		startWithWin.IsChecked = init_startWithWin;
		if (App.IsPortable) startWithWin.Checked += (_, _) => dialog.showWarning("Portable mode warning", "This setting will be saved in the Registry. Portable apps should not do it.", owner: this);
		
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
			App.Settings.checkForUpdates = checkForUpdates.IsChecked;
		};
	}
	
	void _Workspace() {
		var b = _Page("Workspace").Columns(-1, 20, -1);
		b.R.xAddInfoBlockT("Settings of current workspace");
		
		//left column
		b.R.StartStack(vertical: true);
		b.Add("Run scripts when workspace loaded", out TextBox startupScripts, App.Model.UserSettings.startupScripts).Multiline(110, TextWrapping.NoWrap)
			.Tooltip("Example:\nScript1.cs\n\\Folder\\Script2.cs\n//Disabled.cs\nDelay1.cs, 3s\nDelay2.cs, 300ms\n\"Comma, comma.csv\"")
			.Validation(_startupScripts_Validation);
		b.End();
		
		//right column
		b.Skip().StartStack(vertical: true);
		b.Add(out KCheckBox cBackup, "Auto-backup (Git commit)").Checked(App.Model.UserSettings.gitBackup).Margin("T6")
			.Tooltip("Silently run Git commit when LibreAutomate is visible the first time after loading this workspace or activated later after several hours from the last backup.\nIt creates a local backup of workspace files (scripts etc). To upload etc, you can use menu File > Git.");
		cBackup.Checked += (_, _) => { if (!Git.IsReady) App.Dispatcher.InvokeAsync(Git.Setup); };
		b.End();
		
		b.End();
		
		//b.Loaded += () => {
		
		//};
		
		_b.OkApply += e => {
			App.Model.UserSettings.startupScripts = startupScripts.Text.Trim().NullIfEmpty_();
			App.Model.UserSettings.gitBackup = cBackup.IsChecked;
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
	
	class _FontControls {
		public ComboBox name;
		public TextBox size;
		
		public void Init(List<string> fonts, string fontName, double fontSize) {
			name.ItemsSource = fonts;
			name.SelectedItem = fontName; if (name.SelectedItem == null) name.Text = fontName;
			
			size.Text = fontSize.ToS("0.##");
			size.MouseWheel += static (o, e) => {
				var tb = o as TextBox;
				if (!tb.Text.ToNumber(out double d)) d = 9;
				tb.Text = Math.Clamp(d + e.Delta / 160d, 6, 30).ToS("0.##"); //120/160d=0.75
			};
		}
		
		public void Init(List<string> fonts, AppSettings.font_t f) => Init(fonts, f.name, f.size);
		
		public (string name, double size) Get() {
			var s = name.Text.Trim(); if (s == "" || s.Starts("[ ")) s = "Consolas";
			return (s, size.Text.ToNumber());
		}
		
		public bool Apply(ref AppSettings.font_t f) {
			var s = name.Text.Trim(); var d = size.Text.ToNumber();
			if (s == f.name && d == f.size) return false;
			f.name = s;
			f.size = d;
			f.Normalize();
			return true;
		}
	}
	
	void _FontAndColors() {
		//CONSIDER: make easier to set dark theme for code.
		
		var b = _Page("Font, colors", WBPanelType.Dock);
		b.Options(bindLabelVisibility: true);
		
		b.Add(out KScintilla sciStyles).Width(150);
		sciStyles.AaInitBorder = true;
		sciStyles.Name = "styles";
		//note: not readonly. Eg users may want to paste and see any character in multiple fonts.
		
		b.StartGrid().Columns(-1).Margin(20);
		b.R.StartGrid<KGroupBox>(out var gFont, "Font");
		_FontControls _AddFontControls(string label) {
			b.R.Add(label, out ComboBox name).Editable().And(40).Add(out TextBox size).Tooltip("Font size.\nUse mouse wheel to select.");
			return new() { name = name, size = size };
		}
		var font = _AddFontControls("Editor");
		var fontOutput = _AddFontControls("Output");
		var fontRecipeText = _AddFontControls("Recipe text");
		var fontRecipeCode = _AddFontControls("Recipe code");
		var fontFind = _AddFontControls("Find");
		b.End();
		b.R.StartGrid();
		var pColor = b.Panel as Grid;
		b.R.Add(out KColorPicker colorPicker);
		b.R.StartStack();
		var pFontStyle = b.Panel;
		b.Add(out KCheckBox cBold, "Bold");
		b.Add(out KCheckBox cItalic, "Italic");
		b.Add(out KCheckBox cUnderline, "Underline");
		b.End();
		b.R.Add("Opacity 0-255", out TextBox tAlpha).Width(50, "L");
		b.End();
		b.Row(-1);
		b.R.AddSeparator();
		b.R.StartStack();
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
			
			font.Init(fonts, styles.FontName, styles.FontSize);
			fontOutput.Init(fonts, App.Settings.font_output);
			fontRecipeText.Init(fonts, App.Settings.font_recipeText);
			fontRecipeCode.Init(fonts, App.Settings.font_recipeCode);
			fontFind.Init(fonts, App.Settings.font_find);
			
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
				new("Debug highlight", _StyleKind.Indicator, SciCode.c_indicDebug),

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
							gFont.Visibility = Visibility.Visible;
						} else {
							gFont.Visibility = Visibility.Collapsed;
							pColor.Visibility = Visibility.Visible;
							ignoreColorEvents = true;
							int col;
							pFontStyle.Visibility = k.kind == _StyleKind.Style ? Visibility.Visible : Visibility.Collapsed;
							tAlpha.Visibility = k.kind is _StyleKind.Element or _StyleKind.Indicator ? Visibility.Visible : Visibility.Collapsed;
							if (k.kind == _StyleKind.Style) {
								col = ColorInt.SwapRB(sciStyles.Call(Sci.SCI_STYLEGETFORE, k.index));
								cBold.IsChecked = 0 != sciStyles.Call(Sci.SCI_STYLEGETBOLD, k.index);
								cItalic.IsChecked = 0 != sciStyles.Call(Sci.SCI_STYLEGETITALIC, k.index);
								cUnderline.IsChecked = 0 != sciStyles.Call(Sci.SCI_STYLEGETUNDERLINE, k.index);
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
			TextChangedEventHandler textChanged = (_, _) => {
				var (fname, fsize) = font.Get();
				for (int i = 0; i <= Sci.STYLE_LINENUMBER; i++) sciStyles.aaaStyleFont(i, fname, fsize);
			};
			font.name.AddHandler(TextBoxBase.TextChangedEvent, textChanged);
			font.size.AddHandler(TextBoxBase.TextChangedEvent, textChanged);
			
			colorPicker.ColorChanged += _ => _UpdateSci();
			cBold.CheckChanged += (sender, _) => _UpdateSci(sender);
			cItalic.CheckChanged += (sender, _) => _UpdateSci(sender);
			cUnderline.CheckChanged += (sender, _) => _UpdateSci(sender);
			tAlpha.TextChanged += (sender, _) => _UpdateSci(sender);
			
			void _UpdateSci(object control = null) {
				if (ignoreColorEvents || currentItem < 0) return;
				var k = table[currentItem];
				int col = colorPicker.Color;
				if (k.kind == _StyleKind.Style) {
					if (control == cBold) sciStyles.aaaStyleBold(k.index, cBold.IsChecked);
					else if (control == cItalic) sciStyles.aaaStyleItalic(k.index, cItalic.IsChecked);
					else if (control == cUnderline) sciStyles.aaaStyleUnderline(k.index, cUnderline.IsChecked);
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
				(styles.FontName, styles.FontSize) = font.Get();
				
				if (styles != CiStyling.TStyles.Settings) {
					CiStyling.TStyles.Settings = styles;
					foreach (var v in Panels.Editor.OpenDocs) {
						styles.ToScintilla(v);
						v.ESetLineNumberMarginWidth_();
					}
				}
				
				if (fontOutput.Apply(ref App.Settings.font_output)) Panels.Output.Scintilla.AaSetStyles();
				if (fontRecipeText.Apply(ref App.Settings.font_recipeText) | fontRecipeCode.Apply(ref App.Settings.font_recipeCode)) Panels.Recipe.Scintilla.AaSetStyles();
				if (fontFind.Apply(ref App.Settings.font_find)) Panels.Find.SetFont_(true);
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
	
	enum _StyleKind { Style, Indicator, Element, Font, Background }
	
	record struct _TableItem(string name, _StyleKind kind, int index);
	
	void _CodeEditor() {
		var b = _Page("Code editor").Columns(200, 20, -1);
		b.R.StartStack(vertical: true); //left
		
		b.StartGrid<KGroupBox>("Completion list");
		b.R.Add("Append ( )", out ComboBox complParen).Items("With Spacebar|Always|Never").Select(App.Settings.ci_complParen)
			.Tooltip("Append ( ) when selected a method or a keyword like 'if'");
		b.End();
		
		b.StartGrid<KGroupBox>("Statement completion");
		b.R.Add("Hotkey", out ComboBox enterWith).Items("Ctrl+Enter|Shift+Enter|Ctrl+Shift+Enter").Select(App.Settings.ci_enterWith);
		b.R.Add(out KCheckBox notClassicEnter, "Enter before )").Checked(!App.Settings.ci_classicEnter)
			.Tooltip("Key Enter before ) in an argument list completes statement like with Ctrl etc, except after comma or space");
		b.End();
		
		b.StartGrid<KGroupBox>("Formatting");
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
		b.R.Add(out KCheckBox formatAuto, "Auto-format").Checked(App.Settings.ci_formatAuto)
			.Tooltip("Automatically format statement on ;{} etc");
		b.End();
		
		//b.StartGrid<KGroupBox>("Insert code");
		//b.R.Add(out KCheckBox unexpandPath, "Unexpand path").Checked(App.Settings.ci_unexpandPath)
		//	.Tooltip("Insert file path like folders.System + \"file.exe\"");
		//b.End();
		
		b.End(); //left
		
		b.Skip().StartStack(vertical: true); //right
		
		b.StartGrid<KGroupBox>("Find references/implemetations, rename");
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
			App.Settings.ci_complParen = complParen.SelectedIndex;
			App.Settings.ci_enterWith = enterWith.SelectedIndex;
			App.Settings.ci_classicEnter = !notClassicEnter.IsChecked;
			
			if (formatCompact.IsChecked != App.Settings.ci_formatCompact || formatTabIndent.IsChecked != App.Settings.ci_formatTabIndent) {
				App.Settings.ci_formatCompact = formatCompact.IsChecked;
				App.Settings.ci_formatTabIndent = formatTabIndent.IsChecked;
				ModifyCode.FormattingOptions = null; //recreate
				
				//note: don't SCI_SETUSETABS(false).
				//	Eg VS does not use it, and it's good; VSCode uses, and it's bad, eg cannot insert tab in raw strings.
				//	All autocorrect/autoindent/format code inserts spaces if need. Users rarely have to type indentation tabs.
				//	And don't add options to set tab/indentation size. Too many options isn't good.
			}
			App.Settings.ci_formatAuto = formatAuto.IsChecked;
			
			//App.Settings.ci_unexpandPath = unexpandPath.IsChecked;
			//App.Settings.ci_shiftEnterAlways = (byte)(shiftEnter.IsChecked ? 0 : 1);
			//App.Settings.ci_shiftTabAlways = (byte)(shiftTab.IsChecked ? 0 : 1);
			//App.Settings.ci_breakString = (byte)breakString.SelectedIndex;
			
			App.Model.WSSett.ci_skipFolders = skipFolders.TextOrNull();
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
				tool_quick = captureMenu.TextOrNull(),
				tool_wnd = captureDwnd.TextOrNull(),
				tool_elm = captureDelm.TextOrNull(),
				tool_uiimage = captureDuiimage.TextOrNull(),
			};
			if (v != App.Settings.hotkeys) {
				App.Settings.hotkeys = v;
				RegHotkeys.UnregisterPermanent();
				RegHotkeys.RegisterPermanent();
			}
		};
	}
	
	void _Other() {
		var b = _Page("Other");
		b.R.Add("Internet search URL", out TextBox internetSearchUrl, App.Settings.internetSearchUrl);
		b.R.Add(out CheckBox printCompiled, "Always print \"Compiled\"").Checked(App.Settings.comp_printCompiled, threeState: true)
			.Tooltip("Always print a \"Compiled\" message when a script etc compiled successfully.\nIf unchecked, prints only if role is exeProgram or classLibrary.\nIf 3-rd state, prints only when executing the Compile command.");
		b.End();
		
		_b.OkApply += e => {
			App.Settings.internetSearchUrl = internetSearchUrl.TextOrNull() ?? "https://www.google.com/search?q=";
			App.Settings.comp_printCompiled = printCompiled.IsChecked;
		};
	}
	
	unsafe void _OS() {
		var b = _Page("OS");
		b.R.xAddInfoBlockF($"Some Windows settings for all programs{(!App.IsPortable ? null : "\n<s c='red'>Portable mode warning: portable apps should not change Windows settings.</s>")}");
		
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
	
	protected override void OnPreviewKeyDown(KeyEventArgs e) {
		if (e.Key == Key.F1 && Keyboard.Modifiers == 0) {
			HelpUtil.AuHelp("editor/Program settings");
			e.Handled = true;
			return;
		}
		base.OnPreviewKeyDown(e);
	}
	
	static class _Api {
		[DllImport("gdi32.dll", EntryPoint = "EnumFontFamiliesExW")]
		internal static extern int EnumFontFamiliesEx(IntPtr hdc, in Api.LOGFONT lpLogfont, FONTENUMPROC lpProc, nint lParam, uint dwFlags);
		internal unsafe delegate int FONTENUMPROC(Api.LOGFONT* lf, IntPtr tm, uint fontType, nint lParam);
		
	}
}
