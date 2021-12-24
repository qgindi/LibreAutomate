using System.Windows.Controls;
using Au.Controls;

class PanelFound : DockPanel
{
	_KScintilla _c;

	public KScintilla ZControl => _c;

	public PanelFound() {
		//this.UiaSetName("Found panel"); //no UIA element for Panel. Use this in the future if this panel will be : UserControl.

		_c = new _KScintilla { Name = "Found_list" };
		_c.ZInitReadOnlyAlways = true;
		_c.ZInitTagsStyle = KScintilla.ZTagsStyle.AutoAlways;
		_c.ZAcceptsEnter = true;
		_c.ZHandleCreated += _c_ZHandleCreated;

		this.Children.Add(_c);
	}

	private void _c_ZHandleCreated() {
		_c.zSetMarginWidth(1, 0);
		_c.zStyleFont(Sci.STYLE_DEFAULT, App.Wmain);
		_c.zStyleClearAll();
		_c.ZTags.SetLinkStyle(new SciTags.UserDefinedStyle(), (false, default), false);
	}

	class _KScintilla : KScintilla
	{
		protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
			switch (msg) {
			case Api.WM_MBUTTONDOWN: //close file
				int pos = Call(Sci.SCI_POSITIONFROMPOINTCLOSE, Math2.LoShort(lParam), Math2.HiShort(lParam));
				if (ZTags.GetLinkFromPos(pos, out var tag, out var attr) && tag is "+f" or "+ra") {
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
