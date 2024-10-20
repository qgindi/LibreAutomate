using System.Windows.Controls;

/// <summary>
/// <b>ComboBox</b> to select screen.
/// Ctor adds items. Finally call <b>Result</b>.
/// </summary>
public class KScreenComboBox : ComboBox {
	///
	public KScreenComboBox() {
		Items.Add("Primary screen");
		SelectedIndex = 0;
		
		var a = screen.all;
		if (a.Length > 1) {
			//add functions of screen.at
			foreach (var v in typeof(screen.at).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public)) {
				Items.Add("screen.at." + v.Name);
			}
			
			//if defined type "screens", add its public static properties that return screen
			//	This code works, but probably this feature would be rarely used. Now undocumented.
			//if (_compilation.GetSymbolsWithName("screens", SymbolFilter.Type).FirstOrDefault() is INamedTypeSymbol screens) {
			//	foreach (var v in screens.GetMembers()) {
			//		if (v is not IPropertySymbol p || !v.IsStatic || v.DeclaredAccessibility is not Microsoft.CodeAnalysis.Accessibility.Public) continue;
			//		if (p.Type.ToString() != "Au.screen") continue;
			//		Items.Add("screens." + v.Name);
			//	}
			//}
			
			//if (a.Length == 2) Items.Add("screen.index(1)"); //no. More screens may be added in the future, and indices may change then.
		}
	}
	
	/// <summary>
	/// Formats <i>screen</i> argument code.
	/// </summary>
	/// <param name="trigger"></param>
	/// <returns>null if primary screen.</returns>
	public string Result(bool trigger) {
		int iScreen = SelectedIndex;
		if (iScreen == 0) return null;
		var s = Items[iScreen] as string;
		//if (s.Starts("screens.")) return s;
		if (s.Starts("screen.at.")) s += trigger ? "(true)" : "()";
		else if (trigger && s.Like("screen.index(*)")) s = s.Insert(^1, ", lazy: true");
		return s;
	}
}
