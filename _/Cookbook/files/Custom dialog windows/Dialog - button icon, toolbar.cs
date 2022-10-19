using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

var b = new wpfBuilder("Window").WinSize(250);

//standard button with icon and text
b.R.AddButton(_IconText("*Material.Debian #4DBF00", "Abc"), _ => { print.it("click"); });

//toolbar with button and checkbox
b.R.Add(out ToolBar tbar);
ToolBarTray.SetIsLocked(tbar, true);
b.Items(
	_TBButton("*MaterialDesign.History #0D69E1", "Button tooltip", _ => { print.it("click"); }),
	_TBCheckbox("*Modern.Stream #0D69E1", "Checkbox tooltip")
	);

if (!b.ShowDialog()) return;

static StackPanel _IconText(string icon, string text) {
	var p = new StackPanel { Orientation = Orientation.Horizontal };
	p.Children.Add(ImageUtil.LoadWpfImageElement(icon));
	p.Children.Add(new TextBlock { Text = text });
	return p;
}

static Button _TBButton(string icon, string tooltip, Action<Button> click) {
	var c = new Button { Content = ImageUtil.LoadWpfImageElement(icon), ToolTip = tooltip };
	if (click != null) c.Click += (_, _) => click(c);
	return c;
}

static CheckBox _TBCheckbox(string icon, string tooltip, Action<CheckBox> click = null) {
	var c = new CheckBox { Content = ImageUtil.LoadWpfImageElement(icon), ToolTip = tooltip };
	if (click != null) c.Click += (_, _) => click(c);
	return c;
}
