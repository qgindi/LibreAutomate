using System.Windows;
using System.Windows.Controls;
using Au.Controls;

namespace Au.Tools;

static class DCommandline {
	static FileNode _CurrentFile() {
		var f = App.Model.CurrentFile;
		if (f != null) {
			if (f.IsExecutableDirectly()) return f;
			dialog.showInfo(null, "This file isn't runnable as a script.", owner: App.Wmain);
		}
		return null;
	}
	
	static string _Quot(string s) {
		var q = s.Contains(' ') ? "\"" : "";
		return $"{q}{s}{q}";
	}
	
	public class Commandline : KDialogWindow {
		public static void ShowForCurrentFile() {
			if (_CurrentFile() is { } f) f.SingleDialog(() => new Commandline(f));
		}
		
		Commandline(FileNode f) {
			InitWinProp("Command line - " + f.Name, App.Wmain);
			var b = new wpfBuilder(this).WinSize(500);
			b.R.xAddInfoBlockT("This tool creates a command line string to run this script from other programs (cmd, PowerShell etc). More info in Cookbook folder \"Script\".");
			b.R.Add(out KCheckBox cEditorNoPath, "Editor program name without path").Tooltip("Makes the string shorter. Not all programs support it.");
			b.R.Add(out KCheckBox cWait, "Wait until script ends").Tooltip("Unchecked:  start the script and return its process id.\nChecked: run the script and return its exit code. The caller can get script.writeResult text.");
			b.R.Add("Script arguments", out TextBox tArgs).Tooltip("Optional");
			b.R.AddSeparator();
			b.R.StartStack();
			b.AddButton("Copy to clipboard", _ => {
				var sb = new StringBuilder();
				sb.Append(_Quot(cEditorNoPath.IsChecked ? process.thisExeName : process.thisExePath)).Append(' ');
				sb.Append(_Quot((cWait.IsChecked ? "*" : "") + f.ItemPathOrName()));
				if (tArgs.Text.NullIfEmpty_() is { } sa) sb.Append(' ').Append(sa);
				
				clipboard.text = sb.ToString();
			});
			b.AddOkCancel(null, "Close");
			b.End();
			b.End();
		}
	}
	
	public class Shortcut : KDialogWindow {
		public static void ShowForCurrentFile() {
			if (_CurrentFile() is { } f) f.SingleDialog(() => new Shortcut(f));
		}
		
		Shortcut(FileNode f) {
			InitWinProp("Shortcut - " + f.Name, App.Wmain);
			var b = new wpfBuilder(this).WinSize(500);
			b.R.xAddInfoBlockT("This tool creates a shortcut (.lnk file) to run this script.");
			b.R.Add("Script arguments", out TextBox tArgs).Tooltip("Optional");
			b.R.AddSeparator();
			b.R.StartStack();
			b.AddButton("Create shortcut...", _ => {
				if (App.IsPortable && 1 != dialog.showWarning("Portable mode warning", "This will create a shortcut file. Portable apps should not create files on host computer.\r\n\r\nDo you want to continue?", "1 Yes|2 No", owner: this)) return;
				var d = new FileOpenSaveDialog("38939d61-1971-45f5-8ce5-0c405aab792c") {
					FileNameText = App.Model.CurrentFile.DisplayName + ".lnk",
					FileTypes = "Shortcut|*.lnk",
					InitFolderFirstTime = folders.Desktop,
					Title = "Create shortcut"
				};
				if (!d.ShowSave(out var lnk, this)) return;
				var s = _Quot(f.ItemPathOrName());
				if (tArgs.Text.NullIfEmpty_() is { } sa) s = s + " " + sa;
				try {
					using var sh = shortcutFile.create(lnk);
					sh.TargetPath = process.thisExePath;
					sh.Arguments = s;
					sh.Save();
					Close();
				}
				catch (Exception e1) { dialog.showError("Failed", e1.ToStringWithoutStack(), owner: this); }
			});
			b.AddOkCancel(null);
			b.End();
			b.End();
		}
	}
	
	public class ShellMenu : KDialogWindow {
		public static void ShowForCurrentFile() {
			if (_CurrentFile() is { } f) f.SingleDialog(() => new ShellMenu(f));
		}
		
		[Flags] enum _Type { file = 1, directory = 2, drive = 4, back = 8, desktop = 16, taskbar = 32 }
		
		const string infoUrl = "https://www.libreautomate.com/forum/showthread.php?tid=7819";
		
