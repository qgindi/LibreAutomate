using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Au.Controls;

/// <summary>
/// Makes the GroupBox more vivid.
/// </summary>
public class KGroupBox : GroupBox {
	protected private readonly bool _no;
	
	internal static Brush TextColor_ => s_brush ??= new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));
	static Brush s_brush;
	
	public KGroupBox() {
		if (_no = SystemParameters.HighContrast) return;
		BorderBrush = SystemColors.ActiveBorderBrush; //default is almost invisible
	}
	
	protected override void OnHeaderChanged(object oldHeader, object newHeader) {
		if (newHeader is string s && !_no) {
			if (oldHeader is null) {
				Header = new TextBlock { Text = s, FontWeight = FontWeights.Bold, Foreground = TextColor_ };
				return;
			} else if (oldHeader is TextBlock t) {
				t.Text = s;
				Header = t;
				return;
			}
		}
		base.OnHeaderChanged(oldHeader, newHeader);
	}
}

/// <summary>
/// Makes the GroupBox look like separator with label.
/// </summary>
public class KGroupBoxSeparator : KGroupBox {
	public KGroupBoxSeparator() {
		if (_no) return; //WPF draws badly
		BorderThickness = new(0, 1, 0, 0);
		Padding = new(-6, 0, -6, -6);
	}
}
