/// The user can end a running script task in several ways.

/// 1. Click the 'End task' toolbar button or menu command.

/// 2. Right-click or middle-click the <+recipe>tray icon<> of the script.

/// 3. If the script calls <see cref="script.setup"/> like this at the start, press the exit key. If UAC blocks it, try with Alt, Ctrl or Win.

script.setup(trayIcon: true, exitKey: KKey.MediaStop);

/// 4. If the script calls <see cref="script.setup"/> like this at the start, press the Sleep button on the keyboard.

script.setup(trayIcon: true, sleepExit: true);

/// 5. Press Win+L or Ctrl+Alt+Delete. If the script calls <see cref="script.setup"/> like this at the start, it will end immediately. Else it will end when calling a keyboard or mouse input function or <see cref="wnd.Activate"/>, because these functions then fail and throw exception; some other functions too.

script.setup(trayIcon: true, lockExit: true);

/// 6. Insert <see cref="script.pause"/> in loops etc, in places safe to pause or end the script. To end the script, press the pause key (default ScrollLock), and then use any of the above ways to end the paused script.

script.pause();

/// Changing the pause key.

script.setup(trayIcon: true, sleepExit: true, pauseKey: KKey.MediaPlayPause);

/// 7. A script can call <see cref="script.end"/> to end another script. For example you can add a trigger with this action code.

script.end("Script name.cs");

/// 8. A script can set a trigger to end itself.

using Au.Triggers;

run.thread(() => {
	ActionTriggers Triggers = new();
	Triggers.Hotkey["?+F1"] = o => { script.end(); };
	Triggers.Run();
});

dialog.show("Main script code");

/// See also: <+recipe return>return, exit<>, <+recipe exceptions>throw exception<>.