		ShellMenu(FileNode f) {
			InitWinProp("Shell menu - " + f.Name, App.Wmain);
			var b = new wpfBuilder(this).WinSize(550);
			b.Options(bindLabelVisibility: true);
			b.R.xAddInfoBlockF($"""
This tool creates <a href='{infoUrl}'>Nilesoft Shell</a> code for adding a menu item to run this script (or submenu etc). Paste in a nss file. To apply: Ctrl+right-click in a folder window or desktop.
""");
			
			b.R.xAddGroupSeparator("Menu item properties");
			b.R.Add("Title", out KTextBox tTitle, f.DisplayName);
			b.R.Add("Image", out KTextBox tImage).Tooltip(@"A font icon, like \uE025 or image.glyph(\uE025, #00e000)
Or path of a file with icons, like c:\dir\file.ico, c:\dir\file.exe, c:\dir\file.dll,3
Or color, like #c0ff80");
			//b.And(26).xAddButtonIcon("*Material.HelpCircleOutline #79AEFF" + Menus.blue, _ => _ImageButton(), "Image reference");
			b.And(26).xAddControlHelpButton(_ => _ImageButton(), "Image reference");
			b.R.Add("Tooltip", out KTextBox tTip).Multiline();
			b.R.Add<Label>("Separator"); b.StartStack().Add(out KCheckBox cSepBefore, "before").Add(out KCheckBox cSepAfter, "after").End();
			//b.R.Add(out KCheckBox cMenu, "Add to an existing submenu"); //rejected. Rarely used. Also would need pos.
			
			b.R.xAddGroupSeparator("Show if");
			b.R.Add("Selection", out ComboBox cbMode).Items(
				"default setting (inherit from parent menu, or single)",
				"single (selected single item)",
				"multiple (selected one or more items)",
				"multi_unique (selected one or more items of the same object type)",
				"multi_single (selected one or more items of the same file type)",
				"none (no selection)");
			
			b.R.Add<Label>("Object type").StartPanel(new WrapPanel()).Margin("T3B3");
			var ty = new EnumUI<_Type>(b.Panel, _Type.file);
			b.End();
			
			b.R.Add("File types", out KTextBox tFileTypes).Tooltip("Examples:\n.exe\n.png|.jpg|.ico\n!.exe (not .exe)");
			b.R.Add(out KCheckBox cShift, "+Shift");
			
			b.R.xAddGroupSeparator("Action");
			b.R.Add("Action", out ComboBox cbAction).Items("Run this script|Run program|Expression|Submenu");
			b.R.Add("Program", out KTextBox tProgram).Hidden(null);
			b.R.Add("Expression", out KTextBox tExpression).Hidden(null);
			b.R.Add("Arguments", out KTextBox tArgs, "@sel(true)")
				.Tooltip("@sel(true) means \"paths of selected files\".\nYou can add more arguments before or after.");
			var bArgs = b.And(26).xAddButtonIcon("*Modern.LanguageCsharp" + Menus.blue, _ => _ArgsButton(), "Copy C# code");
			cbAction.SelectionChanged += (_, _) => {
				int i = cbAction.SelectedIndex;
				tProgram.Visibility = i == 1 ? Visibility.Visible : Visibility.Collapsed;
				tExpression.Visibility = i == 2 ? Visibility.Visible : Visibility.Collapsed;
				tArgs.Visibility = bArgs.Visibility = i < 2 ? Visibility.Visible : Visibility.Collapsed;
			};
			
			b.R.AddSeparator().Margin("T8");
			b.R.StartGrid().Columns(130, 0, -1, 100, 100);
			b.AddButton("Copy to clipboard", _1 => {
				static string _SA(string prop, string s) => s.NE() ? null : _SA2(prop, s);
				static string _SA2(string prop, string s) => $"{prop}='{s?.Replace("'", "@\"'\"")}'";
				static string _SQ(string prop, string s) => s.NE() ? null : $"{prop}=\"{s.Escape()}\"";
				
				var sImage = tImage.Text;
				if (!sImage.NE()) {
					if (pathname.isFullPath(sImage, orEnvVar: true) || sImage.RxIsMatch(@"\.\w{3}(?:, *-?\d+)?$")) {
						sImage = _SA("image", sImage);
					} else {
						if (sImage.RxMatch(@"(?i)^(\\u)?([[:xdigit:]]{4})$", out var m)) {
							string u = m[1].Value ?? "\\u", h = m[2].Value;
							if (h[1] is >= '7') sImage = $"image.segoe({u}{h}, 14)"; //Windows icon font. DPI problems. 14 best, but not perfect. `image.glyph` bad.
							else sImage = u + h; //NS embedded glyphs
						}
						sImage = $"image={sImage}";
					}
				}
				var sCommon = $"""
({_SA2("title", tTitle.Text)}
	{sImage}
	{_SQ("tip", tTip.Text)}
	{_SA("sep", (cSepBefore.IsChecked, cSepAfter.IsChecked) switch { (true, false) => "before", (false, true) => "after", (true, true) => "both", _ => null })}
	{(cbMode.SelectedIndex == 0 ? "" : _SA("mode", (cbMode.SelectedItem as string).Split(' ', 2)[0]))}
	{(ty.Result == 0 ? "" : _SA("type", ty.Result.ToString().Replace(", ", "|")))}
	{_SA("find", tFileTypes.Text)}
	{(cShift.IsChecked ? "vis=key.shift()" : "")}
""".RxReplace(@"(?m)(\R\t)+$", "");
				string s;
				int i = cbAction.SelectedIndex;
				if (i == 3) { //submenu
					s = $$"""
menu{{sCommon}})
{

}

""";
				} else {
					var sca = i switch {
						0 => $"cmd='{process.thisExeName}' {_SA("args", $"\"{f.ItemPathOrName()}\" {tArgs.Text}")}",
						1 => $"{_SA2("cmd", tProgram.Text)} {_SA2("args", tArgs.Text)}",
						_ => $"cmd={tExpression.Text}"
					};
					s = $"""
item{sCommon}
	{sca})

""";
					//rejected: add UI for other command parameters: invoke, window, dir, admin, verb, wait.
				}
				clipboard.text = s;
			});
			b.AddOkCancel(null, "Close");
			b.Skip().AddButton("Open nss file", _ => _OpenNSS());
			b.AddButton("On error ▾", _ => _OnErrorButton());
			b.End();
			b.End();
			
			void _ImageButton() {
				var m = new popupMenu();
				m["Glyph icons"] = o => { run.it("https://nilesoft.org/gallery/glyphs"); };
				m["Win11 font icons (Fluent)"] = o => { run.it("https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font"); };
				m["Win10 font icons (MDL2)"] = o => { run.it("https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font"); };
				m.Separator();
				m["Image function"] = o => { run.it("https://nilesoft.org/docs/functions/image"); };
				m["Image property"] = o => { run.it("https://nilesoft.org/docs/configuration/properties#image"); };
				m.Show(owner: this);
			}
			
			void _ArgsButton() {
				var m = new popupMenu();
				m["Copy C# code \"foreach selected path\""] = o => { clipboard.text = """
foreach (string path in args) {
	print.it(path);
}

"""; };
				m["Copy C# code \"foreach selected path\" when there are more arguments"] = o => { clipboard.text = """
foreach (string path in args.Skip(1)) {
	print.it(path);
}

"""; };
				m.Show(owner: this);
			}
			
			void _OnErrorButton() {
				var m = new popupMenu();
				m["Print last error"] = o => { if (_GetNsDir(out var nsDir)) try { print.it(File.ReadLines(nsDir + @"\shell.log").Where(o => o.Contains("[error]")).Last()); } catch { } };
				m["Restart Explorer"] = o => { if (_GetNsDir(out var nsDir)) run.itSafe(nsDir + @"\shell.exe", "-restart"); };
				m.Show(owner: this);
			}
			
			void _OpenNSS() {
				if (!_GetNsDir(out var nsDir)) return;
				var nssFile = nsDir + @"\shell.nss";
				try {
					var text = filesystem.loadText(nssFile);
					if (text.RxFindAll(@"(?m)^\h*import +'(?!imports[/\\])(.+?)'", 1, out string[] a)) {
						nssFile = a[0];
						if (a.Length > 1) {
							int i = dialog.showList(a.Select(o => pathname.getName(o)).ToArray(), owner: this) - 1;
							if (i < 0) return;
							nssFile = a[i];
						}
					} else {
						if (1 != dialog.show("Your nss file still not imported into shell.nss", "Open shell.nss?", buttons: "1 OK|0 Cancel", footer: $"<a href=\"{infoUrl}\">Setup info</a>", onLinkClick: e => { run.itSafe(e.LinkHref); }, owner: this)) return;
					}
					if (!App.Model.OpenAndGoTo(nssFile, kind: FNFind.File)) run.itSafe(nssFile);
				}
				catch { }
			}
			
			bool _GetNsDir(out string r) {
				r = App.Settings.nilesoftShellDir;
				if (!filesystem.exists(r).Directory) {
					r = folders.ProgramFiles + "Nilesoft Shell";
					while (!filesystem.exists(r).Directory) {
						if (!dialog.showInput(out r, "Where is Nilesoft Shell installed?", $"Nilesoft Shell folder path", footer: $"<a href=\"{infoUrl}\">Setup info</a>", onLinkClick: e => { run.itSafe(e.LinkHref); }, owner: this)) return false;
						App.Settings.nilesoftShellDir = r;
					}
				}
				return true;
			}
		}
	}
}
