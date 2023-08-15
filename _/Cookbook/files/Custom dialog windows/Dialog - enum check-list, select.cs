/// Class <see cref="EnumUI{T}"/> can be used to easily display enum members in a popup menu or WPF dialog as checkboxes or combo box. Classes <b>popupMenu</b> and <b>wpfBuilder</b> have function <b>AddEnum</b> that makes it even easier.

using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Window").WinSize(300);

b.R.AddButton("Context menu", o => {
	var m = new popupMenu();
	var e1 = m.AddEnum<KMod>(KMod.Ctrl|KMod.Alt); //a [Flags] enum
	m.Separator();
	var e2 = m.AddEnum<DayOfWeek>(DateTime.Today.DayOfWeek); //a non-[Flags] enum
	var r = o.Button.RectInScreen();
	m.Show(MSFlags.AlignRectBottomTop, excludeRect: r, owner: o.Window);
	print.it(e1.Result);
	print.it(e2.Result);
});

b.R.AddEnum<KMod>(out var e1, KMod.Ctrl | KMod.Alt, label: "Modifiers", vertical: false);
b.R.AddEnum<DayOfWeek>(out var e2, DateTime.Today.DayOfWeek, label: "Day");
b.R.AddOkCancel();
if (!b.ShowDialog()) return;
print.it(e1.Result);
print.it(e2.Result);
