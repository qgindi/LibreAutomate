using System.Windows.Controls;
using Au.Controls;
using System.Windows;

namespace ToolLand;

/// <summary>
/// <see cref="KTextBox"/> that supports C# expression in text.
/// Just adds context menu item "Expressions..." that shows how to enter an expression; see <see cref="SetExpressionContextMenu"/>.
/// </summary>
class KTextExpressionBox : KTextBox {
	protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
		SetExpressionContextMenu(this);
		base.OnContextMenuOpening(e);
	}
	
	/// <summary>
	/// Replaces the standard context menu. Adds standard items + item "Expressions..." that shows how to enter an expression.
	/// </summary>
	public static void SetExpressionContextMenu(TextBox t) {
		var m = new ContextMenu();
		_Add("Cut", "Ctrl+X", t.SelectionLength > 0 ? () => t.Cut() : null);
		_Add("Copy", "Ctrl+C", t.SelectionLength > 0 ? () => t.Copy() : null);
		_Add("Paste", "Ctrl+V", Clipboard.ContainsText() ? () => t.Paste() : null);
		m.Items.Add(new Separator());
		_Add("Expressions...", null, () => { dialog.show("Expressions", """
In this text field you can enter literal text or a C# expression.
Literal text in code will be enclosed in "" or @"" and escaped if need.
Expression will be added to code without changes.

Examples of all supported expressions:

@@expression
@"verbatim string"
$"interpolated string like {variable} text {expression} text"
$@"interpolated verbatim string like {variable} text {expression} text"

Real expression examples:

@"\bregular expression\b"
@@Environment.GetEnvironmentVariable("API_KEY")
@@"line1\r\nline2"
""", owner: t); });
		t.ContextMenu = m;
		
		void _Add(string name, string key, Action click) {
			var k = new MenuItem { Header = name, InputGestureText = key };
			if (click is null) k.IsEnabled = false;else k.Click += (_,_)=>click();
			m.Items.Add(k);
		}
	}
}

/// <summary>
/// Editable <b>ComboBox</b> that supports C# expression in text.
/// Just adds context menu item "Expressions..." that shows how to enter an expression; see <see cref="SetExpressionContextMenu"/>.
/// </summary>
class KComboExpressionBox : ComboBox {
	public KComboExpressionBox() {
		IsEditable = true;
		SetExpressionContextMenu(this);
	}
	
	/// <summary>
	/// Replaces the standard context menu. Adds standard items + item "Expressions..." that shows how to enter an expression.
	/// </summary>
	public static void SetExpressionContextMenu(ComboBox t) {
		if (t.IsLoaded) {
			_Set();
		} else {
			t.Loaded += (_, _) => _Set();
		}
		void _Set() {
			if (t.Template.FindName("PART_EditableTextBox", t) is TextBox x) {
				KTextExpressionBox.SetExpressionContextMenu(x);
			}
		}
	}
}
