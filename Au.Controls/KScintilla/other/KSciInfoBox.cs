using System.Windows;
using System.Windows.Controls;

namespace Au.Controls;

/// <summary>
/// Scintilla-based control to show formatted information text.
/// To set text use the <see cref="KScintilla.aaaText"/> property. For formatting and links use tags: <see cref="SciTags"/>.
/// </summary>
public class KSciInfoBox : KScintilla {
	public KSciInfoBox() {
		AaInitReadOnlyAlways = true;
		AaInitTagsStyle = AaTagsStyle.AutoAlways;
		AaInitImages = true;
		AaInitUseDefaultContextMenu = true;
		AaInitWrapVisuals = false;
		AaWrapLines = true;
		//TabStop = false;
		Name = "info";
	}

	protected override void AaOnHandleCreated() {
		base.AaOnHandleCreated();

		aaaStyleBackColor(Sci.STYLE_DEFAULT, 0xf8fff0);
		if (AaInitUseSystemFont) aaaStyleFont(Sci.STYLE_DEFAULT); //Segoe UI 9 is narrower but taller than the default Verdana 8. Also tested Calibri 9, but Verdana looks better.
		aaaStyleClearAll();

		aaaMarginSetWidth(1, 0);

		SIZE z = AaInitBlankMargins;
		z = Dpi.Scale(z, AaWnd);
		Call(Sci.SCI_SETMARGINLEFT, 0, z.width);
		Call(Sci.SCI_SETMARGINRIGHT, 0, z.height);
	}

	/// <summary>
	/// Use font Segoe UI 9 instead of the default font Verdana 8.
	/// </summary>
	public bool AaInitUseSystemFont { get; set; }

	/// <summary>
	/// The width of the blank margin on both sides of the text. Logical pixels.
	/// </summary>
	public (int left, int right) AaInitBlankMargins { get; set; } = (1, 1);

	//protected override bool IsInputKey(Keys keyData) {
	//	switch (keyData & Keys.KeyCode) { case Keys.Tab: case Keys.Escape: case Keys.Enter: return false; }
	//	return base.IsInputKey(keyData);
	//}

	/// <summary>
	/// Sets element's tooltip text to show in this control instead of standard tooltip popup.
	/// Uses <b>ToolTip</b> property; don't overwrite it.
	/// </summary>
	public void AaAddElem(FrameworkElement c, string text) {
		c.ToolTip = text;
		c.ToolTipOpening += (o, e) => {
			e.Handled = true;
			if (_suspendElems != 0) {
				if (Environment.TickCount64 < _suspendElems) return;
				_suspendElems = 0;
			}
			this.aaaText = (o as FrameworkElement).ToolTip as string;
		};
	}

	/// <summary>
	/// Temporarily suspends showing tooltips of elements in this control. See <see cref="AaAddElem"/>.
	/// </summary>
	/// <param name="timeMS">Suspend for this time interval, ms. If 0, resumes.</param>
	public void AaSuspendElems(long timeMS = 3000) {
		_suspendElems = Environment.TickCount64 + timeMS;
	}
	long _suspendElems;

	///
	public bool AaElemsSuspended => _suspendElems != 0 && Environment.TickCount64 < _suspendElems;
}
