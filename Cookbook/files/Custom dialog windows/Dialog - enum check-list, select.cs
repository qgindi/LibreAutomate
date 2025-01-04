/// Class <see cref="EnumUI{T}"/> can be used to easily display enum members in a popup menu or WPF dialog as checkboxes or combo box.

using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Window").WinSize(300);

b.R.AddButton("Context menu", o => {
	var m = new popupMenu();
	var e1 = new EnumUI<KMod>(m, KMod.Ctrl|KMod.Alt); //a [Flags] enum
	m.Separator();
	var e2 = new EnumUI<DayOfWeek>(m, DateTime.Today.DayOfWeek); //a non-[Flags] enum
	var r = o.Button.RectInScreen();
	m.Show(PMFlags.AlignRectBottomTop, excludeRect: r, owner: o.Window);
	print.it(e1.Result);
	print.it(e2.Result);
});

b.R.StartStack<GroupBox>("Modifiers");
var e1 = new EnumUI<KMod>(b.Panel, KMod.Ctrl | KMod.Alt);
b.End();

b.R.Add("Day", out ComboBox cb);
var e2 = new EnumUI<DayOfWeek>(cb, DateTime.Today.DayOfWeek);

b.R.StartGrid<Expander>("File attributes").Columns(0, -1);
var e3 = new EnumUI<FileAttributes>(b.Panel);
b.End();

b.R.AddOkCancel();
if (!b.ShowDialog()) return;
print.it(e1.Result);
print.it(e2.Result);
print.it(e3.Result);
