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
		if (Topmost) App.Commands[nameof(Menus.Tools.Output.Output_topmost_when_floating)].Checked = true; //see also OnParentChanged, below
		_c.AaNoMouseSetFocus = MButtons.Middle;
		_inInitSettings = false;
		_leaf.FloatingChanged += (_, floating) => _SetTopmost(floating ? 1 : 0);
	}
	bool _inInitSettings;
	
	public bool WrapLines {
		get => App.Settings.output_wrap;
		set {
			Debug.Assert(!_inInitSettings || value);
			if (!_inInitSettings) App.Settings.output_wrap = value;
			//_c.Call(SCI_SETWRAPVISUALFLAGS, SC_WRAPVISUALFLAG_START | SC_WRAPVISUALFLAG_END); //in KScintilla.aaOnHandleCreated
			//_c.Call(SCI_SETWRAPINDENTMODE, SC_WRAPINDENT_INDENT); //in KScintilla.aaOnHandleCreated
			_c.Call(SCI_SETWRAPMODE, value ? SC_WRAP_WORD : 0);
			App.Commands[nameof(Menus.Tools.Output.Output_wrap_lines)].Checked = value;
		}
	}
	
	public bool WhiteSpace {
		get => App.Settings.output_white;
		set {
			Debug.Assert(!_inInitSettings || value);
			if (!_inInitSettings) App.Settings.output_white = value;
			_c.Call(SCI_SETWHITESPACEFORE, 1, 0xFF0080);
			_c.Call(SCI_SETVIEWWS, value);
			App.Commands[nameof(Menus.Tools.Output.Output_white_space)].Checked = value;
		}
	}
	
	public bool Topmost {
		get => App.Settings.output_topmost;
		set {
			App.Settings.output_topmost = value;
			App.Commands[nameof(Menus.Tools.Output.Output_topmost_when_floating)].Checked = value;
			if (_leaf.Floating) _SetTopmost(2);
		}
	}
	
	//action: 0 dock, 1 undock, 2 changed while floating
	void _SetTopmost(int action) {
		var w = P.Hwnd().Window;
		if (action >= 1) {
			if (Topmost) {
				WndUtil.SetOwnerWindow(w, default);
				w.ZorderTopmost();
				//w.SetExStyle(WSE.APPWINDOW, SetAddRemove.Add);
				//wnd.getwnd.root.ActivateL(); w.ActivateL(); //let taskbar add button
			} else if (action == 2) {
				w.ZorderNoTopmost();
				WndUtil.SetOwnerWindow(w, App.Hmain);
			}
		}
		
		//Windows bug: sometimes the floating/topmost output panel becomes behind normal windows, although has topmost style. Until clicked.
		//	To reproduce: in a ribbon show a dropdown, eg a popup menu. If can't reproduce, try others.
		bool needTimer = Topmost && action >= 1;
		if (needTimer != _workaround1.isTimer) {
			if (_workaround1.isTimer = needTimer) App.Timer1s += _WorkaroundTimer; else App.Timer1s -= _WorkaroundTimer;
			if (needTimer) _workaround1.w = w;
		}
		void _WorkaroundTimer() {
			//print.it("timer");
			var wa = wnd.active;
			if (!wa.Is0 && !wa.IsTopmost) {
				var ww = _workaround1.w;
				if (!ww.ZorderIsAbove(wa)) {
					//print.it("workaround");
					ww.ZorderTop();
				}
			}
		}
	}
	(bool isTimer, wnd w) _workaround1;
	
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
			_p._c_HandleCreated();
			aaaMarginSetWidth(1, 3);
			
			AaSetStyles();
			AaTags.CodeStylesProvider = CiUtil.GetScintillaStylingBytes;
			
			SciTags.AddCommonLinkTag("open", _OpenLink);
			SciTags.AddCommonLinkTag("script", _RunScript);
			SciTags.AddCommonLinkTag("google", _Google);
			AaTags.AddLinkTag("+properties", fid => {
				var f = App.Model.FindCodeFile(fid);
				if (f == null || !App.Model.SetCurrentFile(f)) return;
				Menus.File.Properties();
			});
			AaTags.AddLinkTag("+DCustomize", DCustomize.ShowSingle);
			AaTags.AddLinkTag("+nuget", DNuget.ShowSingle);
			
			App.PrintServer.SetNotifications(AaWnd, Api.WM_APP);
			
			base.AaOnHandleCreated();
		}
		
		public void AaSetStyles() {
			var styles = new CiStyling.TStyles(customized: false) {
				BackgroundColor = 0xF7F7F7,
				FontName = App.Settings.font_output.name,
				FontSize = App.Settings.font_output.size
			};
			styles.ToScintilla(this);
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
				if (s.Starts("<><lc #") && s.Eq(13, ">Compilation: ")) { //compilation
					s_rx1 ??= new regexp(@"(?m)^\[(.+?)(\((\d+),(\d+)\))?\]: ");
					m.Text = s_rx1.Replace(s, x => {
						var f = App.Model?.FindByFilePath(x[1].Value);
						if (f == null) return x[0].Value;
						return $"<open {f.IdStringWithWorkspace}|{x[3].Value}|{x[4].Value}>{f.Name}{x[2].Value}<>: ";
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
			
			_p._leaf.Visible = true; //TODO3: if(App.Hwnd.IsVisible) ?
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
