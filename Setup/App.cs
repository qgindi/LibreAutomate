/*/
role exeProgram
optimize true
define DEV,NO_GLOBAL,NO_DEFAULT_CHARSET_UNICODE
outputPath %folders.Workspace%\exe\App
c \Au.sln\@Au.Editor\resources\global2.cs
/*/

global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.IO;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Text.RegularExpressions;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.Win32;
global using System.Windows;

using System.Windows.Controls;

#if !EMPTY
static class App {
	[STAThread]
	static int Main(string[] args) {
#if NET
		Au.print.clear();
#endif
		if (!Environment.Is64BitOperatingSystem) {
			Msgbox("Not 64-bit Windows.", icon: MessageBoxImage.Error);
			return -1;
		}
		
		//need UAC elevation?
		_runsRestartedElevated = args.Length > 0 && args[^1] is var last && last == "/elevated";
		if (!_runsRestartedElevated && _RestartElevated(out int exitCode)) return exitCode;
		
		using var mutex = new Mutex(initiallyOwned: true, @"Global\LA-setup-e5acdbab83bc", createdNew: out bool firstInstance);
		if (!firstInstance) return -1;
		
		bool uninstall = Process.GetCurrentProcess().ProcessName.Equals("uninstall", StringComparison.OrdinalIgnoreCase);
		foreach (var v_ in args) {
			if (v_.Length == 0) continue;
			if (v_[0] is '/' or '-') {
				var v = v_.Substring(1).ToUpperInvariant();
				switch (v) {
				case "UNINSTALL":
					uninstall = true;
					break;
				case "SILENT" or "VERYSILENT" or "QUIET":
					Installer.Silent = true;
					break;
				}
			}
		}
		
#if !NET
		if (!Installer.EnsureAppNotRunning()) return -1;
#endif
		Installer.UnloadDll();
		
		//uninstall = true;
		if (uninstall) {
			return Uninstall();
		} else {
			return Install();
		}
	}
	
	static bool _runsRestartedElevated;
	static bool _noDotnet;
	
	static int Install() {
		var dir = Installer.Reg.GetPreviousDir() ?? (folders.ProgramFiles + AppName);
		
		_noDotnet = !DotnetInfo.IsInstalled();
		bool isOffline = Installer.IsOffline;
		
		bool installedOK = false;
		if (Installer.Silent) {
			var x = new Installer(dir, o => Util.Print(o));
			installedOK = x.InstallApp();
			if (_noDotnet && !isOffline) x.InstallDotnet();
		} else {
			var w = _window = new Window {
				Title = c_winTitle,
				Width = 400,
				WindowStartupLocation = WindowStartupLocation.CenterScreen,
				ResizeMode = ResizeMode.CanMinimize,
				ShowInTaskbar = true,
				SizeToContent = SizeToContent.Height,
				Background = SystemColors.ControlBrush,
			};
			
			var g = new StackPanel { Margin = new(8) };
			w.Content = g;
			Thickness margin = new(0, 8, 0, 8), margin2 = new(0, 0, 0, 3);
			
			var tInfo = new TextBlock { Margin = margin, Text = $"Setup will install LibreAutomate {AppVersion}." };
			g.Children.Add(tInfo);
			g.Children.Add(new Separator { Margin = margin });
			
			double panelHeight = _noDotnet ? 100 : 50;
			
			var pMain = new StackPanel { Height = panelHeight };
			g.Children.Add(pMain);
			pMain.Children.Add(new TextBlock { Text = "Folder", Margin = margin2 });
			var tFolder = new TextBox { Height = 21, Text = dir };
			pMain.Children.Add(tFolder);
			
			CheckBox cDotnet = null;
			if (_noDotnet) {
				pMain.Children.Add(new Separator { Margin = margin });
				pMain.Children.Add(new TextBlock { Text = $"The required .NET Desktop Runtime {DotnetInfo.VersionXX} is not installed.", Margin = margin2 });
				cDotnet = new() { Content = "Install .NET Desktop Runtime now", IsChecked = !isOffline };
				cDotnet.Unchecked += (_, _) => { Msgbox("LibreAutomate cannot run without the runtime. But you can install it later manually. The download size is ~60 MB."); };
				pMain.Children.Add(cDotnet);
			}
			
			var pFinal = new StackPanel { Height = panelHeight, Visibility = Visibility.Collapsed };
			g.Children.Add(pFinal);
			var cRunLA = new CheckBox { Content = "Run LibreAutomate now", IsChecked = true };
			pFinal.Children.Add(cRunLA);
			
			g.Children.Add(new Separator { Margin = margin });
			
			var pButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
			g.Children.Add(pButtons);
			
			var bInstall = new Button { Content = "Install", Width = 70, Height = 22, IsDefault = true };
			pButtons.Children.Add(bInstall);
			
			bool clickedFinish = false;
			var bFinish = new Button { Content = "Finish", Width = 70, Height = 22, HorizontalAlignment = HorizontalAlignment.Center, IsDefault = true, Visibility = Visibility.Collapsed };
			pButtons.Children.Add(bFinish);
			bFinish.Click += (_, _) => { clickedFinish = true; w.Close(); };
			
			var bCancel = new Button { Content = "Cancel", Width = 70, Height = 22, Margin = new(6, 0, 0, 0), IsCancel = true };
			pButtons.Children.Add(bCancel);
			bCancel.Click += (_, _) => { w.Close(); };
			
			var bHelp = new Button { Content = "Help", Width = 70, Height = 22, Margin = new(6, 0, 0, 0) };
			pButtons.Children.Add(bHelp);
			bHelp.Click += (_, _) => _Help();
			
			bInstall.Click += async (_, _) => {
				if (tFolder.Text is { Length: > 0 } sd) {
					if (!Util.IsFullPath(sd)) { Msgbox("Must be full path."); return; }
					dir = sd;
				}
				w.IsEnabled = false;
				var x = new Installer(dir, o => { tInfo.Dispatcher.InvokeAsync(() => { tInfo.Text = o; }); });
				installedOK = await Task.Run(x.InstallApp);
				if (installedOK) {
					if (_noDotnet && cDotnet.IsChecked == true) {
						bool dotnetOK = await Task.Run(x.InstallDotnet);
						if (dotnetOK) tInfo.Text = "Done"; //else InstallDotnet sets error text
					} else {
						tInfo.Text = "Done";
					}
					bInstall.Visibility = Visibility.Collapsed;
					bFinish.Visibility = Visibility.Visible;
					pMain.Visibility = Visibility.Collapsed;
					pFinal.Visibility = Visibility.Visible;
					
					if (x.DontRunApp) {
						cRunLA.IsChecked = false;
						cRunLA.Visibility = Visibility.Hidden;
					}
				} else {
					tInfo.Text += " - failed";
					bInstall.Content = "Retry";
				}
				w.IsEnabled = true;
			};

			w.Loaded += async (_, _) => {
				//after UAC consent etc the window may be behind other windows. Maybe this workaround does not fix it, but at least then the taskbar button flashes.
				_ = w.Dispatcher.InvokeAsync(() => Util.ActivateWindowAsync(w));
			};
			
			w.Closing += (_, e) => {
				if (Installer.DontInterrupt) e.Cancel = true;
			};
			
			var app = new Application();
			app.Run(w);
			if (installedOK && clickedFinish && cRunLA.IsChecked == true) {
				if (_runsRestartedElevated) return 1;
				_RunLA();
			}
		}
		
		return installedOK ? 0 : -1;
	}
	
