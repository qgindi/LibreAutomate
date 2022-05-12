using System.Windows;

namespace Au.Controls;
/// <summary>
/// Can be used as base class for WPF windows used as dialogs.
/// Adds WS_POPUP style, which prevents activating an unrelated window when closing this active owned nonmodal window (OS bug).
/// </summary>
public class KDialogWindow : Window {
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
	/// <param name="owner"></param>
	/// <param name="hideOwner">Temporarily hide owner.</param>
	/// <param name="disableOwner">Temporarily disable owner.</param>
	public bool ShowAndWait(Window owner, bool hideOwner = false, bool disableOwner = false) {
		Owner = owner;
		wnd ow = hideOwner || disableOwner ? owner.Hwnd() : default;
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
