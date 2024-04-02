
namespace Au.Types;

/// <summary>
/// Represents a menu item in <see cref="popupMenu"/>.
/// </summary>
/// <remarks>
/// Most properties cannot be changed while the menu is open. Can be changed <b>Tag</b>, <b>Tooltip</b>, <b>IsChecked</b> and <b>IsDisabled</b>.
/// </remarks>
public class PMItem : MTItem {
	readonly popupMenu _m;
	internal byte checkType; //1 checkbox, 2 radio
	internal bool checkDontClose;
	internal bool rawText;
	
	internal PMItem(popupMenu m, bool isDisabled, bool isChecked = false) {
		_m = m;
		_isDisabled = isDisabled;
		_isChecked = isChecked;
		checkDontClose = m.CheckDontClose;
		rawText = m.RawText;
	}
	
	/// <summary>Gets item action.</summary>
	public Action<PMItem> Clicked => base.clicked as Action<PMItem>;
	
	/// <summary>Gets or sets menu item id.</summary>
	public int Id { get; set; }
	
	/// <summary><c>true</c> if is a submenu-item.</summary>
	public bool IsSubmenu { get; init; }
	
	/// <summary><c>true</c> if is a separator.</summary>
	public bool IsSeparator { get; init; }
	
	/// <summary>
	/// Gets or sets disabled state.
	/// </summary>
	public bool IsDisabled {
		get => _isDisabled || IsSeparator;
		set {
			if (value != _isDisabled && !IsSeparator) {
				_isDisabled = value;
				_m.Invalidate_(this);
			}
		}
	}
	bool _isDisabled;
	
	/// <summary>
	/// Gets or sets checked state.
	/// </summary>
	/// <exception cref="InvalidOperationException">The <c>set</c> function throws this exception if the item isn't checkable. Use <see cref="popupMenu.AddCheck"/> or <see cref="popupMenu.AddRadio"/>.</exception>
	public bool IsChecked {
		get => _isChecked;
		set {
			if (checkType == 0) throw new InvalidOperationException();
			if (value != _isChecked) {
				_isChecked = value;
				_m.Invalidate_(this);
			}
		}
	}
	bool _isChecked;
	
	/// <summary>Gets or sets whether to use bold font.</summary>
	public bool FontBold { get; set; }
	
	/// <summary>Gets or sets background color.</summary>
	public ColorInt BackgroundColor { get; set; }
	
	/// <summary>
	/// Hotkey display text.
	/// </summary>
	public string Hotkey { get; set; }
	
	/// <summary>
	/// Invokes <see cref="Clicked"/> if not <c>null</c>.
	/// Handles exceptions if need. Invokes in new thread if need.
	/// </summary>
	internal void InvokeAction_() {
		if (clicked is Action<PMItem> action) {
			if (actionThread) run.thread(() => _Invoke(), background: false); else _Invoke();
			void _Invoke() {
				try { action(this); }
				catch (Exception ex) when (!this.actionException) { print.warning(ex); }
			}
		}
	}
}

/// <summary>
/// Flags for <see cref="popupMenu"/> <b>ShowX</b> methods.
/// </summary>
/// <remarks>
/// The <b>AlignX</b> flags are for API <msdn>TrackPopupMenuEx</msdn>.
/// </remarks>
[Flags]
public enum PMFlags {
	/// <summary>Show by the caret (text cursor) position. If not possible, use flag <b>WindowCenter</b> or <b>ScreenCenter</b> or <i>xy</i> or mouse position.</summary>
	ByCaret = 0x1000000,
	
	/// <summary>Show in the center of the screen that contains the mouse pointer.</summary>
	ScreenCenter = 0x2000000,
	
	/// <summary>Show in the center of the active window.</summary>
	WindowCenter = 0x4000000,
	
	/// <summary>Underline characters preceded by <c>&amp;</c>, regardless of Windows settings. More info: <see cref="StringUtil.RemoveUnderlineChar"/>.</summary>
	Underline = 0x8000000,
	
	//TPM_ flags
	
	/// <summary>Horizontally align the menu so that the show position would be in its center.</summary>
	AlignCenterH = 0x4,
	
	/// <summary>Horizontally align the menu so that the show position would be at its right side.</summary>
	AlignRight = 0x8,
	
	/// <summary>Vertically align the menu so that the show position would be in its center.</summary>
	AlignCenterV = 0x10,
	
	/// <summary>Vertically align the menu so that the show position would be at its bottom.</summary>
	AlignBottom = 0x20,
	
	/// <summary>Show at the bottom or top of <i>excludeRect</i>, not at the right/left.</summary>
	AlignRectBottomTop = 0x40,
}

/// <summary>
/// Used with <see cref="popupMenu.KeyboardHook"/>.
/// </summary>
public enum PMKHook {
	/// <summary>Process the key event as usually.</summary>
	Default,
	
	/// <summary>Close the menu.</summary>
	Close,
	
	/// <summary>Do nothing.</summary>
	None,
	
	/// <summary>Execute the focused item and close the menu.</summary>
	ExecuteFocused,
}

/// <summary>
/// Used with <see cref="popupMenu.Metrics"/> and <see cref="popupMenu.defaultMetrics"/>.
/// </summary>
/// <remarks>
/// All values are in logical pixels (1 pixel when DPI is 100%).
/// </remarks>
public record class PMMetrics(int ItemPaddingY = 0, int ItemPaddingLeft = 0, int ItemPaddingRight = 0);
