using Au.Controls;
using System.Windows.Controls;
using System.Windows;

namespace Au.Tools
{
	/// <summary>
	/// <see cref="KPopup"/>-based info window with 1 or 2 scintilla controls (<see cref="KSciInfoBox"/>) with output tags etc.
	/// You can set text, resize and show/hide/dispose it many times.
	/// User can middle-click to hide.
	/// </summary>
	class InfoWindow : KPopup
	{
		DockPanel _panel;
		_InfoBox _c, _c2;

		/// <param name="split">If not 0, sets <b>Control1.Width</b>=<i>split</i> and adds <b>Control2</b>.</param>
		/// <param name="caption">With caption.</param>
		public InfoWindow(int split, bool caption = true) : base(caption ? WS.THICKFRAME | WS.POPUP | WS.CAPTION | WS.SYSMENU : WS.THICKFRAME | WS.POPUP) {
			Content = _panel = new();
			_panel.Children.Add(_c = new());
			if (split > 0) {
				_c.Width = split;
				_panel.Children.Add(_c2 = new() { Name = "info_2" });
			}
		}

		/// <summary>
		/// The child control. Displays text.
		/// </summary>
		public KSciInfoBox Control1 => _c;

		/// <summary>
		/// The second child control. Displays text.
		/// </summary>
		public KSciInfoBox Control2 => _c2;

		/// <summary>
		/// Text with output tags.
		/// </summary>
		public string Text {
			get => _c?.zText;
			set => Control1.zText = value;
		}

		/// <summary>
		/// Text of second control with output tags.
		/// </summary>
		public string Text2 {
			get => _c2?.zText;
			set => Control2.zText = value;
		}

		/// <summary>
		/// A text control in which to insert the link text when clicked.
		/// If null, uses the focused control.
		/// </summary>
		public FrameworkElement InsertInControl { get; set; }

		class _InfoBox : KSciInfoBox
		{
			//InfoWindow _t;

			//public _Control(InfoWindow t) {
			public _InfoBox() {
				//_t = t;
				this.ZInitUseSystemFont = true;
				this.ZInitBlankMargins = (4, 4);
			}

			//protected override void ZOnHandleCreated() {
			//	base.ZOnHandleCreated();
			//	//base.NoMouseLeftSetFocus = true; //no, then cannot scroll with wheel on Win7-8.1
			//	//base.NoMouseRightSetFocus = true;
			//}
		}
	}
}