	static int Uninstall() {
		if (!Installer.Silent) if (Msgbox("Do you want to uninstall LibreAutomate?", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return -2;
		
		string dir = Installer.Reg.GetPreviousDir();
		if (dir is null || !Directory.Exists(dir)) {
			dir = folders.ThisApp;
		}
		var x = new Installer(dir, o => Util.Print(o));
		if (!x.UninstallApp()) return -3;
		
		if (!Installer.Silent) Msgbox("Uninstalled.");
		return 0;
	}
	
	static Window _window;
	
	const string c_winTitle = "LibreAutomate setup";
	
#if DEV
	public const string AppName = "LA test";
#else
	public const string AppName = "LibreAutomate";
#endif
	
	public static readonly string AppVersion = Au.More.Au_.Version;
	
	public static MessageBoxResult Msgbox(string text, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None) {
		if (_window == null) return MessageBox.Show(text, c_winTitle, buttons, icon);
		return _window.Dispatcher.Invoke(() => MessageBox.Show(_window, text, c_winTitle, buttons, icon));
	}
	
	public static MessageBoxResult Msgbox(string text, Exception ex, MessageBoxImage icon = MessageBoxImage.Error) {
		return Msgbox($"{text}\n\n{ex}", icon: icon);
	}
	
	static bool _RestartElevated(out int exitCode) {
		exitCode = 0;
		
		try {
			Registry.LocalMachine.OpenSubKey("SOFTWARE", System.Security.AccessControl.RegistryRights.CreateSubKey).Dispose();
			return false;
		}
		catch { }
		
		var psi = new ProcessStartInfo {
			UseShellExecute = true,
			FileName = Util.ThisExePath,
			Arguments = Util.ThisExeArgs + " /elevated",
			Verb = "RunAs",
			WorkingDirectory = Environment.CurrentDirectory
		};
		try {
			var p = Process.Start(psi);
			p.WaitForExit();
			exitCode = p.ExitCode;
			
			if (exitCode == 1) {
				exitCode = 0;
				_RunLA();
			}
		}
		catch { }
		
		return true;
	}
	
	static void _RunLA() {
		if (Registry.GetValue(Installer.Reg.AppPathKey, "", null) is string s) {
			try { Process.Start(s); }
			catch { }
		}
	}
	
	static void _Help() {
		var s = $"""
By default, setup downloads LibreAutomate program files from GitHub.{(_noDotnet ? " Also downloads the .NET Desktop Runtime from Microsoft." : "")}

Or you can install offline. Download the .lzma files from the same GitHub release page to the setup program's folder. Then run setup. It will not use the internet.{(_noDotnet ? " It will not install .NET." : "")}

Current mode: {(Installer.IsOffline ? "" : "not ")}offline.

Command line switches:
/SILENT - don't show the setup window and error message boxes.
/UNINSTALL - uninstall LibreAutomate.
""";
		Msgbox(s);
	}
}
#else
static class App {
	[STAThread]
	static void Main(string[] args) {
	}
}
#endif
