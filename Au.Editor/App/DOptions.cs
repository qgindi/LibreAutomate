using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using Microsoft.Win32;
using System.Windows.Controls.Primitives;
using ToolLand;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Input;
using EStyle = LA.SciTheme.EStyle;

namespace LA;

class DOptions : KDialogWindow {
	public enum EPage { Program, Workspace, FontAndColors, CodeEditor, Templates, Hotkeys, AI, Other, OS }
	
	public static void AaShow(EPage? page = null) {
		var d = ShowSingle(() => new DOptions());
		if (page != null) d._tc.SelectedIndex = (int)page.Value;
	}
	
	wpfBuilder _b;
	TabControl _tc;
	
	DOptions() {
		InitWinProp(@"Options", App.Wmain);
		
		_b = new wpfBuilder(this).WinSize(600);
		_b.Row(-1).Add(out _tc).Height(300..);
		_b.R.StartOkCancel()
			.AddOkCancel(out var bOK, out var bCancel, out _, apply: "Apply")
			.xAddDialogHelpButtonAndF1("editor/Program settings");
		if (miscInfo.isChildSession) _b.Add<TextBlock>().FormatText($"<a {() => HelpUtil.AuHelp("editor/PiP session")}>PiP</a>");
		_b.End();
		bOK.IsDefault = false; bCancel.IsCancel = false;
		
		_Program();
		_Workspace();
		_FontAndColors();
		_CodeEditor();
		_Templates();
		_Hotkeys();
		_AI();
		_Other();
		_OS();
		
		//_tc.SelectedIndex = 2;
		
		_b.End();
	}
	
	/// <summary>
	/// Adds new TabItem to _tc. Creates and returns new wpfBuilder for building the tab page.
	/// </summary>
	wpfBuilder _Page(string name, WBPanelType panelType = WBPanelType.Grid) {
		var tp = new TabItem { Header = name, MinWidth = 30 };
		_tc.Items.Add(tp);
		return new wpfBuilder(tp, panelType).Margin("3");
	}
	
