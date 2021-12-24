
using System.Windows;
using System.Windows.Controls;

namespace Au
{
	public partial class toolbar
	{
		/// <summary>
		/// Creates dialog with a list of toolbars of this thread. Just creates, does not show. Can be used to find lost toolbars.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// toolbar.toolbarsDialog().Show();
		/// ]]></code>
		/// </example>
		public static Window toolbarsDialog() {
			//		var s = script.name;
			//		s = s == "Triggers and toolbars" ? "Toolbars" : "Toolbars of " + s;

			var b = new wpfBuilder("Toolbars").WinSize(400, 250).Columns(-1, 0);
			b.Row(-1).Add(out ListBox lb).Focus();
			b.StartStack(vertical: true);
			b.AddButton(out var bShow, "Show rectangle", e => _Click(1));
			b.AddButton(out var bMove, "Move here", e => _Click(2));
			b.AddButton("Refresh", e => _FillLB());
			b.End().End();

			_FillLB();

			lb.SelectionChanged += (_, _) => _EnableButtons(lb.SelectedIndex >= 0);

			void _FillLB() {
				lb.Items.Clear();
				foreach (var tb in _Manager._atb) {
					lb.Items.Add(tb);
					var sat = tb.Satellite; if (sat?.IsOpen ?? false) lb.Items.Add(sat);
				}
				_EnableButtons(false);
			}

			void _EnableButtons(bool enable) {
				bShow.IsEnabled = enable;
				bMove.IsEnabled = enable;
			}

			void _Click(int action) {
				if (lb.SelectedItem is not toolbar tb) return;
				var dlg = b.Window.Hwnd();
				if (!tb.IsOpen) {
					dialog.show("Closed", owner: dlg);
					return;
				}
				var w = tb._w;
				var r = w.Rect;
				if (action == 1) {
					if (screen.isInAnyScreen(r)) {
						r.Inflate(2, 2);
						var osd = new osdRect { Rect = r, Color = 0xff0000 };
						osd.Show();
						timer.after(1000, _ => osd.Dispose());
					} else {
						dialog.show("Offscreen", "Rectangle: " + r.ToString(), owner: dlg);
					}
				} else if (action == 2) {
					if (!w.IsVisible && !dialog.showOkCancel("Hidden", "Move this hidden toolbar?", owner: dlg)) return;
					w.MoveL_(mouse.xy);
					if (!w.ZorderIsAbove(dlg)) w.ZorderAbove(dlg);
				}
			}

			b.Loaded += () => {
				b.Window.Hwnd().ActivateL();
			};

			return b.Window;
		}
	}
}
