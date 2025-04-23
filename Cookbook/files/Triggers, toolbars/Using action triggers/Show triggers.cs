/// Function <help>Au.Triggers.ActionTriggers.ShowTriggersListWindow<> shows a temporary window with a list of currently active triggers for the active window (hotkey, autotext and mouse edge/move triggers) or the mouse window (mouse click/wheel triggers).
/// A good way to call the function is a hotkey or mouse trigger. For example, you can add or/and edit this code in file <open>Hotkey triggers<>. Then click the <b>Run<> button to restart the triggers script.

		hk["Ctrl+Shift+T"] = o => Triggers.ShowTriggersListWindow();
		hk.Last.EnabledAlways = true;
