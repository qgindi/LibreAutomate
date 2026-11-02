using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace Au.More;

/// <summary>
/// Adds enum members to a <see cref="popupMenu"/> menu or WPF control, and gets the selected value(s).
/// </summary>
public class EnumUI<TEnum> where TEnum : unmanaged, Enum {
#pragma warning disable CS1591 //Missing XML comment for publicly visible type or member
	public record class ItemData(TEnum Value) { public string Text, Tooltip; internal object uiItem; }
#pragma warning restore CS1591 //Missing XML comment for publicly visible type or member
	
	bool _isFlags;
	readonly ItemData[] _a;
	object _container;
	
	EnumUI((TEnum value, string text)[] items, Func<ItemData, bool> itemFunc, bool sort) {
		_isFlags = typeof(TEnum).IsDefined(typeof(FlagsAttribute), false);
		
		IEnumerable<ItemData> e;
		if (items != null) {
			e = items.Select(o => {
				ItemData d = new(o.value);
				
				var text = o.text;
				if (text != null) {
					int j = text.IndexOf('|');
					if (j >= 0) {
						d.Tooltip = text[(j + 1)..];
						text = j == 0 ? null : text[..j];
					}
				}
				d.Text = text ?? o.value.ToString();
				
				return d;
			});
		} else {
			IEnumerable<TEnum> e1 = Enum.GetValues<TEnum>();
			if (_isFlags) e1 = e1.Where(static o => !EqualityComparer<TEnum>.Default.Equals(o, default));
			
			e = e1.Select(o => new ItemData(o) { Text = o.ToString() });
			if (itemFunc != null) e = e.Where(itemFunc);
		}
		
		if (sort) e = e.OrderBy(o => o.Text);
		_a = e.ToArray();
	}
	
	/// <param name="sort">Sort by name.</param>
	public EnumUI(bool sort = false) : this(null, null, sort) { }
	
	/// <param name="items">Enum members and their text/tooltip. Optional. Text can be: <c>null</c>, <c>"text"</c>, <c>"text|tooltip"</c>, <c>"|tooltip"</c>.</param>
	/// <param name="sort">Sort by name.</param>
	public EnumUI((TEnum value, string text)[] items, bool sort = false) : this(items, null, sort) { }
	
	/// <param name="itemFunc">This callback function is called for each enum member, with the value and name. Can set text and tooltip. Return <c>true</c> to add.</param>
	/// <param name="sort">Sort by name.</param>
	public EnumUI(Func<ItemData, bool> itemFunc, bool sort = false) : this(null, itemFunc, sort) { }
	
	/// <inheritdoc cref="AddTo(popupMenu, TEnum)"/>
	/// <inheritdoc cref="EnumUI{TEnum}.EnumUI(ValueTuple{TEnum, string}[], bool)"/>
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
	public EnumUI(popupMenu m, TEnum init = default, (TEnum value, string text)[] items = null, bool sort = false) : this(items, null, sort) {
		AddTo(m, init);
	}
	
	/// <summary>
	/// Adds enum members to a <see cref="popupMenu"/> menu as checkbox-items (if it's a <c>[Flags]</c> enum) or radio-items.
	/// </summary>
	/// <param name="m">Menu.</param>
	/// <param name="init">Initial value.</param>
	public void AddTo(popupMenu m, TEnum init = default) {
		Not_.Null(m);
		_container = m;
		bool cns = m.CheckDontClose; if (_isFlags) m.CheckDontClose = true;
		foreach (var v in _a) {
			var mi = _isFlags
				? m.AddCheck(v.Text, init.HasFlag(v.Value))
				: m.AddRadio(v.Text, EqualityComparer<TEnum>.Default.Equals(init, v.Value));
			mi.Tooltip = v.Tooltip;
			v.uiItem = mi;
		}
		if (_isFlags) m.CheckDontClose = cns;
	}
	
