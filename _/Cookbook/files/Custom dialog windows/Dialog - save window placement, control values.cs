/// To save something, at first need to decide where. This example uses a JSON file and <see cref="JSettings"/>. See recipe <+recipe>saving variables<>.

using System.Windows;
using System.Windows.Controls;

using var sett = MySettings.Load();

var b = new wpfBuilder("Window").WinSize(300, 300);
b.R.Add(out CheckBox check1, "Check").Checked(sett.checked1);
b.Row(-1).Add("Text", out TextBox _).Multiline();
b.R.AddOkCancel();
b.End();

b.WinSaved(sett.placement, o => {
	sett.placement = o;
	sett.checked1 = check1.IsChecked == true;
});

if (!b.ShowDialog()) return;


record class MySettings : JSettings {
	public static readonly string File = folders.ThisAppDocuments + @"Dialog save 1.json";

	public static MySettings Load() => Load<MySettings>(File);
	
	public string placement;
	public bool checked1;
}
