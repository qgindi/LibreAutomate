/* role editorExtension; define SCRIPT; testInternal Au,Au.Editor,Au.Controls; r Au.Editor.dll; r Au.Controls.dll; /*/

using System.Windows.Controls;
using Au.Controls;
using System.Windows.Documents;

#if SCRIPT
using Au.Tools;

Docr.Dialog();
#else
namespace Au.Tools;

record OcrEngineSettings {
	public string wLang, tLang, tCL, gKey, gFeat, gIC, mUrl, mKey;
}
#endif

class Docr : KDialogWindow {
	public static void Dialog()
		=> TUtil.ShowDialogInNonmainThread(() => new Docr());

	wnd _wnd, _con;
	bool _useCon;

	KSciInfoBox _info;
	Button _bTest, _bInsert;
	ComboBox _cbAction;
	const int c_waitnot = 6, c_finder = 7;
	KSciCodeBoxWnd _code;
	TextBox _text;

	KCheckBox exceptionC, exceptionnoC;
	KCheckTextBox rectC, scaleC, skipC, waitC, waitnoC;
	KCheckComboBox wiflagsC, engineC;
	TextBox textC;
	//CONSIDER: add wnd Activate if pixels from screen.

	public Docr() {
		Title = "Find OCR text";

		_noeventValueChanged = true;
		var b = new wpfBuilder(this).WinSize((460, 440..), (500, 400..)).Columns(160, 20, -1);
		b.R.Add(out _info).Height(80);
		b.R.StartGrid().Columns(76, 76, 76, -1);
		//row 1
		b.R.AddButton("Window...", _ => _bWnd_Click());
		b.AddButton(out _bTest, "Test", _Test).Disabled().Tooltip("Execute the code now (except wait/fail/mouse) and show the rectangle");
		b.AddButton(out _bInsert, "Insert", _Insert).Disabled();
		b.Add(out _cbAction).Align("L").Width(140).Items("|MouseMove|MouseClick|MouseClickD|MouseClickR|PostClick|waitNot|new ocrFinder").Select(2);
		//row 2
		b.StartStack();
		waitC = b.xAddCheckText("Wait", "1", check: true); b.Width(53);
		(waitnoC = b.xAddCheckText("Timeout", "5")).Visible = false; b.Width(53);
		b.xAddCheck(out exceptionC, "Fail if not found").Checked();
		b.xAddCheck(out exceptionnoC, "Fail on timeout").Checked().Hidden(null);
		b.End();
		b.End();
		//row 3
		//left
		b.R.StartGrid();
		b.R.Add("Find text:", out textC, row2: 0);
		skipC = b.R.xAddCheckText("Skip");
		b.End();
		//right
		b.Skip().xStartPropertyGrid();
		rectC = b.xAddCheckText("Rectangle", "0, 0, ^0, ^0");
		b.And(21).AddButton("···", _bRect_Click).Tooltip("Select rectangle in window").Height(19); //info: the '·' in "···" is U+00B7, because '.' is too low
		wiflagsC = b.xAddCheckCombo("Flags", "WindowDC|PrintWindow");
		scaleC = b.xAddCheckText("Scale");
		engineC = b.xAddCheckCombo("OCR engine", "Win10|Tesseract|GoogleCloud|MicrosoftAzure");
		b.And(21).AddButton("···", _bEngine_Click).Tooltip("OCR engine parameters").Height(19);
		b.xEndPropertyGrid();

		b.Row(100).xAddInBorder(out _code);
		b.xAddSplitterH(span: -1);
		b.Row(-1).Add(out _text).Margin("T").Multiline().Readonly();
		b.End();
		_noeventValueChanged = false;

		WndSavedRect.Restore(this, App.Settings.wndpos.ocr, o => App.Settings.wndpos.ocr = o);
	}

	static Docr() {
		TUtil.OnAnyCheckTextBoxValueChanged<Docr>((d, o) => d._AnyCheckTextBoxComboValueChanged(o), comboToo: true);
	}

