using Au.Controls;
using System.Windows;
using System.Windows.Controls;

class PanelMouse {
	KScintilla _sci;
	POINT _prevXY;
	wnd _prevWnd;
	string _prevWndName;
	int _prevCounter;
	
	public PanelMouse() {
		//P.UiaSetName("Mouse panel"); //no UIA element for Panel
		
		_sci = new KScintilla_(this) { Name = "Mouse_info" };
		P.Children.Add(_sci);
	}
	
	public Grid P { get; } = new();
	
	void _MouseInfo() {
		//using var p1 = perf.local();
		if (!P.IsVisible) return;
		
		var p = mouse.xy;
		if (p == _prevXY && ++_prevCounter < 4) return; _prevCounter = 0; //use less CPU. c and wName rarely change when same p.
		var c = wnd.fromXY(p);
		//p1.Next();
		var w = c.Window;
		string wName = w.Name;
		if (p == _prevXY && c == _prevWnd && wName == _prevWndName) return;
		_prevXY = p;
		_prevWnd = c;
		_prevWndName = wName;
		
		string lineSep = App.Settings.mouse_singleLine ? "    ..    " : "\r\n";
		int limit = Math.Clamp(App.Settings.mouse_limitText, 20, 10000);
		
		//p1.Next();
		using (new StringBuilder_(out var b)) {
			var cn = w.ClassName;
			if (cn != null) {
				var pc = p; w.MapScreenToClient(ref pc);
				b.AppendFormat("<b>Mouse</b> {0,5} {1,5}  .  <b>in window</b> {2,5} {3,5}    ..    <b>Program</b>  {4}",
					p.x, p.y, pc.x, pc.y, w.ProgramName?.Escape(limit));
				if (c.UacAccessDenied) b.Append(" <c red>(admin)<>");
				b.AppendFormat("{0}<b>Window   ", lineSep);
				var name = wName?.Escape(limit); if (!name.NE()) b.AppendFormat("</b>{0}  .  <b>", name);
				b.Append("cn</b>  ").Append(cn.Escape(limit));
				if (c != w) {
					b.AppendFormat("{0}<b>Control   id</b>  {1}  .  <b>cn</b>  {2}",
						lineSep, c.ControlId, c.ClassName?.Escape(limit));
					var ct = c.Name;
					if (!ct.NE()) b.Append("  .  <b>name</b>  ").Append(ct.Escape(limit));
				} else if (cn == "#32768") {
					var m = MenuItemInfo.FromXY(p, w, 50);
					if (m != null) {
						b.AppendFormat("{0}<b>Menu   id</b>  {1}", lineSep, m.ItemId);
						if (m.IsSystem) b.Append(" (system)");
						//print.it(m.GetText(true, true));
					}
				}
				
				//rejected. Makes this func 5 times slower.
				//var color = CaptureScreen.Pixel(p);
			}
			var s = b.ToString();
			//p1.Next();
			_sci.aaaSetText(s);
		}
	}
	
	//public void SetMouseInfoText(string text)
	//{
	//	if(Dispatcher.Thread == Thread.CurrentThread) _SetMouseInfoText(text);
	//	else Dispatcher.InvokeAsync(() => _SetMouseInfoText(text));
	//
	//	void _SetMouseInfoText(string text) { _sci.aaaSetText(text); }
	//}
	
	internal class KScintilla_ : KScintilla {
		PanelMouse _p;
		
		internal KScintilla_(PanelMouse panel) {
			_p = panel;
			
			AaInitReadOnlyAlways = true;
			AaInitTagsStyle = KScintilla.AaTagsStyle.AutoAlways;
		}
		
		protected override void AaOnHandleCreated() {
			aaaStyleBackColor(Sci.STYLE_DEFAULT, 0xF0F0F0);
			aaaStyleFont(Sci.STYLE_DEFAULT, App.Wmain);
			aaaMarginSetWidth(1, 4);
			aaaStyleClearAll();
			Call(Sci.SCI_SETHSCROLLBAR);
			Call(Sci.SCI_SETVSCROLLBAR);
			Call(Sci.SCI_SETWRAPMODE, Sci.SC_WRAP_WORD);
			
			App.Timer025sWhenVisible += _p._MouseInfo;
		}
		
		protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
			//WndUtil.PrintMsg(out var s, default, msg, wParam, lParam); print.qm2.write(s);
			switch (msg) {
			case Api.WM_CONTEXTMENU:
				var m = new popupMenu();
				m.AddCheck("Single line", App.Settings.mouse_singleLine, o => { App.Settings.mouse_singleLine = o.IsChecked; });
				m["Text length..."] = o => { if (dialog.showInputNumber(out int i, "Mouse panel", "Maximal length of name, cn and program strings.", editText: App.Settings.mouse_limitText, owner: Handle)) App.Settings.mouse_limitText = i > 0 ? i : 100; };
				m.Show(owner: Handle);
				return default;
			}
			return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
		}
	}
}
