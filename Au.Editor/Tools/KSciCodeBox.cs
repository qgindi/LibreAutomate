
namespace Au.Controls;

/// <summary>
/// Scintilla-based control that shows colored C# code.
/// Also can be used anywhere to edit partially styled C# code. To make editable and set text use <see cref="ZSetText"/> with readonlyFrom=-1.
/// </summary>
public class KSciCodeBox : KScintilla {
	public KSciCodeBox() {
		ZInitUseDefaultContextMenu = true;
		//ZInitBorder = true; //no, the native border is of different color and thickness (high DPI) than other WPF controls
		Name = "code";
	}

	protected override void ZOnHandleCreated() {
		base.ZOnHandleCreated();

		zSetMarginWidth(1, 0);
		zIsReadonly = true;

		var styles = new CiStyling.TStyles { FontSize = 9 };
		styles.ToScintilla(this);
	}

	protected override void ZOnSciNotify(ref Sci.SCNotification n) {
		//switch(n.nmhdr.code) {
		//case Sci.NOTIF.SCN_PAINTED: case Sci.NOTIF.SCN_UPDATEUI: break;
		//default: print.it(n.nmhdr.code, n.modificationType); break;
		//}

		switch (n.nmhdr.code) {
		case Sci.NOTIF.SCN_UPDATEUI:
			//make text after _ReadonlyStartUtf8 readonly
			if (0 != (n.updated & Sci.SC_UPDATE_SELECTION)) { //selection changed
				if (_readonlyLenUtf8 > 0) {
					int i = Call(Sci.SCI_GETSELECTIONEND);
					zIsReadonly = i > _ReadonlyStartUtf8 || _LenUtf8 == 0; //small bug: if caret is at the boundary, allows to delete readonly text, etc.
				}
			}
			break;
		case Sci.NOTIF.SCN_STYLENEEDED:
			_Styling();
			break;
		}

		base.ZOnSciNotify(ref n);
	}

	/// <summary>
	/// Sets text and makes all or part of it readonly.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="readonlyFrom">If 0, makes all text readonly. If s.Length or -1, makes all text editable. If between 0 and s.Length, makes readonly from this position.</param>
	public void ZSetText(string s, int readonlyFrom = 0) {
		zIsReadonly = false;
		zSetText(s, SciSetTextFlags.NoUndoNoNotify);
		if (readonlyFrom > 0) {
			_readonlyLenUtf8 = _LenUtf8 - zPos8(readonlyFrom);
		} else if (readonlyFrom < 0) {
			_readonlyLenUtf8 = 0;
		} else {
			zIsReadonly = true;
			_readonlyLenUtf8 = -1;
		}
	}

	public int ZReadonlyStart => _readonlyLenUtf8 < 0 ? 0 : zPos16(_ReadonlyStartUtf8);

	protected int _readonlyLenUtf8;

	protected int _ReadonlyStartUtf8 => _readonlyLenUtf8 < 0 ? 0 : _LenUtf8 - _readonlyLenUtf8;

	protected int _LenUtf8 => Call(Sci.SCI_GETTEXTLENGTH);

	unsafe void _Styling() {
		var styles8 = CiUtil.GetScintillaStylingBytes(zText);
		Call(Sci.SCI_STARTSTYLING);
		fixed (byte* p = styles8) Call(Sci.SCI_SETSTYLINGEX, styles8.Length, p);
	}
}