	protected override void OnSourceInitialized(EventArgs e) {
		base.OnSourceInitialized(e);

		_InitInfo();
		//_bWnd_Click(); //rejected. Confusing.
	}

	protected override void OnClosed(EventArgs e) {
		base.OnClosed(e);

		App.Hmain.ActivateL();
	}

	void _bWnd_Click() {
		var r = _code.AaShowWndTool(this, _wnd, _con, checkControl: _wnd.Is0 || _useCon);
		if (r.ok) _SetWndCon(r.w, r.con, r.useCon);
	}

	void _bRect_Click(WBButtonClickArgs e) {
		if (_wnd.Is0) return;
		var m = new popupMenu();
		m["Select rectangle..."] = o => { if (_CaptureRect(out var r)) rectC.Set(true, r.rect.ToStringFormat("({0}, {1}, {4}, {5})")); };
		m.Show(owner: this);
	}

	void _SetWndCon(wnd w, wnd con, bool useCon) {
		var wPrev = _AreaWnd;
		_wnd = w;
		_con = con == w ? default : con;

		_noeventValueChanged = true;
		_useCon = useCon && !_con.Is0;
		if (_AreaWnd != wPrev) rectC.c.IsChecked = false;
		_noeventValueChanged = false;
		_FormatCode();
	}

	//when checked/unchecked any checkbox, and when text changed of any textbox
	void _AnyCheckTextBoxComboValueChanged(object source) {
		if (!_noeventValueChanged) {
			_noeventValueChanged = true;
			//print.it("source", source);
			if (source is KCheckBox c) {
				//bool on = c.IsChecked;
			} else if (source is TextBox t && t.Tag is KCheckTextBox k) {
				_noeventValueChanged = _formattedOnValueChanged = false; //allow auto-check but prevent formatting twice
				k.CheckIfTextNotEmpty();
				if (_formattedOnValueChanged) return;
			} else if (source is ComboBox u && u.Tag is KCheckComboBox m) {
				_noeventValueChanged = _formattedOnValueChanged = false; //allow auto-check but prevent formatting twice
				m.c.IsChecked = true;
				if (_formattedOnValueChanged) return;
			} else if (source == _cbAction) {
				int i = _cbAction.SelectedIndex;
				waitnoC.Visible = i == c_waitnot;
				exceptionnoC.Visibility = i == c_waitnot ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
				waitC.Visible = i < c_waitnot;
				exceptionC.Visibility = i < c_waitnot ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
			}
			_noeventValueChanged = false;
			_formattedOnValueChanged = true;
			//print.it("format");
			_FormatCode();
		}
	}
	bool _noeventValueChanged, _formattedOnValueChanged;