	void _Program() {
		var b = _Page("Program").Columns(-1, 20, -1);
		
		//left column
		b.StartGrid();
		b.R.Add(out KCheckBox startWithWin, "Start with Windows"); //note: must be the first checkbox in Options, and don't change text, because used for the forum registration security question
		b.R.Add(out KCheckBox startHidden, wpfBuilder.formattedText($"Start hidden; let <s b='#C42B1C' c='white' FontFamily='Segoe UI Symbol' FontSize='11'>  ✖  </s> hide")).Checked(App.Settings.runHidden);
		b.R.Add(out KCheckBox visibleIfNotAutoStarted, "Visible if not auto-started").Margin(22).Checked(App.Settings.startVisibleIfNotAutoStarted).xBindCheckedEnabled(startHidden);
		b.R.Add(out KCheckBox checkForUpdates, "Check for updates every day").Checked(App.Settings.checkForUpdates);
		b.R.AddButton("Check for updates now", o => App.CheckForUpdates(o.Button)).Margin(22).Width(150, "L");
		b.End();
		
		//right column
		b.Skip().StartStack(vertical: true);
		b.End();
		
		b.End();
		
		const string c_rkRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
		string init_swwValue = Registry.GetValue(@"HKEY_CURRENT_USER\" + c_rkRun, "Au.Editor", null) as string;
		bool init_swwYes = true == init_swwValue?.RxMatch($"^\"(.+?)\"", 1, out string s1) && filesystem.more.isSameFile(s1, process.thisExePath);
		startWithWin.IsChecked = init_swwYes;
		if (App.IsPortable) startWithWin.Checked += (_, _) => dialog.showWarning("Portable mode warning", "This setting will be saved in the Registry. Portable apps should not do it.", owner: this);
		
		_b.OkApply += e => {
			if (startWithWin.IsChecked != init_swwYes || (init_swwYes && !init_swwValue.Ends(" /a"))) {
				try {
					using var rk = Registry.CurrentUser.OpenSubKey(c_rkRun, true);
					if (init_swwYes = startWithWin.IsChecked) rk.SetValue("Au.Editor", init_swwValue = $"\"{process.thisExePath}\" /a");
					else rk.DeleteValue("Au.Editor");
				}
				catch (Exception ex) { print.it("Failed to change 'Start with Windows'. " + ex.ToStringWithoutStack()); }
			}
			App.Settings.runHidden = startHidden.IsChecked;
			App.Settings.startVisibleIfNotAutoStarted = visibleIfNotAutoStarted.IsChecked;
			App.Settings.checkForUpdates = checkForUpdates.IsChecked;
		};
	}
	
	void _Workspace() {
		var b = _Page("Workspace").Columns(-1.5, 20, -1);
		b.R.xAddInfoBlockT("Settings of current workspace");
		
		//left column
		b.R.StartStack(vertical: true);
		b.Add("Startup scripts", out TextBox startupScripts, App.Model.UserSettings.startupScripts).Multiline(110, TextWrapping.NoWrap)
			.Tooltip("Example:\nScript1.cs\n\\Folder\\Script2.cs\n//Disabled.cs\nDelay1.cs, 3s\nDelay2.cs, 300ms\n\"Comma, comma.cs\"")
			.Validation(_startupScripts_Validation);
		
		b.Add(out KCheckBox cBackup, "Auto-backup (Git commit)").Checked(App.Model.UserSettings.gitBackup).Margin("T10")
			.Tooltip("Silently run Git commit when LibreAutomate is visible the first time after loading this workspace or activated later after several hours from the last backup.\nIt creates a local backup of workspace files (scripts etc). To upload etc, you can use menu File > Git.");
		cBackup.Checked += (_, _) => { if (!Git.IsReady) App.Dispatcher.InvokeAsync(Git.Setup); };
		b.End();
		
		//right column
		b.Skip().StartStack(vertical: true);
		b.Add("Hide/ignore files and folders", out TextBox tSyncFsSkip, App.Model.WSSett.syncfs_skip).Multiline(110, TextWrapping.NoWrap)
			.Tooltip(@"Hide and don't use files and folders whose paths in workspace would match a wildcard from this list.
Example:
*.bak
*\FolderAnywhere
\Folder
\Folder1\Folder2
//Comment");
		b.End();
		
		b.End();
		
		_b.OkApply += e => {
			App.Model.UserSettings.startupScripts = startupScripts.Text.Trim().NullIfEmpty_();
			App.Model.UserSettings.gitBackup = cBackup.IsChecked;
			
			string skipOld = App.Model.WSSett.syncfs_skip, skipNew = tSyncFsSkip.TextOrNull();
			App.Model.WSSett.syncfs_skip = skipNew;
			if (skipNew != skipOld) App.Model.SyncWithFilesystem_();
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
		
		public void Init(string[] fonts) {
			name.ItemsSource = fonts;
			size.MouseWheel += static (o, e) => {
				var tb = o as TextBox;
				if (!tb.Text.ToNumber(out double d)) d = 9;
				tb.Text = Math.Clamp(d + e.Delta / 160d, 6, 30).ToS("0.##"); //120/160d=0.75
			};
		}
		
		public void Init(string[] fonts, AppSettings.font_t f) {
			Init(fonts);
			Set(f.name, f.size);
		}
		
		public void Set(string fontName, double fontSize) {
			name.SelectedItem = fontName; if (name.SelectedItem is null) name.Text = fontName;
			size.Text = fontSize.ToS("0.##");
		}
		
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
		SciTheme theme = null, savedTheme = null;
		bool ignoreColorEvents = false;
		
		var b = _Page("Font, colors", WBPanelType.Dock);
		b.Options(bindLabelVisibility: true);
		
		b.Add(out KScintilla sciStyles).Width(180);
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
		b.R.xAddInfoBlockT("Only editor font depends on theme.");
		b.End();
		b.R.StartGrid();
		var pColor = b.Panel as Grid;
		b.R.Add(out KColorPicker colorPicker);
		b.R.StartStack();
		var pFontStyle = b.Panel;
		b.Add(out KCheckBox cBold, "Bold");
		b.Add(out KCheckBox cItalic, "Italic");
		b.Add(out KCheckBox cUnderline, "Underline");
		b.Add(out KCheckBox cBackground, "Background");
		b.End();
		b.R.Add("", out TextBox tAlpha).Width(50, "L");
		var lAlpha = b.Last2 as Label;
		b.End();
		b.Row(-1);
		b.R.AddSeparator();
		b.R.StartStack();
		b.AddButton("Theme ▾", _ThemesButtonClicked).Width(70);
		b.End().Align("r");
		b.End();
		b.End();
		
		pColor.Visibility = Visibility.Collapsed;
		
		b.Loaded += () => {
			sciStyles.Call(Sci.SCI_SETCARETLINEFRAME, 1);
			sciStyles.aaaSetElementColor(Sci.SC_ELEMENT_CARET_LINE_BACK, 0xE0E0E0);
			sciStyles.Call(Sci.SCI_SETCARETLINEVISIBLEALWAYS, 1);
			
			//font
			
			List<string> fontsMono = new(), fontsVar = new();
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
			string[] fonts = ["[ Fixed-width fonts ]", .. fontsMono, "", "[ Variable-width fonts ]", .. fontsVar];
			
			font.Init(fonts);
			fontOutput.Init(fonts, App.Settings.font_output);
			fontRecipeText.Init(fonts, App.Settings.font_recipeText);
			fontRecipeCode.Init(fonts, App.Settings.font_recipeCode);
			fontFind.Init(fonts, App.Settings.font_find);
			
			//styles
			
			const int indicHidden = 0;
			sciStyles.aaaIndicatorDefine(indicHidden, Sci.INDIC_HIDDEN);
			sciStyles.aaaMarginSetWidth(1, 0);
			
			const int c_isStyle = 0, c_isIndicator = 1, c_isElementAlpha = 2, c_isFont = 3, c_isBackground = 4, c_isElementThickness = 5, c_isElementColorOnly = 6;
			
			(string name, int kind, int index)[] table = [
				("Font", c_isFont, 0),

				("Background", c_isBackground, 0),

				("Text", c_isStyle, (int)EStyle.None),
				("//Comment", c_isStyle, (int)EStyle.Comment),
				(@"""String"" 'c'", c_isStyle, (int)EStyle.String),
				(@"\r\n\t\0\\", c_isStyle, (int)EStyle.StringEscape),
				("1234567890", c_isStyle, (int)EStyle.Number),
				("()[]{},;:", c_isStyle, (int)EStyle.Punctuation),
				("Operator", c_isStyle, (int)EStyle.Operator),
				("Keyword", c_isStyle, (int)EStyle.Keyword),
				("Namespace", c_isStyle, (int)EStyle.Namespace),
				("Type", c_isStyle, (int)EStyle.Type),
				("Function", c_isStyle, (int)EStyle.Function),
				("Event", c_isStyle, (int)EStyle.Event),
				("Local variable", c_isStyle, (int)EStyle.LocalVariable),
				("Field variable", c_isStyle, (int)EStyle.Field),
				("Constant", c_isStyle, (int)EStyle.Constant),
				("GotoLabel", c_isStyle, (int)EStyle.Label),
				("#directive", c_isStyle, (int)EStyle.Preprocessor),
				("#if-disabled", c_isStyle, (int)EStyle.Excluded),
				("/// doc text", c_isStyle, (int)EStyle.XmlDocText),
				("/// <doc tag>", c_isStyle, (int)EStyle.XmlDocTag),
				("Regexp text", c_isStyle, (int)EStyle.RxText),
				("Regexp meta", c_isStyle, (int)EStyle.RxMeta),
				("Regexp chars", c_isStyle, (int)EStyle.RxChars),
				("Regexp option", c_isStyle, (int)EStyle.RxOption),
				("Regexp escape", c_isStyle, (int)EStyle.RxEscape),
				("Regexp callout", c_isStyle, (int)EStyle.RxCallout),
				("Regexp comment", c_isStyle, (int)EStyle.RxComment),
				("Line number text", c_isStyle, (int)EStyle.LineNumber),
				("Line number margin", c_isElementColorOnly, -1),
				("Marker margin", c_isElementColorOnly, -2),

				("Text highlight", c_isIndicator, SciTheme.Indic.Found),
				("Symbol highlight", c_isIndicator, SciTheme.Indic.Refs),
				("Brace highlight", c_isIndicator, SciTheme.Indic.Braces),
				("Debug highlight", c_isIndicator, SciTheme.Indic.Debug),
				("Snippet field", c_isIndicator, SciTheme.Indic.SnippetField),
				("Snippet field active", c_isIndicator, SciTheme.Indic.SnippetFieldActive),

				("Selection", c_isElementAlpha, Sci.SC_ELEMENT_SELECTION_BACK),
				("Selection no focus", c_isElementAlpha, Sci.SC_ELEMENT_SELECTION_INACTIVE_BACK),
				
				("Caret", c_isElementThickness, Sci.SC_ELEMENT_CARET),
				("Caret line frame", c_isElementThickness, Sci.SC_ELEMENT_CARET_LINE_BACK),
			];
			
			sciStyles.aaaText = string.Join("\r\n", table.Select(o => o.name));
			for (int i = 0; i < table.Length; i++) {
				int lineStart = sciStyles.aaaLineStart(false, i), lineEnd = sciStyles.aaaLineEnd(false, i);
				sciStyles.aaaIndicatorAdd(indicHidden, false, lineStart..lineEnd, i + 1);
				var kind = table[i].kind;
				if (kind is c_isStyle) {
					sciStyles.Call(Sci.SCI_STARTSTYLING, lineStart);
					sciStyles.Call(Sci.SCI_SETSTYLING, lineEnd - lineStart, table[i].index);
				} else if (kind is c_isIndicator) {
					sciStyles.aaaIndicatorAdd(table[i].index, false, lineStart..lineEnd);
				} else if (kind is c_isElementColorOnly && table[i].index is -1) { //line number margin
					sciStyles.Call(Sci.SCI_STARTSTYLING, lineStart);
					sciStyles.Call(Sci.SCI_SETSTYLING, lineEnd - lineStart, Sci.STYLE_LINENUMBER);
				}
			}
			
			//when changed current line
			int currentItem = 0;
			sciStyles.AaNotify += e => {
				switch (e.n.code) {
				case Sci.NOTIF.SCN_UPDATEUI:
					int i = sciStyles.aaaIndicatorGetValue(indicHidden, sciStyles.aaaLineStartFromPos(false, sciStyles.aaaCurrentPos8)) - 1;
					if (i != currentItem && i >= 0) {
						currentItem = i;
						var k = table[i];
						if (k.kind is c_isFont) {
							pColor.Visibility = Visibility.Collapsed;
							gFont.Visibility = Visibility.Visible;
						} else {
							gFont.Visibility = Visibility.Collapsed;
							pColor.Visibility = Visibility.Visible;
							ignoreColorEvents = true;
							int col;
							pFontStyle.Visibility = k.kind is c_isStyle ? Visibility.Visible : Visibility.Collapsed;
							tAlpha.Visibility = k.kind is c_isIndicator or c_isElementAlpha or c_isElementThickness ? Visibility.Visible : Visibility.Collapsed;
							lAlpha.Content = k.kind is c_isElementThickness ? "Thickness 1-4" : "Opacity 0-255";
							if (k.kind is c_isStyle) {
								ref var rs = ref theme[(EStyle)k.index];
								col = rs.color;
								cBold.IsChecked = rs.bold;
								cItalic.IsChecked = rs.italic;
								cUnderline.IsChecked = rs.underline;
								bool back = (EStyle)k.index is >= EStyle.RxText and <= EStyle.RxComment;
								if (back) cBackground.IsChecked = rs.back;
								cBackground.Visibility = back ? Visibility.Visible : Visibility.Collapsed;
							} else if (k.kind is c_isIndicator) {
								ref var ri = ref theme.Indicator(k.index);
								col = ri.color;
								tAlpha.Text = ri.alpha.ToS();
							} else if (k.kind is c_isElementAlpha or c_isElementThickness) {
								col = theme.Element(k.index);
								tAlpha.Text = (col >>> 24).ToS();
								col &= 0xFFFFFF;
							} else if (k.kind is c_isElementColorOnly) {
								col = theme.Element(k.index) & 0xFFFFFF;
							} else { //Background
								col = theme.Background;
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
				(theme.FontName, theme.FontSize) = font.Get();
				theme.ToScintilla(sciStyles);
			};
			font.name.AddHandler(TextBoxBase.TextChangedEvent, textChanged);
			font.size.AddHandler(TextBoxBase.TextChangedEvent, textChanged);
			
			colorPicker.ColorChanged += _ => _UpdateSci();
			cBold.CheckChanged += (sender, _) => _UpdateSci(sender);
			cItalic.CheckChanged += (sender, _) => _UpdateSci(sender);
			cUnderline.CheckChanged += (sender, _) => _UpdateSci(sender);
			cBackground.CheckChanged += (sender, _) => _UpdateSci(sender);
			tAlpha.TextChanged += (sender, _) => _UpdateSci(sender);
			
			void _UpdateSci(object control = null) {
				if (ignoreColorEvents || currentItem < 0) return;
				var k = table[currentItem];
				int col = colorPicker.Color;
				if (k.kind is c_isStyle) {
					ref var rs = ref theme[(EStyle)k.index];
					if (control == cBold) rs.bold = cBold.IsChecked;
					else if (control == cItalic) rs.italic = cItalic.IsChecked;
					else if (control == cUnderline) rs.underline = cUnderline.IsChecked;
					else if (control == cBackground) rs.back = cBackground.IsChecked;
					else rs.color = col;
				} else if (k.kind is c_isIndicator) {
					ref var ri = ref theme.Indicator(k.index);
					if (control == tAlpha) ri.alpha = Math.Clamp(tAlpha.Text.ToInt(), 0, 255);
					else ri.color = col;
				} else if (k.kind is c_isElementAlpha or c_isElementThickness or c_isElementColorOnly) {
					ref var m = ref theme.Element(k.index);
					if (control == tAlpha) {
						bool cl = k.kind is c_isElementThickness; //note: don't allow caret line frame 0, which set line background color instead of frame, because it would be mixed with translucent indicators
						m = (m & 0xFFFFFF) | Math.Clamp(tAlpha.Text.ToInt(), cl ? 1 : 0, cl ? 4 : 255) << 24;
					} else m = (m & ~0xFFFFFF) | (col & 0xFFFFFF);
				} else if (k.kind is c_isBackground) {
					theme.Background = col;
				}
				theme.ToScintilla(sciStyles);
			}
			
			_b.OkApply += e => {
				bool stylesChanged = theme != savedTheme;
				bool stylesOrThemeChanged = _ThemeApply(stylesChanged);
				if (stylesChanged) { savedTheme = SciTheme.Current; theme = savedTheme with { }; }
				
				if (fontFind.Apply(ref App.Settings.font_find) || stylesOrThemeChanged) Panels.Find.CodeStylesChanged_();
				if (fontOutput.Apply(ref App.Settings.font_output)) Panels.Output.Scintilla.AaSetStyles();
				if (fontRecipeText.Apply(ref App.Settings.font_recipeText) | fontRecipeCode.Apply(ref App.Settings.font_recipeCode)) Panels.Recipe.Scintilla.AaChangedFontSettings();
			};
			
			_OpenTheme(SciTheme.Current);
		};
		
		void _OpenTheme(SciTheme t) {
			savedTheme = t;
			theme = savedTheme with { };
			
			font.Set(theme.FontName, theme.FontSize);
			theme.ToScintilla(sciStyles);
			sciStyles.aaaGoToPos(false, 0);
		}
		
		void _ThemesButtonClicked(WBButtonClickArgs e) {
			var m = new popupMenu();
			_Add("LA.csv");
			foreach (var v in filesystem.enumFiles(SciTheme.ThemesDirDefaultBS, "*.csv")) _Add(v.Name);
			
			void _Add(string fn) {
				var s = fn[..^4];
				_Add2(s);
				if (filesystem.exists(SciTheme.ThemesDirCustomizedBS + fn)) _Add2(s + " [customized]");
				
				void _Add2(string s) {
					var v = m.AddRadio(s);
					if (s == theme.Name) v.IsChecked = true;
				}
			}
			
			m.Separator();
			m.Submenu("Open folder", m => {
				m["Default themes"] = o => { run.itSafe(SciTheme.ThemesDirDefaultBS); };
				m["Customized themes"] = o => { run.itSafe(SciTheme.ThemesDirCustomizedBS); };
			});
			
			m.Show(owner: this);
			if (m.Result is { IsChecked: true } r) {
				_OpenTheme(new(r.Text));
			}
		}
		
		bool _ThemeApply(bool modified) {
			if (!modified && theme.Name == SciTheme.Current.Name) return false;
			
			var name = theme.Name;
			if (modified && !name.Ends(" [customized]")) name += " [customized]";
			
			var t = theme with { Name = name };
			SciTheme.Current = t;
			
			App.Settings.edit_theme = name == "LA" ? null : name;
			if (modified) t.Save();
			
			foreach (var v in Panels.Editor.OpenDocs) {
				t.ToScintilla(v);
				v.ESetLineNumberMarginWidth_();
			}
			
			return true;
		}
	}
	
	void _CodeEditor() {
		var b = _Page("Code editor").Columns(-1, 20, -1);
		b.R.StartStack(vertical: true); //left
		
		b.StartGrid<KGroupBox>("Completion list");
		b.R.Add("Append ( )", out ComboBox complParen).Items("Space|Space, Tab, Enter, 2*click|None").Select(App.Settings.ci_complParen)
			.Tooltip("These keys etc can be used to append ( ) when completing a function name or a keyword like 'if'.\nPlus characters ;.,:([{+-*/ etc.");
		b.End();
		
		b.StartGrid<KGroupBox>("Statement completion");
		b.R.Add("Hotkey", out ComboBox enterWith).Items("Ctrl+Enter|Shift+Enter|Ctrl+Shift+Enter").Select(App.Settings.ci_enterWith);
		b.R.Add(out KCheckBox enterBeforeParen, "Enter before )").Checked(App.Settings.ci_enterBeforeParen)
			.Tooltip("Key Enter before ) or ] completes statement like with Ctrl etc.\nExcept after comma or space or with Shift etc.");
		b.R.Add(out KCheckBox enterBeforeSemicolon, "Enter before ;").Checked(App.Settings.ci_enterBeforeSemicolon)
			.Tooltip("Key Enter before ; completes statement like with Ctrl etc.\nExcept after comma or space or with Shift etc.");
		b.R.Add(out KCheckBox autoSemicolon, "Add ; when starting a statement").Checked(App.Settings.ci_semicolon);
		b.End();
		
		b.End(); //left
		
		b.Skip().StartStack(vertical: true); //right
		
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
			.Tooltip("For indent use tab character, not spaces");
		b.R.Add(out KCheckBox formatAuto, "Auto-format").Checked(App.Settings.ci_formatAuto)
			.Tooltip("Automatically format statement on ;{} etc");
		b.End();
		
		b.StartGrid<KGroupBox>("Find references, rename");
		b.R.Add("Skip\nfolders", out TextBox skipFolders, App.Model.WSSett.ci_skipFolders).Multiline(55, wrap: TextWrapping.NoWrap)
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
			App.Settings.ci_enterBeforeParen = enterBeforeParen.IsChecked;
			App.Settings.ci_enterBeforeSemicolon = enterBeforeSemicolon.IsChecked;
			App.Settings.ci_semicolon = autoSemicolon.IsChecked;
			
			if (formatCompact.IsChecked != App.Settings.ci_formatCompact || formatTabIndent.IsChecked != App.Settings.ci_formatTabIndent) {
				App.Settings.ci_formatCompact = formatCompact.IsChecked;
				App.Settings.ci_formatTabIndent = formatTabIndent.IsChecked;
				ModifyCode.FormattingOptions = null; //recreate
				
				//note: don't SCI_SETUSETABS(false).
				//	Eg VS does not use it, and it's good; VSCode uses, and it's bad, eg cannot insert tab in raw strings.
				//	All autocorrect/autoindent/format code inserts spaces if need. Users rarely have to type indent tabs.
				//	And don't add options to set tab/indent size. Too many options isn't good.
			}
			App.Settings.ci_formatAuto = formatAuto.IsChecked;
			
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
		sci.AaTextChanged += _ => customText[template.SelectedIndex] = sci.aaaText;
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
			if (v != App.Settings.session.hotkeys) {
				App.Settings.session.hotkeys = v;
				RegHotkeys.UnregisterPermanent();
				RegHotkeys.RegisterPermanent();
			}
		};
	}
	
	void _AI() {
		var b = _Page("AI", WBPanelType.VerticalStack);
		
		b.xAddGroupSeparator("API keys");
		b.StartGrid().Columns(100, -1);
		b.R.Add(out ComboBox api)
			.Add(out KPasswordBox apiKey).Tooltip("API key or environment variable (if (X) checked).\nAPI keys are saved encrypted and can't be decrypted on other computers/accounts.").Hidden();
		b.End();
		
		b.StartGrid().Columns(0, -1, 10, 0, -1);
		b.xAddGroupSeparator("Models for documentation search and chat");
		b.R.Add("Search", out ComboBox modelDocSearch).Tooltip("AI embedding model for documentation search and chat RAG");
		b.Skip().Add("Chat", out ComboBox modelDocChat).Tooltip("AI chat model");
		//b.R.StartGrid<KGroupBox>("Chat model settings");
		//b.End();
		
		//rejected. Currently using Voyage multimodal. It supports text and images.
		//b.xAddGroupSeparator("Models for icon search in the Icons tool");
		//b.R.Add("Search", out ComboBox modelIconSearch).Tooltip("AI embedding model for icon search");
		//b.Skip().Add("Improve", out ComboBox modelIconImprove).Tooltip("AI chat model for filtering/reranking AI search results");
		
		b.AddSeparator(false);
		b.R.AddButton("...", _ => _MoreMenu()).Align(HorizontalAlignment.Left).Span(1);
		
		b.End();
		b.End();
		
		b.Loaded += () => {
			var apiNames = AI.AiModel.Models.Select(o => o.api).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
			var apiKeys = new (bool once, (string key, bool ev) now, (string key, bool ev) old)[apiNames.Length];
			int currentApi = -1;
			api.ItemsSource = apiNames;
			api.SelectionChanged += (_, e) => {
				if (currentApi >= 0) apiKeys[currentApi].now = (apiKey.Text.NullIfEmpty_(), apiKey.IsEnvVar);
				currentApi = api.SelectedIndex;
				apiKey.Visibility = currentApi >= 0 ? Visibility.Visible : Visibility.Hidden;
				if (currentApi < 0) return;
				if (!apiKeys[currentApi].once) {
					apiKeys[currentApi].once = true;
					App.Settings.ai_ak.TryGetValue((string)api.SelectedItem, out var key);
					bool ev = false;
					if (key.NE()) key = null;
					else {
						ev = key is ['%', _, .., '%'];
						key = ev ? key[1..^1] : EdProtectedData.Unprotect(key);
					}
					apiKeys[currentApi].now = apiKeys[currentApi].old = (key, ev);
				}
				(apiKey.Text, apiKey.IsEnvVar) = apiKeys[currentApi].now;
			};
			//api.SelectedIndex = 0; //no
			b.Validation(apiKey, _ => {
				if (currentApi >= 0) apiKeys[currentApi].now = (apiKey.Text.NullIfEmpty_(), apiKey.IsEnvVar);
				if (apiKeys.Any(o => o.now.ev && o.now.key.Lenn() >= 32)) return "Variable name too long. Uncheck (X) if it's not a variable."; //probably checked the toggle button to see the key and forgot to uncheck
				return null;
			});
			
			_InitModelCombo(modelDocSearch, o => o is AI.AiEmbeddingModel { isCompact: false }, App.Settings.ai_modelDocSearch);
			_InitModelCombo(modelDocChat, o => o is AI.AiChatModel, App.Settings.ai_modelDocChat);
			//_InitModelCombo(modelIconSearch, o => o is AI.AiEmbeddingModel { isCompact: true }, App.Settings.ai_modelIconSearch);
			//_InitModelCombo(modelIconImprove, o => o is AI.AiChatModel, App.Settings.ai_modelIconImprove, true);
			void _InitModelCombo(ComboBox c, Func<AI.AiModel, bool> predicate, string select, bool optional = false) {
				var e = AI.AiModel.Models.Where(predicate).Select(o => o.DisplayName);
				if (optional) e = e.Prepend("none");
				c.ItemsSource = e.ToArray();
				c.SelectedItem = select;
			}
			
			_b.OkApply += e => {
				foreach (var (i, v) in apiKeys.Index()) {
					if (v.once && v.now != v.old) {
						apiKeys[i].old = v.now;
						var key = v.now.key == null ? null : v.now.ev ? $"%{v.now.key}%" : EdProtectedData.Protect(v.now.key);
						App.Settings.ai_ak[apiNames[i]] = key;
					}
				}
				
				App.Settings.ai_modelDocSearch = _GetModelCombo(modelDocSearch);
				App.Settings.ai_modelDocChat = _GetModelCombo(modelDocChat);
				//App.Settings.ai_modelIconSearch = _GetModelCombo(modelIconSearch);
				//App.Settings.ai_modelIconImprove = _GetModelCombo(modelIconImprove);
				string _GetModelCombo(ComboBox c) => c.SelectedItem is string s && s != "none" ? s : null;
			};
		};
		
		void _MoreMenu() {
			var m = new popupMenu();
			m["Print all model configurations"] = o => { print.it(AI.AiModel.Models); };
			m["How to add new model"] = o => { _AddCustomModel(); };
			var emFolder = folders.ThisAppDataCommon + $@"AI\Embedding";
			if (filesystem.exists(emFolder).Directory) m["Open embedding vectors folder"] = o => { var s = run.itSafe(emFolder); };
			
			m.Show(owner: this);
		}
		
		void _AddCustomModel() {
			if (modelDocChat.SelectedIndex < 0 && modelDocChat.Items.Count > 0) modelDocChat.SelectedIndex = 0;
			if (!(modelDocChat.SelectedItem is string mdn && AI.AiModel.Models.FirstOrDefault(o => o is AI.AiChatModel && o.DisplayName == mdn) is AI.AiChatModel m)) return;
			string code = $$"""
/*/ role editorExtension; testInternal Au.Editor; r Au.Editor.dll; /*/
using AI;
var model1 = AiModel.GetModel<{{m.GetType()}}>() with { model = "model-name" };
AiModel.Models.Add(model1);

""";
			StringBuilder s = new("<>To add an AI model configuration, you need a script with role editorExtension (");
			foreach (var v in App.Model.GetStartupScriptsExceptDisabled()) if (new MetaCommentsParser(v.f).role == "editorExtension") s.Append(v.f.SciLink()).Append(", ");
			s.Append($$"""
<+newStartupScriptEditorExtension {{code.Replace('>', '\x1')}}>create<>). Add the script to <+options Workspace>startup scripts<>.
It's possible to create a completely new AI model configuration (any AI API, any parameters). How - please ask in the forum.
Or clone an existing model configuration and change some properties. Example:
<code>{{code}}</code>
""");
			print.it(s);
			Panels.Output.Scintilla.AaTags.AddLinkTag("+newStartupScriptEditorExtension", code => {
				code = code.Replace('\x1', '>');
				App.Model.NewItem("Script.cs", name: "init.cs", beginRenaming: true, text: new(true, code));
				//note: can't auto-add to startup scripts now, because the final script name is unknown
			});
		}
	}
	
	void _Other() {
		var b = _Page("Other");
		b.R.Add("Documentation", out ComboBox localHelp).Items("Online documentation of the latest program version|Local documentation of the installed program version").Select(App.Settings.localDocumentation ? 1 : 0);
		b.R.Add("Internet search URL", out TextBox internetSearchUrl, App.Settings.internetSearchUrl);
		b.R.Add(out CheckBox minimalSdk, "Use minimal .NET SDK").Checked(App.Settings.minimalSDK, threeState: true).Tooltip("The SDK is used to install NuGet packages and for the Publish feature.\nIndeterminate - use full SDK if installed, else minimal.");
		b.R.Add(out CheckBox printCompiled, "Always print \"Compiled\"").Checked(App.Settings.comp_printCompiled, threeState: true)
			.Tooltip("Always print a \"Compiled\" message when a script etc compiled successfully.\nIf unchecked, prints only if role is exeProgram or classLibrary.\nIf 3-rd state, prints only when executing the Compile command.");
		b.End();
		
		b.Loaded += () => {
			_b.OkApply += e => {
				if ((localHelp.SelectedIndex == 1) != App.Settings.localDocumentation) {
					App.Settings.localDocumentation ^= true;
					DocsHttpServer.StartOrSwitch();
				}
				App.Settings.internetSearchUrl = internetSearchUrl.TextOrNull();
				App.Settings.minimalSDK = minimalSdk.IsChecked;
				App.Settings.comp_printCompiled = printCompiled.IsChecked;
			};
		};
	}
	
	unsafe void _OS() {
		var b = _Page("OS");
		b.R.xAddInfoBlockF($"Some Windows settings for all programs{(!App.IsPortable ? null : "\n<s c='red'>Portable mode warning: portable apps should not change Windows settings.</s>")}");
		
		b.R.Add("Key/mouse hook timeout, ms", out TextBox hooksTimeout).Width(70, "L");
		b.R.Add(out KCheckBox cDisableLAW, "Disable \"lock active window\"");
		b.R.Add(out KCheckBox cUnderlineAK, "Underline menu/dialog item access keys");
		b.R.AddButton("Java...", _ => Delm.Java.EnableDisableJabUI(this)).Width(70, "L");
		b.End();
		
		b.Loaded += () => {
			hooksTimeout.Text = WindowsHook.LowLevelHooksTimeout.ToS();
			b.Validation(hooksTimeout, o => ((o as TextBox).Text.ToInt() is >= 300 and <= 1000) ? null : "300-1000");
			bool disableLAW = 0 == Api.SystemParametersInfo(Api.SPI_GETFOREGROUNDLOCKTIMEOUT, 0);
			cDisableLAW.IsChecked = disableLAW;
			bool underlineAK = Api.SystemParametersInfo(Api.SPI_GETKEYBOARDCUES);
			cUnderlineAK.IsChecked = underlineAK;
			
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
		};
	}
	
	static class _Api {
		[DllImport("gdi32.dll", EntryPoint = "EnumFontFamiliesExW")]
		internal static extern int EnumFontFamiliesEx(IntPtr hdc, in Api.LOGFONT lpLogfont, FONTENUMPROC lpProc, nint lParam, uint dwFlags);
		internal unsafe delegate int FONTENUMPROC(Api.LOGFONT* lf, IntPtr tm, uint fontType, nint lParam);
		
	}
}
