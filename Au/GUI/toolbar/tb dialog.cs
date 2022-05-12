using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Au;

public partial class toolbar {
	/// <summary>
	/// Creates dialog with a list of toolbars of this thread. Just creates, does not show. Can be used to find lost toolbars.
	/// </summary>
	/// <param name="show">Show the dialog now, non-modal. If a dialog shown by this function already exists in this thread - activate.</param>
	public static Window toolbarsDialog(bool show = false) {
		if (show && s_dialog != null) {
			s_dialog.Hwnd().ActivateL(true);
			return s_dialog;
		}

		var b = new wpfBuilder("Active toolbars").WinSize(330, 300).Columns(-1, 0);
		b.Row(-1).Add(out ListBox lb).Focus();
		//b.StartStack(vertical: true);
		//b.AddButton(out var bMove, "X", e => _Click(2));
		//b.End();
		b.End();

		var window = b.Window;

		var osdr = new osdRect { Color = 0xff0000, Thickness = 12 };
		osdText osdt = null;
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
				patb = atb.Select(o => (o, o.Satellite?.IsOpen ?? false)).ToArray();
				_FillLB();
			}
		});

		window.Closed += (_, _) => {
			osdr.Dispose();
			timer1.Stop();
		};
		//lb.SelectionChanged += (_, _) => _EnableButtons(lb.SelectedIndex >= 0);

		_FillLB();

		void _FillLB() {
			lb.Items.Clear();
			foreach (var tb in _Manager._atb) {
				_AddLbItem(tb);
				var sat = tb.Satellite; if (sat?.IsOpen ?? false) _AddLbItem(sat);
			}
			//_EnableButtons(false);
		}

		void _AddLbItem(toolbar tb) {
			var li = new ListBoxItem { Content = tb };
			lb.Items.Add(li);
			li.MouseEnter += (_, _) => {
				if (tb.IsOpen) {
					var w = tb._w;
					var r = w.Rect;
					if (screen.isInAnyScreen(r)) {
						r.Inflate(10, 10);
						osdr.Rect = r;
						osdr.Show();
					} else {
						osdt = osdText.showText("The toolbar is offscreen.\nRectangle: " + r.ToString(), xy: PopupXY.Mouse);
					}
				} else {
					_FillLB();
				}
			};
			li.MouseLeave += (_, _) => {
				osdr.Hide();
				osdt?.Dispose(); osdt = null;
			};
			li.ContextMenuOpening += (_, _) => {
				var dlg = window.Hwnd();
				var m = new popupMenu();
				m["Edit\tD-click"] = o => _Edit();
				m["Move here"] = o => {
					if (!tb.IsOpen) return;
					var w = tb._w;
					if (!w.IsVisible && !dialog.showOkCancel("Hidden", "Move this hidden toolbar?", owner: dlg)) return;
					w.MoveL_(mouse.xy);
					if (!w.ZorderIsAbove(dlg)) w.ZorderAbove(dlg);
				};
				m.Show(owner: dlg);
			};
			li.MouseDoubleClick += (_, e) => {
				if (e.ChangedButton == System.Windows.Input.MouseButton.Left) _Edit();
			};
			void _Edit() {
				//window.Close();
				script.editor.OpenAndGoToLine(tb._sourceFile, tb._sourceLine);
				timer.after(100, _ => window.Hwnd().ZorderTop());
			}
		}

		//void _EnableButtons(bool enable) {
		//	//bX.IsEnabled = enable;
		//}

		//void _Click(int action) {
		//	if ((lb.SelectedItem as ListBoxItem)?.Content is not toolbar tb) return;
		//}

		b.Loaded += () => {
			window.Hwnd().ActivateL();
		};

		if (show) {
			window.Show();
			window.Closed += (_, _) => { s_dialog = null; };
			s_dialog = window;
		}

		return window;
	}
	[ThreadStatic] static Window s_dialog;
}