	(string code, string wndVar) _FormatCode(bool forTest = false) {
		//print.it("_FormatCode");
		StringBuilder b = new(), bb = new(); //b for the find/finder function line and everything after it; bb for everything before it.

		string waitTime = null;
		bool finder = false, waitNot = false, wait = false, orThrow = false, notTimeout = false;
		int iAction = 0;
		if (!forTest) {
			iAction = _cbAction.SelectedIndex;
			if (finder = iAction == c_finder) {

			} else if (waitNot = iAction == c_waitnot) {
				notTimeout = waitnoC.GetText(out waitTime, emptyToo: true);
				orThrow = notTimeout && exceptionnoC.IsChecked;
				if (waitTime.NE()) waitTime = "0";
			} else {
				wait = waitC.GetText(out waitTime, emptyToo: true);
				orThrow = exceptionC.IsChecked;
			}
		}

		string wndCode = null, wndVar = null;
		if (finder) {
			b.Append("var f = new ocrFinder(");
		} else {
			b.Append(waitNot ? "ocr.waitNot(" : "ocr.find(");
			if (wait || waitNot || orThrow) if (b.AppendWaitTime(waitTime ?? "0", orThrow, appendAlways: waitNot)) b.Append(", ");

			(wndCode, wndVar) = _code.AaGetWndFindCode(forTest, _wnd, _useCon ? _con : default);
			bb.AppendLine(wndCode);

			if (rectC.GetText(out var sRect)) b.AppendFormat("new({0}, {1})", wndVar, sRect);
			else b.Append(wndVar);
		}

		bool isEngine = engineC.GetIndex(out var ieng);
		if (isEngine) {
			var g = App.Settings.ocr ?? new();
			bb.AppendFormat("{0}engine = new Ocr{1}(", forTest ? "var " : "ocr.", engineC.t.SelectedItem);
			if (ieng == 3) bb.AppendStringArg(g.mUrl).AppendStringArg(g.mKey);
			else if (ieng == 2) bb.AppendStringArg(g.gKey);
			bb.Append(")");
			if (ieng switch { 0 => !g.wLang.NE(), 1 => !g.tLang.NE() || !g.tCL.NE(), 2 => !g.gFeat.NE() || !g.gIC.NE(), _ => false }) {
				bb.Append(" { ");
				switch (ieng) {
				case 0:
					bb.Append("Language = ").AppendStringArg(g.wLang, noComma: true);
					break;
				case 1:
					if (!g.tLang.NE()) bb.Append("Language = ").AppendStringArg(g.tLang, noComma: true);
					if (!g.tCL.NE()) bb.Append(g.tLang.NE() ? "" : ", ").Append("CommandLine = ").AppendStringArg(g.tCL, noComma: true);
					break;
				case 2:
					if (!g.gFeat.NE()) bb.Append("Features = ").AppendStringArg(g.gFeat, noComma: true);
					if (!g.gIC.NE()) bb.Append(g.gFeat.NE() ? "" : ", ").Append("ImageContext = ").AppendStringArg(g.gIC, noComma: true);
					break;
				}
				bb.Append(" }");
			}
			bb.AppendLine(";");
		}

		b.AppendStringArg(textC.Text);

		if (wiflagsC.GetText(out var wiFlag)) b.AppendOtherArg("OcrFlags." + wiFlag);

		if (scaleC.GetText(out var scale) && scale != "0") b.AppendOtherArg(scale, "scale");

		if (forTest) b.AppendOtherArg(isEngine ? "engine" : "new OcrWin10()", "engine"); //don't set/use ocr.engine when testing

		if (skipC.GetText(out var skip)) b.AppendOtherArg(skip, "skip");

		b.Append(");");

		if (!forTest) {
			if (waitNot) {
				if (!orThrow && notTimeout) bb.Append("bool ok = ");
			} else if (!finder) {
				string mouse = iAction switch {
					1 => "t.MouseMove();",
					2 => "t.MouseClick();",
					3 => "t.MouseClickD();",
					4 => "t.MouseClickR();",
					5 => "t.PostClick();",
					_ => null
				};
				bb.Append("var t = ");
				if (!orThrow || mouse != null) b.AppendLine();
				if (!orThrow) b.Append("if(t != null) { ");
				if (mouse != null) b.Append(mouse);
				if (!orThrow) b.Append(" } else { print.it(\"not found\"); }");
			}
		}

		var R = bb.Append(b).ToString();

		if (!forTest) {
			_code.AaSetText(R, wndCode.Lenn());
			_bTest.IsEnabled = true; _bInsert.IsEnabled = true;
		}

		return (R, wndVar);
	}

	#region util, misc

	wnd _AreaWnd => _useCon ? _con : _wnd;

	bool _CaptureRect(out CIUResult r) {
		if (!CaptureScreen.ImageColorRectUI(out r, CIUFlags.Rectangle, this)) return false;

		var w2 = _useCon ? r.w : r.w.Window;
		string es = null;
		bool otherWindow = w2 != _AreaWnd;
		if (otherWindow) es = "Whole rectangle must be in the client area of the captured image's window or control.";
		if (es != null) {
			dialog.showError(null, es, owner: this);
			return false;
		}

		w2.MapScreenToClient(ref r.rect);
		return true;
	}

