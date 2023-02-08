/// Various .NET and automation library classes and functions can be used to implement triggers and events of all kinds.
/// For example <see cref="SystemEvents"/>, <see cref="FileSystemWatcher"/>, <see cref="WindowsHook"/>, <see cref="WinEventHook"/>, <see cref="process.triggers"/>.
/// A good place for triggers that should be active all the time is file "Other triggers". To open it, use menu TT -> Other triggers.
/// This is an "Other triggers" file example.

partial class Program {
	[Triggers]
	void OtherTriggers() {
		
		if (!true) { //examples. To enable and test it, replace (!true) with (true) and run this script.
			computer.suspendResumeEvent += _computer_suspendResumeEvent;
			SystemEvents.PowerModeChanged += _SystemEvents_PowerModeChanged;
			SystemEvents.DisplaySettingsChanged += _SystemEvents_DisplaySettingsChanged;
			SystemEvents.UserPreferenceChanged += _SystemEvents_UserPreferenceChanged;
			SystemEvents.SessionEnding += _SystemEvents_SessionEnding;
			SystemEvents.SessionEnded += _SystemEvents_SessionEnded;
			SystemEvents.SessionSwitch += _SystemEvents_SessionSwitch;
			//note: all above event handler functions run in other thread.
			
			run.thread(_ProcessTriggers);
			//run.thread(_FileTriggers);
		}
	}
	
	//When computer suspending (sleep, hibernate) or resumed.
	void _computer_suspendResumeEvent(PowerModes pm) {
		print.it("_computer_suspendResumeEvent", pm, Environment.CurrentManagedThreadId);
	}
	
	//When power mode changed (battery/AC etc).
	void _SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e) {
		print.it("PowerModeChanged", e.Mode, computer.isOnBattery, Environment.CurrentManagedThreadId);
	}
	
	//When changed screen (display monitor) count, configuration, DPI (text size, scaling), resolution or other properties.
	void _SystemEvents_DisplaySettingsChanged(object sender, EventArgs e) {
		print.it("DisplaySettingsChanged");
	}
	
	//When changed some system setting. See API SystemParametersInfo.
	void _SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) {
		print.it("UserPreferenceChanged", e.Category);
	}
	
	//When trying to log off or shutdown.
	void _SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e) {
		print.it("SessionEnding", e.Reason);
		//e.Cancel = true;
	}
	
	//When started to log off or shutdown actually.
	void _SystemEvents_SessionEnded(object sender, SessionEndedEventArgs e) {
		print.it("SessionEnded", e.Reason);
	}
	
	//When switching user sessions, locking/unlocking the computer, etc.
	void _SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e) {
		print.it("SessionSwitch", e.Reason);
	}
	
	//Process triggers. More info in Cookbook.
	static void _ProcessTriggers() {
		foreach (var v in process.triggers()) {
			print.it(v);
			if (v.Started)
				print.it("\t",
					process.getDescription(v.Id),
					process.getName(v.Id, fullPath: true),
					process.getCommandLine(v.Id));
		}
	}
	
	//File/directory triggers. More info in Cookbook.
	static void _FileTriggers() {
		using var fw = new FileSystemWatcher(@"C:\Test");
		//you can specify various filters etc
		//fw.Filters.Add("*.txt");
		//fw.Filters.Add("*.xml");
		//fw.IncludeSubdirectories = true;
		
		//set one or more events
		fw.Created += (o, e) => { print.it(e.ChangeType, e.Name, e.FullPath); };
		fw.Deleted += (o, e) => { print.it(e.ChangeType, e.Name, e.FullPath); };
		fw.Renamed += (o, e) => { print.it(e.ChangeType, e.Name, e.OldName); };
		fw.Changed += (o, e) => { print.it(e.ChangeType, e.Name, e.FullPath); };
		//fw.Error += (o, e) => { print.it(e.GetException()); };
		fw.EnableRaisingEvents = true;
		
		//using var fw2 = new FileSystemWatcher(@"C:\Another directory");
		//fw2.Created += ...
		// ...
		//fw2.EnableRaisingEvents = true;
		
		//then wait or execute any code. Event handlers run in another thread.
		wait.ms(-1); //wait forever
		//keys.waitForHotkey(0, "Esc"); //another "wait" example
		//dialog.show("File change events", buttons: "Stop", x: ^0); //another "wait" example
	}
}
