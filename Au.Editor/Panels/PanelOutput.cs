using System.Windows.Controls;
using Au.Controls;
using static Au.Controls.Sci;

class PanelOutput {
	readonly KScintilla_ _c;
	readonly KPanels.ILeaf _leaf;
	readonly Queue<PrintServerMessage> _history;
	
	public KScintilla_ Scintilla => _c;
	
	public PanelOutput() {
		//P.UiaSetName("Output panel"); //no UIA element for Panel
		
		_c = new KScintilla_(this) { Name = "Output_text" };
		P.Children.Add(_c);
		_history = new Queue<PrintServerMessage>();
		App.Commands.BindKeysTarget(P, "Output");
		_leaf = Panels.PanelManager["Output"];
	}
	
	public DockPanel P { get; } = new();
	
	public void Clear() { _c.aaaClearText(); _c.Call(SCI_SETSCROLLWIDTH, 1); }
	
	public void Copy() { _c.Call(SCI_COPY); }
	
	//public void Find() { Panels.Find.CtrlF(_c); }
	
	public void History() {
		var p = new KPopupListBox { PlacementTarget = P, Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
		p.Control.ItemsSource = _history;
		p.OK += o => print.it((o as PrintServerMessage).Text);
		P.Dispatcher.InvokeAsync(() => p.IsOpen = true);
	}
	
	void _c_HandleCreated() {
		_inInitSettings = true;
		if (WrapLines) WrapLines = true;
		if (WhiteSpace) WhiteSpace = true;
		_c.AaNoMouseSetFocus = MButtons.Middle;
		_inInitSettings = false;
	}
	bool _inInitSettings;
	
	public bool WrapLines {
		get => App.Settings.output_wrap;
		set {
			Debug.Assert(!_inInitSettings || value);
			if (!_inInitSettings) App.Settings.output_wrap = value;
			_c.Call(SCI_SETWRAPMODE, value ? SC_WRAP_WORD : 0);
			App.Commands[nameof(Menus.Tools.Output.Output_wrap_lines)].Checked = value;
		}
	}
	
	public bool WhiteSpace {
		get => App.Settings.output_white;
		set {
			Debug.Assert(!_inInitSettings || value);
			if (!_inInitSettings) App.Settings.output_white = value;
			_c.Call(SCI_SETVIEWWS, value);
			App.Commands[nameof(Menus.Tools.Output.Output_white_space)].Checked = value;
		}
	}
	
	internal class KScintilla_ : KScintilla {
		PanelOutput _p;
		StringBuilder _sb;
		
		internal KScintilla_(PanelOutput panel) {
			_p = panel;
			
			AaInitReadOnlyAlways = true;
			AaInitTagsStyle = AaTagsStyle.AutoWithPrefix;
			AaInitImages = true;
			
			//App.Commands[nameof(Menus.Tools.Output)].SetKeysTarget(this);
		}
		
		protected override void AaOnHandleCreated() {
			aaaMarginSetWidth(1, 3);
			AaSetStyles();
			_p._c_HandleCreated();
			
			AaTags.CodeStylesProvider = CiUtil.GetScintillaStylingBytes8;
			
			SciTags.AddCommonLinkTag("open", _OpenLink);
			SciTags.AddCommonLinkTag("script", _RunScript);
			SciTags.AddCommonLinkTag("google", _Google);
			SciTags.AddCommonLinkTag("+recipe", Panels.Cookbook.OpenRecipe);
			SciTags.AddCommonLinkTag("+nuget", DNuget.ShowSingle);
			SciTags.AddCommonLinkTag("+options", s => { DOptions.AaShow(s.NE() ? null : Enum.Parse<DOptions.EPage>(s)); });
			AaTags.AddLinkTag("+properties", fid => {
				var f = App.Model.FindCodeFile(fid);
				if (f == null || !App.Model.SetCurrentFile(f)) return;
				Menus.File.Properties();
			});
			AaTags.AddLinkTag("+DCustomize", DCustomize.ShowSingle);
			
			App.PrintServer.SetNotifications(AaWnd, Api.WM_APP);
			
			base.AaOnHandleCreated();
		}
		
		public void AaSetStyles() {
			var t = CiStyling.TTheme.Default with {
				FontName = App.Settings.font_output.name,
				FontSize = App.Settings.font_output.size,
				Background = 0xF7F7F7,
			};
			t.ToScintilla(this);
			Call(SCI_SETWHITESPACESIZE, 2); //not DPI-scaled
		}
		
		protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
			//WndUtil.PrintMsg(out var s, default, msg, wParam, lParam); print.qm2.write(s);
			switch (msg) {
			case Api.WM_APP:
				AaTags.PrintServerProcessMessages(App.PrintServer, _onServerMessage ??= _OnServerMessage);
				return default;
			case Api.WM_MBUTTONDOWN:
				_p.Clear();
				return default;
			case Api.WM_CONTEXTMENU:
				var m = new ContextMenu { PlacementTarget = this };
				App.Commands[nameof(Menus.Tools.Output)].CopyToMenu(m);
				m.IsOpen = true;
				return default;
			}
			return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
		}
		
		Action<PrintServerMessage> _onServerMessage;
		void _OnServerMessage(PrintServerMessage m) {
			if (m.Type != PrintServerMessageType.Write) {
				if (m.Type == PrintServerMessageType.TaskEvent) RecentTT.TriggerEvent(m);
				return;
			}
			
			//create links in compilation errors/warnings or run-time stack trace
			var s = m.Text; int i;
			if (s.Length >= 22) {
				if (s.Starts("<><lc #") && (s.Eq(13, ">Compilation: ") || s.Eq(13, ">Can't export "))) { //compilation. Or error in meta while exporting.
					s_rxCompError ??= new regexp(@"(?m)^\[(.+?)(\((\d+),(\d+)\))?\]: ((?:error|warning) \w+)");
					m.Text = s_rxCompError.Replace(s, x => {
						var f = App.Model?.FindByFilePath(x[1].Value);
						if (f == null) return x[0].Value;
						var sEW = x[5].Value;
						return $"<open {f.IdStringWithWorkspace}|{x[3].Value}|{x[4].Value}>{f.Name}{x[2].Value}<>: <c {(sEW[0] == 'e' ? "red" : "green")}>{sEW}<>";
					});
				} else if ((i = s.Find("   at ") + 1) >= 0 && s.Find(":line ", i) > 0) { //stack trace with source file info
					_ModifyStackTraces(m);
				}
			}
			
			if (s.Length <= 10_000) { //* 50 = 1 MB
				if (!ReferenceEquals(s, m.Text)) m = new PrintServerMessage(PrintServerMessageType.Write, s, m.TimeUtc, m.Caller);
				var h = _p._history;
				h.Enqueue(m);
				if (h.Count > 50) h.Dequeue();
			}
			
			_p._leaf.Visible = true;
		}
		static regexp s_rxCompError;
		
		static void _OpenLink(string s) {
			var a = s.Split('|');
			if (a.Length > 3) App.Model.OpenAndGoTo3(a[0], a[3]);
			else App.Model.OpenAndGoTo2(a[0], a.Length > 1 ? a[1] : null, a.Length > 2 ? a[2] : null);
		}
		
		static void _RunScript(string s) {
			var a = s.Split('|');
			var f = App.Model.FindCodeFile(a[0]); if (f == null) return;
			CompileRun.CompileAndRun(true, f, a.Length == 1 ? null : a.RemoveAt(0));
		}
		
		static void _Google(string s) {
			var a = s.Split('|');
			string s1 = a[0], s2 = a.Length > 1 ? a[1] : null;
			run.itSafe(App.Settings.internetSearchUrl + System.Net.WebUtility.UrlEncode(s1) + s2);
		}
		
		void _ModifyStackTraces(PrintServerMessage psm) {
			string s = psm.Text;
			//print.qm2.write("`" + s + "`");
			var b = _sb ??= new StringBuilder(s.Length + 2000);
			b.Clear();
			bool wasFormatted = s.Starts("<>");
			RXGroup[] aLit = null;
			if (wasFormatted) (s_rxLiteral ??= new regexp(@"(?s)<([\a_])>.+?</\1>")).FindAllG(s, 0, out aLit);
			else b.Append("<><\a>");
			int appendFrom = 0;
			
			//for each stack trace
			s_rxStack ??= new regexp(@"(?m)(^   at .+(?:\R|\z))+(^ *--.*\R(?1)+)*");
			foreach (var g1 in s_rxStack.FindAllG(s, 0)) {
				int start = g1.Start, end = g1.End;
				if (s[end - 1] == '\n' && s[--end - 1] == '\r') end--;
				bool inLiteral = true;
				if (wasFormatted) {
					if (s[end - 1] == '>') //don't include eg `<\a><>` at the end of the last line
						if ((s_rxClosingTags ??= new regexp(@"(?:<(?:/.+?)?>)+$")).Match(s, 0, out RXGroup g2, s.LastIndexOf('\n', end - 1)..end)) end = g2.Start;
					inLiteral = aLit?.Any(o => o.Start < start && o.End > end) ?? false;
				}
				if (s.Find("<\a>", start..end) >= 0 || s.Find("</\a>", start..end) >= 0 || s.Find("<_>", start..end) >= 0 || s.Find("</_>", start..end) >= 0) continue;
				
				b.Append(s, appendFrom, start - appendFrom); appendFrom = end;
				
				//for each line that has source file info
				bool modified = false;
				s_rxAtLine ??= new regexp(@"(?m)^   at (.+?\)) in ([^\r\n<]+):line (\d+)$");
				foreach (var m in s_rxAtLine.FindAll(s, start..end)) {
					var f = App.Model?.FindByFilePath(m[2].Value); if (f == null) continue;
					b.AppendFormat("{3}   at <open {0}|{1}>line {1}<> in <bc #FAFAD2>{2}<>{4}", f.IdStringWithWorkspace, m[3].Value, f.Name, inLiteral ? "</\a>" : "", inLiteral ? "<\a>" : "");
					bool isMain = f.IsScript && 0 != s.Eq(m[1].Start, false, "Program.<Main>$(String[] args)", "Program..ctor(String[] args)", "Script..ctor(String[] args)");
					if (!isMain) {
						var method = m[1].Value;
						bool lit = !inLiteral && method.Contains('<');
						b.AppendFormat("{1}, {0}{2}", method, lit ? "<\a>" : "", lit ? "</\a>" : "");
					}
					b.AppendLine();
					modified = true;
				}
				
				if (modified) { //append `<fold>raw stack trace</fold>`
					if (b[b.Length - 1] != '\n') b.AppendLine();
					if (inLiteral) b.Append("</\a>");
					b.Append("   <fold><\a>   --- Raw stack trace ---\r\n").Append(s, start, end - start);
					if (b[b.Length - 1] != '\n') b.AppendLine();
					b.Append("</\a></fold>");
					if (inLiteral) b.Append("<\a>");
				} else { //append raw stack trace
					if (!inLiteral) b.Append("<\a>");
					b.Append(s, start, end - start);
					if (!inLiteral) b.Append("</\a>");
				}
			}
			if (appendFrom > 0) {
				b.Append(s, appendFrom, s.Length - appendFrom);
				if (!wasFormatted) b.Append("</\a>");
				//print.qm2.write("+" + b);
				psm.Text = b.ToString();
			}
			if (_sb.Capacity > 10_000) _sb = null; //let GC free it. Usually < 4000.
		}
		static regexp s_rxStack, s_rxClosingTags, s_rxAtLine, s_rxLiteral;
	}
}
