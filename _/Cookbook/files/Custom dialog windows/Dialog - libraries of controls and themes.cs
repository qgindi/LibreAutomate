/// This example uses library <google>HandyControl</google>. NuGet: <+nuget>HandyControl<>. Applies its theme to all controls and adds several its controls to the dialog.

/*/ nuget -\HandyControl; /*/

using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using HC = HandyControl.Controls;
using System.ComponentModel;

var xaml = """
<Application xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:hc='https://handyorg.github.io/handycontrol'>
<Application.Resources>
<ResourceDictionary>
<ResourceDictionary.MergedDictionaries>
<ResourceDictionary Source='pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml'/>
<ResourceDictionary Source='pack://application:,,,/HandyControl;component/Themes/Theme.xaml'/>
</ResourceDictionary.MergedDictionaries>
</ResourceDictionary>
</Application.Resources>
</Application>
""";

XamlReader.Parse(xaml); //creates and sets Application object for this process. Its resources will be used by WPF windows.

var b = new wpfBuilder("Window").WinSize(400);
b.WinProperties(whiteBackground: true);
b.Options(modifyPadding: false, rightAlignLabels: true);

//standard WPF controls

b.R.Add("Text", out TextBox text1).Focus();
b.R.Add("Combo", out ComboBox combo1).Items("Zero|One|Two");
b.R.Add(out CheckBox c1, "Check");
b.R.AddButton("Button", _ => { print.it("Button clicked"); });

//HandyControl controls

b.R.Add(out HC.Divider div).Margin("10");
div.Content = "HandyControl controls";

b.R.Add("UpDown", out HC.NumericUpDown ud).Width(70, "L");
ud.Minimum = 0;
ud.Maximum = 10;

b.R.Add("CheckCombo", out HC.CheckComboBox ccb);
ccb.ItemsSource = Enum.GetValues<KMod>();

b.R.Add(out HC.SplitButton split, "Split");
split.Click += (o, e) => { if (e.OriginalSource == o) print.it("click"); };
var lb = new ListBox { BorderThickness = default, ItemsSource = new string[] { "One", "Two", "Three" } };
lb.SelectionChanged += (_, e) => { print.it(lb.SelectedItem); split.IsDropDownOpen = false; };
split.DropDownContent = lb;

var props = new PropertyClass { Bool = false, Text = "text" };
b.R.Add(out HC.PropertyGrid g);
g.SelectedObject = props;

b.R.AddOkCancel();
b.End();
if (!b.ShowDialog()) return;
print.it("UpDown", ud.Value);
print.it("Modifiers", ccb.SelectedItems);
print.it(props);

public record PropertyClass {
	[Category("Category1")]
	public bool Bool { get; set; }
	[Category("Category1")]
	public string Text { get; set; }
	[Category("Category2"), DisplayName("Enum")]
	public MButton Mouse { get; set; }
}
