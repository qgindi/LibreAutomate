using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace Au.More;

/// <summary>
/// Add enum members to a <see cref="popupMenu"/> menu or WPF control.
/// </summary>
public class EnumUI<TEnum> where TEnum : unmanaged, Enum {
	(TEnum e, object c, string text, string tt)[] _a;
	Selector _cb;
	bool _isFlags;

	/// <summary>
	/// Adds enum members to a <see cref="popupMenu"/> menu as checkbox-items (if it's a <c>[Flags]</c> enum) or radio-items.
	/// </summary>
	/// <param name="m"></param>
	/// <param name="init">Initial value.</param>
	/// <param name="items">Enum members and their text/tooltip. Optional. Text can be: <c>null</c>, <c>"text"</c>, <c>"text|tooltip"</c>, <c>"|tooltip"</c>.</param>
	/// <example>
	/// <code><![CDATA[
	/// var m = new popupMenu();
	/// var f = new EnumUI<KMod>(m, KMod.Ctrl|KMod.Alt); //a [Flags] enum
	/// m.Separator();
	/// var e = new EnumUI<DayOfWeek>(m, DateTime.Today.DayOfWeek); //a non-[Flags] enum
	/// m.Show();
	/// print.it(f.Result);
	/// print.it(e.Result);
	/// ]]></code>
	/// </example>
	/// <seealso cref="popupMenu.AddEnum{TEnum}"/>
	public EnumUI(popupMenu m, TEnum init = default, (TEnum value, string text)[] items = null) {
		Not_.Null(m);
		_isFlags = typeof(TEnum).IsDefined(typeof(FlagsAttribute), false);
		bool cns = m.CheckDontClose; if (_isFlags) m.CheckDontClose = true;
		_InitArray(items);
		for (int i = 0; i < _a.Length; i++) {
			var (e, _, text, tt) = _a[i];
			var mi = _isFlags
				? m.AddCheck(text, init.HasFlag(e))
				: m.AddRadio(text, init.Equals(e));
			mi.Tooltip = tt;
			_a[i].c = mi;
		}
		if (_isFlags) m.CheckDontClose = cns;
	}

	void _InitArray((TEnum value, string text)[] items) {
		if (items != null) {
			_a = new (TEnum e, object c, string text, string tt)[items.Length];
			for (int i = 0; i < _a.Length; i++) {
				var (v, text) = items[i];
				_a[i].e = v;
				if (text != null) {
					int j = text.IndexOf('|');
					if (j >= 0) {
						_a[i].tt = text[(j + 1)..];
						text = j == 0 ? null : text[..j];
					}
				}
				_a[i].text = text ?? v.ToString();
			}
		} else {
			_a = Enum.GetValues<TEnum>().Select(o => (o, (object)null, o.ToString(), (string)null)).ToArray();
		}
	}

	/// <summary>
	/// Adds members of a <c>[Flags]</c> enum to a WPF <b>StackPanel</b> as checkboxes.
	/// </summary>
	/// <param name="container"></param>
	/// <param name="init">Initial value.</param>
	/// <param name="items">Enum members and their text/tooltip. Optional. Text can be: <c>null</c>, <c>"text"</c>, <c>"text|tooltip"</c>, <c>"|tooltip"</c>.</param>
	/// <example>
	/// With <b>wpfBuilder</b>.
	/// <code><![CDATA[
	/// b.R.StartStack(vertical: true);
	/// var e = new EnumUI<KMod>(b.Panel as StackPanel, KMod.Ctrl|KMod.Alt);
	/// b.End();
	/// ...
	/// print.it(e.Result);
	/// ]]></code>
	/// </example>
	public EnumUI(StackPanel container, TEnum init = default, (TEnum value, string text)[] items = null) {
		Not_.Null(container);
		_isFlags = true;
		_InitArray(items);
		Thickness margin = container.Orientation == Orientation.Vertical ? new(2) : new(2, 2, 12, 2);
		for (int i = 0; i < _a.Length; i++) {
			var (e, _, text, tt) = _a[i];
			var c = new CheckBox {
				Content = text,
				ToolTip = tt,
				IsChecked = init.HasFlag(e),
				Margin = margin,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			_a[i].c = c;
			container.Children.Add(c);
		}
	}

	/// <summary>
	/// Adds members of a non-<c>[Flags]</c> enum to a WPF <b>ComboBox</b> or other <b>Selector</b> control.
	/// </summary>
	/// <param name="container"></param>
	/// <param name="init">Initial value.</param>
	/// <param name="items">Enum members and their text/tooltip. Optional. Text can be: <c>null</c>, <c>"text"</c>, <c>"text|tooltip"</c>, <c>"|tooltip"</c>.</param>
	/// <example>
	/// <code><![CDATA[
	/// b.R.Add("Dock", out ComboBox cb1);
	/// var e = new EnumUI<Dock>(cb1);
	/// ...
	/// print.it(e.Result);
	/// ]]></code>
	/// </example>
	public EnumUI(Selector container, TEnum init = default, (TEnum value, string text)[] items = null) {
		Not_.Null(container);
		_cb = container;
		_InitArray(items);
		int iSel = -1;
		for (int i = 0; i < _a.Length; i++) {
			var (e, _, text, tt) = _a[i];
			ListBoxItem k = container is ComboBox ? new ComboBoxItem() : new ListBoxItem();
			k.Content = text;
			k.ToolTip = tt;
			_cb.Items.Add(k);
			if (iSel < 0 && init.Equals(e)) iSel = i;
		}
		if (iSel >= 0) _cb.SelectedIndex = iSel;
	}

	/// <summary>
	/// Gets enum value from checked/selected menu items or WPF elements.
	/// </summary>
	public TEnum Result {
		get {
			TEnum r = default;
			if (_cb != null) {
				int i = _cb.SelectedIndex;
				if (i >= 0) r = _a[i].e;
			} else {
				for (int i = 0; i < _a.Length; i++) {
					var (e, c, _, _) = _a[i];
					switch (c) {
					case PMItem mi:
						if (!mi.IsChecked) continue;
						break;
					case CheckBox ch:
						if (ch.IsChecked != true) continue;
						break;
					}
					if (_isFlags) {
						ExtMisc.SetFlag(ref r, e, true);
					} else {
						r = e;
						break;
					}
				}
			}
			return r;
		}
	}
}
