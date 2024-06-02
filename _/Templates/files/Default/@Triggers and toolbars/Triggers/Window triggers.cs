using Au.Triggers;

partial class Program {
	[Triggers]
	void WindowTriggers() {
		Triggers.Options.ThreadNew();
		
		//Add window triggers here.
		//To add triggers can be used triggerSnippet or Ctrl+Shift+Q or menu TT > New trigger.
		//Click the Run button to apply changes after editing.
		//More info in Cookbook.
		
		
		
		if (!true) { //examples. To enable and test it, replace (!true) with (true) and run this script.
			Triggers.Window[TWEvent.ActiveNew, "* Notepad", "Notepad"] = o => print.it("window trigger example 1");
			Triggers.Window[TWEvent.ActiveNew, "Notepad", "#32770", contains: "Do you want to save *"] = o => {
				print.it("window trigger example 2");
				//keys.send("Alt+S"); //press button Save
			};
			
			//note: window triggers don't depend on Triggers.Of.
		}
	}
	
	
}