	/// <inheritdoc cref="AddTo(Panel, TEnum)"/>
	/// <inheritdoc cref="EnumUI{TEnum}.EnumUI(ValueTuple{TEnum, string}[], bool)"/>
	/// <example>
	/// With <see cref="wpfBuilder"/>.
	/// <code><![CDATA[
	/// b.R.StartStack(vertical: true);
	/// var e = new EnumUI<KMod>(b.Panel, KMod.Ctrl|KMod.Alt);
	/// b.End();
	/// ...
	/// print.it(e.Result);
	/// ]]></code>
	/// </example>
	/// <seealso cref="wpfBuilder.AddEnum"/>
	public EnumUI(Panel container, TEnum init = default, (TEnum value, string text)[] items = null, bool sort = false) : this(items, null, sort) {
		AddTo(container, init);
	}
	
	/// <summary>
	/// Adds members of a <c>[Flags]</c> enum as checkboxes to a WPF panel.
	/// </summary>
	/// <param name="container"><see cref="StackPanel"/>, <see cref="WrapPanel"/> or <see cref="Grid"/>. If <c>Grid</c> without columns, adds 2 columns.</param>
	/// <param name="init">Initial value.</param>
	public void AddTo(Panel container, TEnum init = default) {
		Not_.Null(container);
		_container = container;
		_isFlags = true;
		
		int nCol = 0;
		if (container is Grid g1) {
			nCol = g1.ColumnDefinitions.Count;
			if (nCol == 0) {
				nCol = 2;
				g1.AddColumns(0, -1);
			}
		}
		
		Thickness margin = container is StackPanel { Orientation: Orientation.Vertical } ? new(2) : new(2, 2, 12, 2);
		int row = -1;
		foreach (var (i, v) in _a.Index()) {
			var c = new CheckBox {
				Content = v.Text,
				ToolTip = v.Tooltip,
				IsChecked = init.HasFlag(v.Value),
				Margin = margin,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			v.uiItem = c;
			container.Children.Add(c);
			
			if (nCol > 0) {
				int col = i % nCol;
				if (col == 0) { row++; ((Grid)container).RowDefinitions.Add(new() { Height = new() }); }
				Grid.SetColumn(c, col);
				Grid.SetRow(c, row);
			}
		}
	}
	
	/// <inheritdoc cref="AddTo(Selector, TEnum)"/>
	/// <inheritdoc cref="EnumUI{TEnum}.EnumUI(ValueTuple{TEnum, string}[], bool)"/>
	/// <example>
	/// <code><![CDATA[
	/// b.R.Add("Dock", out ComboBox cb1);
	/// var e = new EnumUI<Dock>(cb1);
	/// ...
	/// print.it(e.Result);
	/// ]]></code>
	/// </example>
	public EnumUI(Selector container, TEnum init = default, (TEnum value, string text)[] items = null, bool sort = false) : this(items, null, sort) {
		AddTo(container, init);
	}
	
	/// <summary>
	/// Adds members of a non-<c>[Flags]</c> enum to a WPF <see cref="ComboBox"/> or other <see cref="Selector"/>-based control.
	/// </summary>
	/// <param name="container">A <c>ComboBox</c> or other <c>Selector</c>-based control.</param>
	/// <param name="init">Initial value.</param>
	public void AddTo(Selector container, TEnum init = default) {
		Not_.Null(container);
		_container = container;
		_isFlags = false;
		int iSel = -1;
		foreach (var (i, v) in _a.Index()) {
			ListBoxItem k = container is ComboBox ? new ComboBoxItem() : new ListBoxItem();
			k.Content = v.Text;
			k.ToolTip = v.Tooltip;
			container.Items.Add(k);
			if (iSel < 0 && EqualityComparer<TEnum>.Default.Equals(init, v.Value)) iSel = i;
		}
		if (iSel >= 0) container.SelectedIndex = iSel;
	}
	
	/// <summary>
	/// Gets enum value from checked/selected menu items or WPF elements.
	/// </summary>
	public TEnum Result {
		get {
			TEnum r = default;
			if (_container is Selector sel) {
				int i = sel.SelectedIndex;
				if (i >= 0) r = _a[i].Value;
			} else {
				foreach (var v in _a) {
					switch (v.uiItem) {
					case PMItem mi:
						if (!mi.IsChecked) continue;
						break;
					case CheckBox ch:
						if (ch.IsChecked != true) continue;
						break;
					}
					if (_isFlags) {
						ExtMisc.SetFlag(ref r, v.Value, true);
					} else {
						r = v.Value;
						break;
					}
				}
			}
			return r;
		}
	}
}
