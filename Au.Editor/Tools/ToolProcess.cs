using System.Runtime.Loader;

namespace ToolLand;

/// <summary>
/// Tools like "Find UI element" run in a separate Au.Editor.exe process. They are implemented in Au.Controls.dll.
/// The <c>StartX</c> functions are used by the main Au.Editor.exe process to start a tool process.
/// Then <c>Run</c> is called in a tool process.
/// </summary>
static class ToolProcess {
	public static void StartDwnd(wnd w = default, DwndFlags flags = 0) {
		_Start($"Dwnd {w.Handle} {(int)flags}");
	}
	
	public static void StartDelm(POINT? p = null) {
		_Start($"Delm {p?.x} {p?.y}");
	}
	
	public static void StartDuiimage(wnd wCapture = default) {
		_Start($"Duiimage {wCapture.Handle}");
	}
	
	public static void StartDocr() {
		_Start($"Docr");
	}
	
	static void _Start(string commandline) {
		var ps = new ProcessStarter_(process.thisExePath, $"/tool {LA.App.Hmain.Handle} {commandline}", rawExe: true);
		ps.Start();
	}
	
	public static void Run(ReadOnlySpan<string> args) {
		//perf.first();
		
		try {
			var poDir = folders.ThisAppDataCommon + "optimization";
			if (!Directory.Exists(poDir)) Directory.CreateDirectory(poDir);
			AssemblyLoadContext.Default.SetProfileOptimizationRoot(poDir);
			AssemblyLoadContext.Default.StartProfileOptimization(args[0]);
		}
		catch (Exception ex) { Debug_.Print(ex); }
		
		Task task1 = Task.Run(() => { LA.AppSettings.Load(); }); //makes startup faster, although with PO not so much
		
		_ToolProcess(args[1..], (wnd)args[0].ToInt(), task1);
	}
	
	static void _ToolProcess(ReadOnlySpan<string> args, wnd wMain, Task task1) {
		//perf.next();
		process.ThisThreadSetComApartment_(ApartmentState.STA);
		process.thisProcessCultureIsInvariant = true;
		LA.App.InitThisAppFoldersEtc_();
		AssemblyLoadContext.Default.Resolving += (alc, an) => alc.LoadFromAssemblyPath($@"{folders.ThisAppBS}\Roslyn\{an.Name}.dll");
		
		new System.Windows.Window { Content = new System.Windows.Controls.Button() };
		//perf.next();
		task1.Wait();
		//perf.next('t');
		//timer.after(1, _ => { perf.nw(); });
		
		timer.after(1000, _1 => {
			Task.Run(() => { //warm-up compiler
				try { if (!Scripting.Compile("static void M() { print.it(1); }", out _, true, true, true, true)) throw null; }
				catch (Exception ex) { Debug_.Print(ex); }
			});
			
			timer2.every(200, _ => { //exit when main window closed
				if (!wMain.IsAlive) Api.ExitProcess(0); //Environment.Exit not always works when main thread is hung
			});
		});
		
		if (args[0] is "Dwnd") {
			var d = new Dwnd((wnd)args[1].ToInt(), (DwndFlags)args[2].ToInt());
			d.ShowDialog();
		} else if (args[0] is "Delm") {
			var d = new Delm(args.Length > 2 ? new POINT(args[1].ToInt(), args[2].ToInt()) : null);
			d.ShowDialog();
		} else if (args[0] is "Duiimage") {
			var d = new Duiimage((wnd)args[1].ToInt());
			d.ShowDialog();
		} else if (args[0] is "Docr") {
			var d = new Docr();
			d.ShowDialog();
		}
	}
}
