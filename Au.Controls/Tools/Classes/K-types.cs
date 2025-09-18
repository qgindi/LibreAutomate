using Au.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;

namespace Au.Tools;

//TODO: mess

public interface IEditor {
	string SettingsDirBS { get; }
	string ThemeName { get; set; }
	string InternetSearchUrl { get; }
	//void InsertStatement(InsertCodeParams p);
	
	public static IEditor Editor;
}

static class Editor {
	public static AppSettings Settings;
	
	/// <summary>
	/// Inserts one or more statements at current line. With correct position, indent, etc.
	/// If editor is null or readonly or not C# file, prints in output.
	/// Async if called from non-main thread.
	/// </summary>
	public static void InsertStatements(InsertCodeParams p) {
		if (p.s == null) return;
		var json = JsonSerializer.Serialize(p);
		WndCopyData.Send<char>(ScriptEditor.WndMsg_, 17, json);
	}
	
	public static void OpenCookbookRecipe(string name) {
		WndCopyData.Send<char>(ScriptEditor.WndMsg_, 18, name);
	}
}

/// <summary>
/// Parameters for <c>InsertCode.Statements</c>.
/// </summary>
/// <param name="s">Text. The function ignores "\r\n" at the end. Does nothing if null. If contains '\n', prepends/appends empty line to separate from surrounding code.</param>
/// <param name="goTo">If text contains <c>`|`</c>, remove it and finally move caret there.</param>
/// <param name="activateEditor">Activate editor window.</param>
/// <param name="noFocus">Don't focus the editor control. Without this flag focuses it if window is active or activated.</param>
/// <param name="selectNewCode"></param>
/// <param name="makeVarName1"></param>
/// <param name="renameVars">Variable names to rename in <i>s</i>.</param>
public record InsertCodeParams(string s, bool goTo = false, bool activateEditor = false, bool noFocus = false, bool selectNewCode = false, bool makeVarName1 = false, (string oldName, string newName)[] renameVars = null);

public static class KUtil {
	
	/// <summary>
	/// Gets Keyboard.FocusedElement. If null, and a HwndHost-ed control is focused, returns the HwndHost.
	/// Slow if HwndHost-ed control.
	/// </summary>
	public static FrameworkElement FocusedElement {
		get {
			var v = System.Windows.Input.Keyboard.FocusedElement;
			if (v != null) return v as FrameworkElement;
			return wnd.Internal_.ToWpfElement(Api.GetFocus());
		}
	}
	
	/// <summary>
	/// Inserts text in specified or focused control.
	/// At current position, not as new line, replaces selection.
	/// </summary>
	/// <param name="c">If null, uses the focused control, else sets focus.</param>
	/// <param name="s">If contains <c>`|`</c>, removes it and moves caret there; must be single line.</param>
	public static void InsertTextIn(FrameworkElement c, string s) {
		if (c == null) {
			c = FocusedElement;
			if (c == null) return;
		} else {
			Debug.Assert(Environment.CurrentManagedThreadId == c.Dispatcher.Thread.ManagedThreadId);
			if (c != FocusedElement) //be careful with HwndHost
				c.Focus();
		}
		
		int i = s.Find("`|`");
		if (i >= 0) {
			Debug.Assert(!s.Contains('\n'));
			s = s.Remove(i, 3);
			i = s.Length - i;
		}
		
		if (c is KScintilla sci) {
			if (sci.aaaIsReadonly) return;
			sci.aaaReplaceSel(s);
			while (i-- > 0) sci.Call(Sci.SCI_CHARLEFT);
		} else if (c is TextBox tb) {
			if (tb.IsReadOnly) return;
			tb.SelectedText = s;
			tb.CaretIndex = tb.SelectionStart + tb.SelectionLength - Math.Max(i, 0);
		} else {
			Debug_.Print(c);
			if (!c.Hwnd().Window.ActivateL()) return;
			Task.Run(() => {
				var k = new keys(null);
				k.AddText(s);
				if (i > 0) k.AddKey(KKey.Left).AddRepeat(i);
				k.SendNow();
			});
		}
	}
	
}
