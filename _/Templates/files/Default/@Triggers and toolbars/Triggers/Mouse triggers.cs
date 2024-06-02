using Au.Triggers;

partial class Program {
	[Triggers]
	void MouseTriggers() {
		
		//Add mouse triggers here.
		//To add triggers can be used triggerSnippet or menu TT > New trigger.
		//To add trigger scopes (window, program) can be used Ctrl+Shift+Q.
		//Click the Run button to apply changes after editing.
		//More info in Cookbook.
		
		
		
		if (!true) { //examples. To enable and test it, replace (!true) with (true) and run this script.
			Triggers.Mouse[TMClick.Right, "Ctrl+Shift", TMFlags.ButtonModUp] = o => print.it("mouse trigger example", o.Trigger); //Ctrl+Shift+RightClick
			Triggers.Mouse[TMEdge.RightInCenter50] = o => { //the right edge of the primary screen, center 50%
				var m = new popupMenu("example");
				m["A (mouse trigger example)"] = o => { };
				m["B"] = o => { };
				m.Submenu("C", m => {
					m["D"] = o => { };
					m["E"] = o => { };
				});
				m.Show();
				
				//To create menus can be used snippets. Start typing "menu" and you will see snippets in the completion list.
			};
			Triggers.Mouse[TMMove.LeftRightInCenter50, screen: screen.ofMouse] = o => wnd.switchActiveWindow(); //move the mouse quickly to the left and back in center 50% of any screen
			Triggers.Mouse[TMMove.RightLeftInCenter50, screen: screen.ofMouse] = o => keys.send("Ctrl+Tab"); //to the right and back. Ctrl+Tab should switch the active document.
			
			Triggers.FuncOf.NextTrigger = o => keys.isScrollLock; //example of a custom scope (context, condition)
			Triggers.Mouse[TMWheel.Forward] = o => print.it($"mouse trigger example: {o.Trigger} while ScrollLock is on");
		}
	}
	
	
}
