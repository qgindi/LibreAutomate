using System.Windows;
using System.Windows.Controls;
using Au.Controls;

namespace LA;

/// <summary>
/// Misc extension methods.
/// </summary>
static class EdExt {
	/// <summary>
	/// Returns true if this starts with <i>s</i> and then follows '\\' or '/'.
	/// Case-insensitive.
	/// </summary>
	/// <param name="orEquals">Also return true if this equals <i>s</i>.</param>
	public static bool PathStarts(this string t, RStr s, bool orEquals = false) {
		if (!t.Starts(s, true)) return false;
		if (t.Length > s.Length) return t[s.Length] is '\\' or '/';
		return orEquals;
	}
	
	/// <summary>
	/// Appends hex like "1A".
	/// Like <c>t.Append(x.ToString("X"))</c> or <c>t.AppendFormat(...)</c>, but faster and no garbage.
	/// </summary>
	public static StringBuilder AppendHex(this StringBuilder t, uint x) {
		if (x == 0) {
			t.Append('0');
		} else {
			Span<char> p = stackalloc char[8];
			int i = 8;
			for (; x != 0; x >>= 4) {
				uint h = x & 15;
				p[--i] = (char)(h + (h < 10 ? 48 : 55));
			}
			t.Append(p[i..]);
		}
		return t;
	}
	
	/// <inheritdoc cref="AppendHex(StringBuilder, uint)"/>
	public static StringBuilder AppendHex(this StringBuilder t, int x) => AppendHex(t, (uint)x);
	
	/// <summary>
	/// Inserts <i>e</i> as menu item.
	/// If <i>e</i> is null, creates and inserts separator.
	/// </summary>
	/// <inheritdoc cref="InsertCustom(MenuItem, int, string, RoutedEventHandler, bool)"/>
	public static void InsertCustom(this MenuItem t, int i, FrameworkElement e = null) {
		e ??= new Separator();
		e.Tag = e;
		if (i < 0) t.Items.Add(e); else t.Items.Insert(i, e);
	}
	
	/// <summary>
	/// Creates and inserts menu item.
	/// </summary>
	/// <param name="i">Insert at this index. If -1, adds at the end.</param>
	/// <param name="text">Item text.</param>
	/// <param name="click"><b>Click</b> event handler or null.</param>
	/// <param name="escapeUnderscore">Replace all "_" with "__".</param>
	/// <remarks>
	/// Sets <b>Tag</b> so that <see cref="RemoveAllCustom"/> will recognize items added by this function.
	/// </remarks>
	public static MenuItem InsertCustom(this MenuItem t, int i, string text, RoutedEventHandler click, bool escapeUnderscore = true) {
		if (escapeUnderscore) text = text.Replace("_", "__");
		var mi = new MenuItem { Header = text };
		if (click != null) mi.Click += click;
		InsertCustom(t, i, mi);
		return mi;
	}
	
	/// <param name="click"><b>Click</b> event handler or null. Receives unescaped <i>text</i>.</param>
	/// <inheritdoc cref="InsertCustom(MenuItem, int, string, RoutedEventHandler, bool)"/>
	public static MenuItem InsertCustom(this MenuItem t, int i, string text, Action<string> click, bool escapeUnderscore = true)
		=> InsertCustom(t, i, text, (o, _) => click(text), escapeUnderscore);
	
	/// <summary>
	/// Removes all menu items inserted by <see cref="InsertCustom"/>.
	/// </summary>
	/// <param name="t"></param>
	public static void RemoveAllCustom(this MenuItem t) {
		for (int j = t.Items.Count; --j >= 0;) if (t.Items[j] is FrameworkElement e && e.Tag == e) t.Items.RemoveAt(j);
	}
	
	/// <summary>
	/// Shows window of type T and ensures that there is single window of that type for this <b>FileNode</b> at a time. 
	/// If a window of type T for this <b>FileNode</b> already exists (created with this function), activates it. Else calls <i>fNew</i> and <b>Show</b>. Auto-closes that window when unloading this workspace.
	/// Example: <c>f.SingleDialog(() => new DThisClass());</c>
	/// </summary>
	/// <param name="f">Can be null.</param>
	/// <param name="fNew">Called to create new T if there is no T window.</param>
	/// <returns>The window (new or existing), or null if <i>f</i> null.</returns>
	public static T SingleDialog<T>(this FileNode f, Func<T> fNew) where T : KDialogWindow {
		if (f == null) return null;
		var v = s_singleDialog.FirstOrDefault(o => o.f == f && o.d is T);
		if (v.f != null) {
			v.d.Hwnd().ActivateL(true);
			return v.d as T;
		} else {
			var d = fNew();
			s_singleDialog.Add((f, d));
			
			App.Model.UnloadingThisWorkspace += d.Close;
			d.Closed += (_, _) => {
				App.Model.UnloadingThisWorkspace -= d.Close;
				s_singleDialog.Remove((f, d));
			};
			
			d.Show();
			return d;
		}
	}
	static List<(FileNode f, KDialogWindow d)> s_singleDialog = new();
}
