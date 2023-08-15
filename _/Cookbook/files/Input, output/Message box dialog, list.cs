/// To quickly insert code for <see cref="dialog.show"/> and other <see cref="dialog"/> functions, use snippet dsDialogShowSnippet: type ds and select from the list.

dialog.show("Simplest message box", "Text.");

dialog.show("Message box with timeout", "Text.", secondsTimeout: 10);

dialog.show("Position, title, icon", x: ^100, title: "Custom title", icon: DIcon.Info);

dialog.showError("Error", "Description.");

if (!dialog.showOkCancel("Continue?")) return;

if (dialog.showYesNo("Icecream?")) {
	print.it("Yes");
} else {
	print.it("No");
}

bool yes = 1 == dialog.show("Default button", buttons: "1 Yes|2 No", defaultButton: 2);
print.it(yes);

int button = dialog.show(null, "Custom buttons.", "0 Cancel|1 Button1|2 Button2\nMore info.", flags: DFlags.CommandLinks);
switch (button) {
case 1: print.it(1); break;
case 2: print.it(2); break;
default: return;
}

/// Dialogs with multiple buttons like a list.

int button1 = dialog.showList("one|two|three", "Simple list");
print.it(button1);

var a = new List<string>(); for (int i = 1; i <= 10; i++) a.Add($"Button {i}");
int button2 = dialog.showList(a, "Variable list");
print.it(button2);

/// Dialogs with various controls.

var c = new DControls() { Checkbox = "Check", IsChecked = true, EditText = "A", EditType = DEdit.Combo, ComboItems = "A|B|C", RadioButtons = "R1|R2|R3" };
int button3 = dialog.show("Controls", "Text.", controls: c);
print.it(button3, c.IsChecked, c.EditText, c.RadioId);

/// Set default properties of all dialog windows.

dialog.options.defaultTitle = "Default title";
dialog.options.useAppIcon = true;
dialog.show("Example");

/// More features are available when using a <b>dialog</b> class instance.

var d = new dialog("Example") {
	Screen = screen.ofMouse,
	Width = 900,
};
d.ShowDialog();

/// See also <+recipe Dialog ->custom dialogs<>.
