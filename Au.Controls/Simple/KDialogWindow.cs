using System.Windows;

namespace Au.Controls;

/// <summary>
/// Can be used as base class for WPF windows used as dialogs.
/// Adds WS_POPUP style, which prevents activating an unrelated window when closing this active owned nonmodal window (OS bug).
/// Also adds functions <see cref="ShowSingle"/>, <see cref="ShowAndWait"/>.
/// </summary>
public class KDialogWindow : Window {
	/// <summary>
	/// Shows window of type T (the caller's class) and ensures that there is single window of that type at a time. 
	/// If a window of type T already exists (created with this function), activates it. Else calls <i>fNew</i> and <b>Show</b>. Will use <b>OnClose</b> to forget that window.
	/// Not thread-safe.
	/// Example: <c>ShowSingle(() => new ThisClass());</c>
	/// </summary>
	/// <param name="fNew">Called to create new T if there is no T window.</param>
	protected static T ShowSingle<T>(Func<T> fNew) where T : KDialogWindow {
		Debug_.PrintIf(Environment.CurrentManagedThreadId != 1);
		var v = s_single.FirstOrDefault(static o => o is T);
		if (v == null) {
			s_single.Add(v = fNew());
			v.Show();
		} else {
			v.Hwnd().ActivateL(true);
		}
		return v as T;
	}
	static HashSet<KDialogWindow> s_single = new();
	
	protected override void OnClosed(EventArgs e) {
		s_single.Remove(this);
		base.OnClosed(e);
	}
	
	/// <summary>
	/// If a window of type T already exists (created with <see cref="ShowSingle"/>), gets it and returns true, else returns false.
	/// </summary>
	protected static bool GetSingle<T>(out T window) where T : KDialogWindow
		=> null != (window = s_single.FirstOrDefault(static o => o is T) as T);
	
	/// <summary>
	/// Sets <b>Title</b>, <b>Owner</b>, <b>ShowInTaskbar</b>.
	/// </summary>
	/// <param name="owner">Window or element or null.</param>
	public void InitWinProp(string title, DependencyObject owner, bool showInTaskbar = false) {
		Title = title;
		if (owner != null) Owner = owner as Window ?? GetWindow(owner);
		ShowInTaskbar = showInTaskbar;
	}
	
	protected override void OnSourceInitialized(EventArgs e) {
		var w = this.Hwnd();
		w.SetStyle(WS.POPUP, WSFlags.Add);
		if (Environment.CurrentManagedThreadId != 1) w.Prop.Set("close me on exit", 1);
		base.OnSourceInitialized(e);
	}
	
	/// <summary>
	/// Sets <b>Owner</b> and calls <b>ShowDialog</b> without disabling thread windows.
	/// </summary>
	/// <returns>True if clicked OK (<b>DialogResult</b> true).</returns>
	/// <param name="owner">If not null, sets <b>Owner</b>.</param>
	/// <param name="hideOwner">Temporarily hide owner.</param>
	/// <param name="disableOwner">Temporarily disable owner.</param>
	public bool ShowAndWait(Window owner = null, bool hideOwner = false, bool disableOwner = false) {
		if (owner != null) Owner = owner;
		wnd ow = hideOwner || disableOwner ? Owner.Hwnd() : default;
		if (hideOwner) ow.ShowL(false); //not owner.Hide(), it closes owner if it is modal
		if (disableOwner) {
			ow.Enable(false);
			Closing += (_, e) => { if (!e.Cancel) ow.Enable(true); }; //the best time to enable. Later would activate wrong window.
		}
		
		//To prevent disabling thread windows, temporarily disable all visible enabled thread windows.
		//	See WPF code in Window.cs functions EnableThreadWindows, ThreadWindowsCallback, ShowDialog.
		//	Disabling/enabing a window is fast and does not send messages to it, even wm_stylechanging/ed.
		//	Another way: Show and Dispatcher.PushFrame. Problem: does not set DialogResult. How to know how the dialog was closed?
		bool reenable = false;
		var tw = wnd.getwnd.threadWindows(process.thisThreadId, onlyVisible: true);
		for (int i = 0; i < tw.Length; i++) { if (tw[i].IsEnabled()) { reenable = true; tw[i].Enable(false); } else tw[i] = default; }
		RoutedEventHandler eh = null; //would be less code with Dispatcher.InvokeAsync or timer, but unreliable, eg can be too soon or interfere with another dialog
		if (reenable) Loaded += eh = (_, _) => {
			eh = null;
			foreach (var v in tw) if (!v.Is0) v.Enable(true);
		};
		
		try { return ShowDialog() == true; }
		finally {
			eh?.Invoke(null, null); //if failed to load
			if (hideOwner) { ow.ShowL(true); ow.ActivateL(); }
		}
	}
}
