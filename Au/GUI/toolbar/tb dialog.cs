//TODO: sometimes the first time shows the window with several s delay. Can't repro, even after reboot. Possibly more likely after hibernation.

using System.Windows.Forms;

namespace Au;

public partial class toolbar {
	[ThreadStatic] static Form s_listWindow;
	
	/// <summary>
	/// Creates a window with a list of toolbars of this thread. Can be used to find lost toolbars.
	/// </summary>
	/// <param name="show">Show the window now, non-modal. If a window shown by this function already exists in this thread - activate it.</param>
	public static Form toolbarsDialog(bool show = true) {
		perf.next();//TODO
		if (show && s_listWindow != null) {
			s_listWindow.Hwnd().ActivateL(true);
			return s_listWindow;
		}
		
		var ico1 = icon.ofThisApp();
		perf.next('i');//TODO
		var ico2 = ico1?.ToGdipIcon();
		perf.next('g');//TODO
		var f = new Form();
		perf.next('F');//TODO
		f.Text = "Active toolbars";
		f.Size = new(330, 330);
		f.AutoScaleMode = AutoScaleMode.Dpi;
		f.StartPosition = FormStartPosition.CenterScreen;
		f.Icon = icon.ofThisApp()?.ToGdipIcon();
		//var f = new Form {
		//	Text = "Active toolbars",
		//	Size = new(330, 330),
		//	AutoScaleMode = AutoScaleMode.Dpi,
		//	StartPosition = FormStartPosition.CenterScreen,
		//	Icon = icon.ofThisApp()?.ToGdipIcon()
		//};
		perf.next('f');//TODO
		//f.Load += (_, _) => { f.Hwnd().ActivateL(); };
		//f.Load += (_, _) => {using var p1 = perf.local();f.Hwnd().ActivateL(); };//TODO
		f.Load += (_, _) => {perf.next('1');f.Hwnd().ActivateL();perf.next('2'); };//TODO
		
		var lv = new ListView {
			Dock = DockStyle.Fill,
			View = View.List,
			BorderStyle = BorderStyle.None,
			MultiSelect = false,
			ContextMenuStrip = new()
		};
		f.Controls.Add(lv);
		perf.next();//TODO
		
		var osdr = new osdRect { Color = 0xff0000, Thickness = 12 };
		osdText osdt = null;
		ListViewItem _osdItem = null;
		
		var atb = _Manager._atb;
		(toolbar tb, bool sat)[] patb = atb.Select(o => (o, o.Satellite?.IsOpen ?? false)).ToArray();
		var timer1 = timer.every(250, _ => {
			bool changed = atb.Count != patb.Length;
			if (!changed) {
				for (int i = 0; i < atb.Count; i++) {
					if (atb[i] != patb[i].tb || (atb[i].Satellite?.IsOpen ?? false) != patb[i].sat) { changed = true; break; }
				}
			}
			if (changed) {
				_HideRect();
				patb = atb.Select(o => (o, o.Satellite?.IsOpen ?? false)).ToArray();
				_FillList();
			}
		});
		
		f.FormClosed += (_, _) => {
			if (show) s_listWindow = null;
			osdr.Dispose();
			timer1.Stop();
		};
		
		perf.next();//TODO
		_FillList();
		perf.next('L');//TODO
		
		void _FillList() {
			lv.Items.Clear();
			foreach (var tb in _Manager._atb) {
				_Add(tb);
				if (tb.Satellite is { } sat && sat.IsOpen) _Add(sat);
			}
			void _Add(toolbar tb) {
				lv.Items.Add(tb.ToString()).Tag = tb;
			}
		}
		
		lv.MouseMove += (_, _) => { //note: MouseHover not always works
			var p = mouse.xy;
			lv.Hwnd().MapScreenToClient(ref p);
			var v = lv.HitTest(p).Item;
			if (v == _osdItem) return;
			if (v != null) {
				var tb = v.Tag as toolbar;
				if (tb.IsOpen) {
					var w = tb._w;
					var r = w.Rect;
					if (screen.isInAnyScreen(r)) {
						r.Inflate(10, 10);
						osdr.Rect = r;
						osdr.Show();
					} else {
						osdt = osdText.showText($"The toolbar is offscreen. Right-click to move.\nRectangle: {r}", xy: PopupXY.Mouse);
					}
					v.Selected = true;
					v.Focused = true;
					_osdItem = v;
				} else {
					_HideRect();
					_FillList();
				}
			} else {
				_HideRect();
			}
		};
		lv.MouseLeave += (_, _) => _HideRect();
		
		void _HideRect() {
			if (_osdItem != null) {
				_osdItem = null;
				osdr.Hide();
				osdt?.Dispose(); osdt = null;
			}
		}
		
		lv.ItemActivate += (_, _) => {
			_Edit(lv.FocusedItem.Tag as toolbar);
		};
		
		void _Edit(toolbar tb) {
			ScriptEditor.Open(tb._sourceFile, tb._sourceLine);
			timer.after(100, _ => f.Hwnd().ZorderTop());
		}
		
		lv.ContextMenuStrip.Opening += (_, e) => {
			e.Cancel = true;
			if (lv.SelectedItems.Count == 0) return;
			_HideRect();
			var tb = lv.FocusedItem.Tag as toolbar;
			var w = f.Hwnd();
			var m = new popupMenu();
			m["Edit\tD-click"] = o => _Edit(tb);
			m["Move here"] = o => {
				if (!tb.IsOpen) return;
				var w = tb._w;
				if (!w.IsVisible && !dialog.showOkCancel("Hidden", "Move this hidden toolbar?", owner: w)) return;
				w.MoveL_(mouse.xy);
				if (!w.ZorderIsAbove(w)) w.ZorderAbove(w);
			};
			m.Show(owner: w);
		};
		
		perf.next();//TODO
		if (show) f.Show();
		perf.nw();//TODO
		return s_listWindow = f;
	}
}
