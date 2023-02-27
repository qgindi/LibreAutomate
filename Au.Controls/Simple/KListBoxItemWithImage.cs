using System.Windows;
using System.Windows.Controls;

public class KListBoxItemWithImage : ListBoxItem {
	readonly TextBlock _tb;

	public KListBoxItemWithImage(string image, string text) {
		var p = new StackPanel { Orientation = Orientation.Horizontal };
		var im = ImageUtil.LoadWpfImageElement(image);
		im.Margin = new(-2, 0, 4, 0);
		p.Children.Add(im);
		p.Children.Add(_tb = new TextBlock { Text = text });
		Content = p;
	}

	public void SetText(string text) {
		_tb.Text = text;
	}

	public void SetText(string text, string tooltip) {
		_tb.Text = text;
		ToolTip = tooltip;
	}
}