	#endregion

	#region Insert, Test

	///// <summary>
	///// When OK clicked, contains C# code. Else null.
	///// </summary>
	//public string aaResultCode { get; private set; }

	void _Insert(WBButtonClickArgs _1) {
		if (_close) {
			base.Close();
		} else if (_code.aaaText.NullIfEmpty_() is string s) {
			InsertCode.Statements(s, ICSFlags.MakeVarName1);
			//if (_Opt.Has(_EOptions.InsertClose)) {
			//	base.Close();
			//} else {
			_close = true;
			_bInsert.Content = "Close";
			_bInsert.MouseLeave += (_, _) => {
				_close = !true;
				_bInsert.Content = "Insert";
			};
			//}

			if (rectC.GetText(out var sRect)) TUtil.InfoRectCoord(_AreaWnd, sRect);
		}
	}
	bool _close;

	void _Test(WBButtonClickArgs _1) {
		var (code, wndVar) = _FormatCode(true); if (code.NE()) return;
		ocrFinder.testing_ = (true, null);
		var rr = TUtil.RunTestFindObject(this, code, wndVar, _AreaWnd, getRect: o => (o as ocr).GetRect(inScreen: true), activateWindow: true);
		_text.Text = ocrFinder.testing_.result?.Text;
		ocrFinder.testing_ = default;
		_info.InfoErrorOrInfo(rr.info);
	}

	#endregion

	#region info

