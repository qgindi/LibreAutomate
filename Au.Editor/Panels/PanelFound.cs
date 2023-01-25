using System.Windows.Controls;
using Au.Controls;

class PanelFound : DockPanel
{
	_KScintilla _c;

	public KScintilla aaControl => _c;

	public PanelFound() {
		//this.UiaSetName("Found panel"); //no UIA element for Panel. Use this in the future if this panel will be : UserControl.

		_c = new _KScintilla {
			Name = "Found_list",
			aaInitReadOnlyAlways = true,
			aaInitTagsStyle = KScintilla.aaTagsStyle.AutoAlways,
			aaUsesEnter = true
		};
		_c.aaHandleCreated += _c_aaHandleCreated;

		this.Children.Add(_c);
	}

	private void _c_aaHandleCreated() {
		_c.aaaSetMarginWidth(1, 0);
		_c.aaaStyleFont(Sci.STYLE_DEFAULT, App.Wmain);
		_c.aaaStyleClearAll();
		_c.aaTags.SetLinkStyle(new SciTags.UserDefinedStyle(), (false, default), false);
	}

	class _KScintilla : KScintilla
	{
		protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
			switch (msg) {
			case Api.WM_MBUTTONDOWN: //close file
				int pos = Call(Sci.SCI_POSITIONFROMPOINTCLOSE, Math2.LoShort(lParam), Math2.HiShort(lParam));
				if (aaTags.GetLinkFromPos(pos, out var tag, out var attr) && tag is "+f" or "+ra") {
					//print.it(tag, attr);
					var f = App.Model.Find(attr.Split(' ')[0]);
					if (f != null) App.Model.CloseFile(f, selectOther: true, focusEditor: true);
				}
				return default; //don't focus
			}
			return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
		}
	}
}
