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
					s_rx1 ??= new regexp(@"(?m)^\[(.+?)(\((\d+),(\d+)\))?\]: ((?:error|warning) \w+)");
					m.Text = s_rx1.Replace(s, x => {
						var f = App.Model?.FindByFilePath(x[1].Value);
						if (f == null) return x[0].Value;
						var sEW = x[5].Value;
						return $"<open {f.IdStringWithWorkspace}|{x[3].Value}|{x[4].Value}>{f.Name}{x[2].Value}<>: <c {(sEW[0] == 'e' ? "red" : "green")}>{sEW}<>";
					});
				} else if ((i = s.Find("\n   at ") + 1) > 0 && s.Find(":line ", i) > 0) { //stack trace with source file info
					var b = _sb ??= new StringBuilder(s.Length + 2000);
					b.Clear();
					//print.qm2.write("'" + s + "'");
					int iLiteral = 0;
					if (!s.Starts("<>")) b.Append("<>");
					else {
						iLiteral = i - 1; if (s[iLiteral - 1] == '\r') iLiteral--;
						if (0 == s.Eq(iLiteral -= 3, false, "<_>", "<\a>")) iLiteral = 0;
					}
					if (iLiteral > 0) b.Append(s, 0, iLiteral).AppendLine(); else b.Append(s, 0, i);
					s_rx2 ??= new regexp(@" in (.+?):line (?=\d+$)");
					bool replaced = false, isMain = false;
					int stackEnd = s.Length/*, stackEnd2 = 0*/;
					foreach (var k in s.Lines(i..)) {
						//print.qm2.write("'"+k+"'");
						if (s.Eq(k.start, "   at ")) {
							if (isMain) {
								//if(stackEnd2 == 0 && s.Eq(k.start, "   at A.Main(String[] args) in ")) stackEnd2 = k.start; //rejected. In some cases may cut something important.
								continue;
							}
							if (!s_rx2.Match(s, 1, out RXGroup g, (k.start + 6)..k.end)) continue; //note: no "   at " if this is an inner exception marker. Also in aggregate exception stack trace.
							var f = App.Model?.FindByFilePath(g.Value); if (f == null) continue;
							int i1 = g.End + 6, len1 = k.end - i1;
							b.Append("   at ")
							.Append("<open ").Append(f.IdStringWithWorkspace).Append('|').Append(s, i1, len1).Append('>')
							.Append("line ").Append(s, i1, len1).Append("<> in <bc #FAFAD2>").Append(f.Name).Append("<>");
							
							isMain
								= s.Eq(k.start, "   at Program.<Main>$(String[] args) in ") //top-level statements
								|| s.Eq(k.start, "   at Program..ctor(String[] args) in ")
								|| s.Eq(k.start, "   at Script..ctor(String[] args) in ");
							if (!isMain || !f.IsScript) b.Append(", <\a>").Append(s, k.start + 6, g.Start - k.start - 10).Append("</\a>");
							b.AppendLine();
							
							replaced = true;
						} else if (!(s.Eq(k.start, "   ---") || s.Eq(k.start, "---"))) {
							stackEnd = k.start;
							break;
						}
					}
					if (replaced) {
						int j = stackEnd; //int j = stackEnd2 > 0 ? stackEnd2 : stackEnd;
						if (s[j - 1] == '\n') { if (s[--j - 1] == '\r') j--; }
						b.Append("   <fold><\a>   --- Raw stack trace ---\r\n").Append(s, i, j - i).Append("</\a></fold>");
						if (iLiteral > 0 && 0 != s.Eq(stackEnd, false, "</_>", "</\a")) stackEnd += 4;
						int more = s.Length - stackEnd;
						if (more > 0) {
							if (!s.Eq(stackEnd, "</fold>")) b.AppendLine();
							b.Append(s, stackEnd, more);
						}
						m.Text = b.ToString();
						//print.qm2.write("'" + m.Text + "'");
					}
					if (_sb.Capacity > 10_000) _sb = null; //let GC free it. Usually < 4000.
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
		static regexp s_rx1, s_rx2;
		
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
	}
}
