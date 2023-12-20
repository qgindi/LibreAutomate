/// Use <see cref="System.Windows.Markup.XamlReader"/>.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

var xaml = """
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Style TargetType='Label'>
        <Setter Property='Padding' Value='1,1,1,1' />
    </Style>
    <Style TargetType='TextBox'>
        <Setter Property='Padding' Value='2,1,1,2' />
    </Style>
    <Style TargetType='PasswordBox'>
        <Setter Property='Padding' Value='2,1,1,2' />
    </Style>
    <Style TargetType='ComboBox'>
        <Setter Property='Padding' Value='5,2,4,3' />
    </Style>
    <Style TargetType='Button'>
        <Setter Property='Padding' Value='5,1,5,2' />
    </Style>
    <Style TargetType='CheckBox'>
        <Setter Property='HorizontalAlignment' Value='Left' />
    </Style>
    <Style TargetType='RadioButton'>
        <Setter Property='HorizontalAlignment' Value='Left' />
    </Style>
</ResourceDictionary>
""";
	
var rd = XamlReader.Parse(xaml) as ResourceDictionary;

var b = new wpfBuilder("Window");
b.Window.Resources = rd;
b.Options(modifyPadding: false);
b.R.Add("Test", out TextBox _)
	.R.Add("Test", out PasswordBox _)
	.R.Add("Test", out ComboBox _).Items("A|B|c")
	.R.Add("Test", out ComboBox _).Editable().Items("A|B|c")
	.R.Add(out CheckBox _, "CheckBox")
	.R.AddOkCancel();
if (!b.ShowDialog()) return;
