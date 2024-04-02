using System.Windows;
using System.Windows.Controls;
using System.Collections;

namespace Au.More;

/// <summary>
/// Dialog with list of check boxes.
/// </summary>
/// <seealso cref="EnumUI{TEnum}"/>
/// <example>
/// <code><![CDATA[
/// string[] a = ["one", "two"];
/// var d = new CheckListDialog("Info.");
/// d.Add(a);
/// d.Add("three", true);
/// d.Add("four");
/// if (!d.ShowDialog()) return;
/// print.it(d.ResultBits, d.ResultIndices, d.ResultItems);
/// ]]></code>
/// </example>
public class CheckListDialog {
	wpfBuilder _b;
	TextBlock _text;
	ListBox _lb;
	List<CheckBox> _ac = new();
	
	/// <summary>
	/// Creates <see cref="Builder"/>, sets some window properties, optionally sets static text.
	/// </summary>
	/// <param name="text">Text above the list. Can be <c>null</c>. See also <see cref="FormatText"/>.</param>
	/// <param name="title">Window name. If <c>null</c>, uses <see cref="dialog.options.defaultTitle"/>.</param>
	public CheckListDialog(string text = null, string title = null) {
		_b = new wpfBuilder(title ?? dialog.options.defaultTitle)
			.WinProperties(resizeMode: ResizeMode.NoResize)
			.Width(250..600);
		_b.R.Add(out _text, text).Wrap(TextWrapping.Wrap);
		if (text == null) _text.Visibility = Visibility.Collapsed;
	}
	
	/// <summary>
	/// Adds a checkbox.
	/// </summary>
	/// <returns>The checkbox.</returns>
	public CheckBox Add(string text, bool check = false, string tooltip = null) {
		if (_lb == null) _b.R.Add(out _lb).Height(..550);
		
		//var content = UseAccessKey ? text : text?.Replace("_", "__");
		var content = text?.Replace("_", "__");
		CheckBox c = new() { Content = content, Tag = text, IsChecked = check, ToolTip = tooltip };
		_ac.Add(c);
		_lb.Items.Add(c);
		return c;
	}
	
	/// <summary>
	/// Adds multiple checkboxes.
	/// </summary>
	/// <param name="items">Array, <b>List</b>, etc containing text strings for checkboxes.</param>
	/// <param name="check">Whether to check all checkboxes.</param>
	public void Add(IEnumerable<string> items, bool check = false) {
		foreach (var v in items) Add(v, check);
	}
	
	///// <summary>
	///// Character <c>'_'</c> in checkbox text isn't displayed. Instead the next character toggles the checkbox when pressed with <c>Alt</c>. For literal <c>"_"</c> use <c>"__"</c>.
	///// </summary>
	//public bool UseAccessKey { get; set; }
	
	/// <summary>
	/// Sets formatted text, like <see cref="wpfBuilder.FormatText(wpfBuilder.InterpolatedString)"/>.
	/// </summary>
	/// <inheritdoc cref="wpfBuilder.formatTextOf" path="//param|//exception"/>
	public void FormatText(wpfBuilder.InterpolatedString text) {
		wpfBuilder.formatTextOf(_text, text);
		_text.Visibility = Visibility.Visible;
	}
	
	/// <summary>
	/// Changes text of <b>OK</b> and <b>Cancel</b> buttons.
	/// </summary>
	public void SetButtons(string ok = "OK", string cancel = "Cancel") {
		_ok = ok;
		_cancel = cancel;
	}
	string _ok, _cancel;
	
	/// <summary>
	/// Gets the <b>wpfBuilder</b> that builds the dialog.
	/// You can use it to add more controls, change window properties, etc; see example.
	/// </summary>
	/// <example>
	/// <code><![CDATA[
	/// using System.Windows.Controls;
	/// string[] a = ["one", "two"];
	/// var d = new CheckListDialog("Info.");
	/// d.Add(a);
	/// d.Builder.R.Add("Input", out TextBox tInput);
	/// if (!d.ShowDialog()) return;
	/// print.it(d.ResultBits, d.ResultIndices, d.ResultItems, tInput.Text);
	/// ]]></code>
	/// </example>
	public wpfBuilder Builder => _b;
	
	/// <summary>
	/// Adds <b>OK</b>/<b>Cancel</b> buttons, shows dialog, sets result properties.
	/// </summary>
	/// <param name="owner">
	/// Owner window, or an element in it.
	/// If used, sets <b>ShowInTaskbar</b> = <c>false</c>. Else sets <b>Topmost</b> = <c>true</c>, unless <see cref="dialog.options.topmostIfNoOwnerWindow"/> is <c>false</c> or the active window belongs to this process.
	/// </param>
	/// <returns><c>true</c> if pressed <b>OK</b>.</returns>
	public bool ShowDialog(DependencyObject owner = null) {
		_b.R.AddOkCancel(_ok ?? "OK", _cancel ?? "Cancel");
		_b.End();
		
		var w = _b.Window;
		if (owner != null) w.ShowInTaskbar = false;
		else if (dialog.options.topmostIfNoOwnerWindow && !wnd.active.IsOfThisProcess) w.Topmost = true;
		
		if (!_b.ShowDialog(owner)) return false;
		
		BitArray ba = new(_ac.Count);
		int n = 0;
		for (int i = 0; i < ba.Length; i++) if (ba[i] = _ac[i].IsChecked == true) n++;
		ResultIndices = new int[n];
		for (int i = 0, j = 0; i < ba.Length; i++) if (ba[i]) ResultIndices[j++] = i;
		ResultItems = new string[n];
		for (int i = 0, j = 0; i < ba.Length; i++) if (ba[i]) ResultItems[j++] = _ac[i].Tag as string;
		ResultBits = ba;
		
		return true;
	}
	
	/// <summary>
	/// Gets a bit array where elements represent checkbox states (<c>true</c> if checked).
	/// This property is set by <b>ShowDialog</b>.
	/// </summary>
	public BitArray ResultBits { get; private set; }
	
	/// <summary>
	/// Gets 0-based indices of checked items.
	/// This property is set by <b>ShowDialog</b>.
	/// </summary>
	public int[] ResultIndices { get; private set; }
	
	/// <summary>
	/// Gets strings of checked items.
	/// This property is set by <b>ShowDialog</b>.
	/// </summary>
	/// <example>
	/// <code><![CDATA[
	/// List<string> a = ["one", "two"];
	/// var d = new CheckListDialog("Info.");
	/// d.Add(a);
	/// if (!d.ShowDialog() || !d.ResultItems.Any()) return;
	/// a = d.ResultItems.ToList();
	/// ]]></code>
	/// </example>
	public string[] ResultItems { get; private set; }
}