	//TUtil.CommonInfos _commonInfos;
	void _InitInfo() {
		//_commonInfos = new TUtil.CommonInfos(_info);

		_info.aaaText = c_dialogInfo;
		_info.AaAddElem(this, c_dialogInfo);

		_info.InfoCT(rectC,
@"Limit the area to this rectangle in the client area of the window or control. Smaller = faster.
Can be <b>RECT<>: <code>(left, top, width, height)</code>. Or 4 <help Au.Types.Coord>Coord<>: <code>left, top, right, bottom</code>; for example <code>^0</code> is right/bottom edge, <code>0.5f</code> is center.
If action is ocrFinder, this is used only for testing.");
		_info.InfoCO(wiflagsC,
@"Get pixels from window, not from screen.
The window can be in the background. Also with WindowDC faster.
Works not with all windows. Try WindowDC, then PrintWindow if fails.");
		_info.InfoCT(scaleC,
@"Scale factor. Value 2 or 3 may improve OCR results.
More info: <help>ocr.find<>.");
		_info.InfoCO(engineC,
@"OCR engine.
• <i>OcrWin10</i> - fast, poor accuracy, available on Windows 10 and later.
• <i>OcrTesseract</i> - slower, poor accuracy, available if Tesseract is installed, supports more languages.
• <i>OcrGoogleCloud</i> - slow, good accuracy, uses internet, need Google Cloud account, may be not free.
• <i>OcrMicrosoftAzure</i> - slowest, good accuracy, uses internet, need Microsoft Azure account, may be not free.

Normally you specify OCR engine once in script. If unchecked, will use the previously specified engine, or OcrWin10 if unspecified.");
		_info.Info(textC, "Find text", @"Text to find in OCR results. Can have prefix:
• <mono>**r<> - PCRE regular expression. Example: <mono>@""**r \bwhole words\b""<>
• <mono>**R<> - .NET regular expression.
• <mono>**i<> - case-insensitive.
• <mono>**t<> - case-sensitive (default).

Separate words with single space.");
		_info.InfoCT(skipC,
@"0-based index of matching text.
For example, if 1, finds the second matching text.");
		_info.Info(_cbAction, "Action", "Call this function when found. Or instead of <b>find<> call <b>waitNot<> or create new <b>ocrFinder<>.");
		_info.InfoCT(waitC, @"The wait timeout, seconds.
The function waits max this time interval. On timeout throws exception if <b>Fail...<> checked, else returns null. If empty, uses 8e88 (infinite).");
		_info.InfoCT(waitnoC, @"The wait timeout, seconds.
The function waits max this time interval. On timeout throws exception if <b>Fail...<> checked, else returns false. No timeout if unchecked or 0 or empty.");
		_info.InfoC(exceptionC,
@"Throw exception if not found.
If unchecked, returns null.");
		_info.InfoC(exceptionnoC,
@"Throw exception on timeout.
If unchecked, returns false.");
	}

	const string c_dialogInfo =
@"This tool creates <help>ocr.find<> code (OCR-extract <help wnd.find>window<> text and find).
1. Select a window or control. Test, OK.
2. Enter text to find.
3. Click the Test button to see how the 'find' code works.
4. If need, change some fields.
5. Click Insert. Click Close, or repeat 1-5 to create new code.
6. If need, edit the code in editor. For example rename variables, delete duplicate wnd.find lines, replace part of window name with *, add code to click the text.";

	#endregion

	#region OCR engine properties

	private void _bEngine_Click(WBButtonClickArgs e) {
		var g = App.Settings.ocr ?? new();

		var b = new wpfBuilder("OCR engine properties").WinSize(500);

		_StartGroupBox("OcrWin10");
		b.R.Add("Language", out ComboBox wLang, g.wLang).Editable().Tooltip("An installed language that supports OCR.\nIf empty, uses the default OCR language.");
		if (osVersion.minWin10) b.Items(true, cb => { try { cb.ItemsSource = new OcrWin10().AvailableLanguages.Select(o => o.tag); } catch { } });
		b.End();

		_StartGroupBox("OcrTesseract");
		//b.R.AddButton("Download", _=> run.itSafe("https://github.com/UB-Mannheim/tesseract/wiki")); //the link is in doc
		b.R.Add("Language", out ComboBox tLang, g.tLang).Editable().Tooltip("A language installed with Tesseract.\nIf empty, uses eng. Can be sevaral, like eng+deu.");
		b.Items(true, cb => { try { cb.ItemsSource = new OcrTesseract().AvailableLanguages; } catch { } });
		b.R.Add("Command line", out TextBox tCL, g.tCL).Tooltip("Additional tesseract.exe command line arguments.");
		b.End();

		_StartGroupBox("OcrGoogleCloud");
		b.R.Add("API key *", out TextBox gKey, g.gKey);
		b.R.Add("Features", out TextBox gFeat, g.gFeat).Multiline().Tooltip("Feature type, like TEXT_DETECTION, or JSON of features array content.\nIf empty, uses DOCUMENT_TEXT_DETECTION.");
		b.R.Add("Image context", out TextBox gIC, g.gIC).Multiline().Tooltip("JSON of imageContext.\nFor example can specify language (usually don't need).");
		b.End();

		_StartGroupBox("OcrMicrosoftAzure");
		b.R.Add("Endpoint URL *", out TextBox mUrl, g.mUrl);
		b.R.Add("API key *", out TextBox mKey, g.mKey);
		b.End();

		b.R.StartGrid().Columns(-1, 0);
		b.Add<Label>("* required");
		b.AddOkCancel();
		b.End();
		b.End();

		if (!b.ShowDialog(this)) return;

		g.wLang = _Text(wLang.Text);
		g.tLang = _Text(tLang.Text);
		g.tCL = _Text(tCL.Text);
		g.gKey = _Text(gKey.Text);
		g.gFeat = _Text(gFeat.Text);
		g.gIC = _Text(gIC.Text);
		g.mUrl = _Text(mUrl.Text);
		g.mKey = _Text(mKey.Text);

		static string _Text(string s) => s.Trim().NullIfEmpty_();

		App.Settings.ocr = g;
		_FormatCode();

		void _StartGroupBox(string engine) {
			var hl = new Hyperlink(new Run(engine));
			hl.Click += (_, _) => HelpUtil.AuHelp("Au.More." + engine);
			b.R.StartGrid<KGroupBox>(hl).Columns(100, -1);
		}
	}

	#endregion
}
