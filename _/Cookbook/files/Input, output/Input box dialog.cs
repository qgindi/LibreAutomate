/// To quickly insert <see cref="dialog.showInput"/> code, use snippet dsDialogShowSnippet: type ds and select from the list.

if (!dialog.showInput(out string s, "Text input")) return;
print.it(s);

if (!dialog.showInputNumber(out int i1, "Number input", editText: 0)) return;
print.it(i1);

if (!dialog.showInput(out string sML, "Multiline text input", editType: DEdit.Multiline)) return;
print.it(sML);

if (!dialog.showInput(out string pw, "Password input", editType: DEdit.Password)) return;
print.it(pw);

if (!dialog.showInput(out string s1, "Text input combo", editType: DEdit.Combo, comboItems: "One|Two|Three")) return;
print.it(s1);

var a = new List<string>(); for (int i = 1; i <= 10; i++) a.Add($"Item {i}");
if (!dialog.showInput(out string s2, "Text input variable combo", editType: DEdit.Combo, comboItems: a)) return;
print.it(s2);

var c = new DControls() { Checkbox = "Check", IsChecked = true };
if (!dialog.showInput(out string s3, "Text input and checkbox", controls: c)) return;
print.it(s3, c.IsChecked);

