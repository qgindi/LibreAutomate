using System.Windows.Controls;
using Au.Controls;

class PanelFound {
	_KScintilla _c;

	public KScintilla Scintilla => _c;

	public PanelFound() {
		//P.UiaSetName("Found panel"); //no UIA element for Panel

		_c = new _KScintilla {
			Name = "Found_list",
			AaInitReadOnlyAlways = true,
			AaInitTagsStyle = KScintilla.AaTagsStyle.AutoAlways,
			AaUsesEnter = true
		};
		_c.AaHandleCreated += _c_aaHandleCreated;

		P.Children.Add(_c);
	}

	public DockPanel P { get; } = new();

	private void _c_aaHandleCreated() {
		_c.aaaSetMarginWidth(1, 0);
		_c.aaaStyleFont(Sci.STYLE_DEFAULT, App.Wmain);
		_c.aaaStyleClearAll();
		_c.AaTags.SetLinkStyle(new SciTags.UserDefinedStyle(), (false, default), false);
	}

	class _KScintilla : KScintilla {
		protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
			switch (msg) {
			case Api.WM_MBUTTONDOWN: //close file
				int pos = Call(Sci.SCI_POSITIONFROMPOINTCLOSE, Math2.LoShort(lParam), Math2.HiShort(lParam));
				if (AaTags.GetLinkFromPos(pos, out var tag, out var attr) && tag is "+f" or "+ra") {
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
