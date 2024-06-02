/// To create initial toolbar code easily, use menu TT > New toolbar.
/// To add buttons quickly: snippet tbToolbarButtonSnippet; drag-drop scripts/files/links; hotkey Ctrl+Shift+Q.
/// Click the Run button to apply changes after editing.
/// More info in Cookbook.

using Au.Triggers;

partial class Program {
	[Toolbars]
	void ToolbarTriggerExamples() {
		//Examples of toolbar triggers. To enable, replace (!true) with (true) and run this script.
		if (!true) {
			//common toolbars
			ToolbarExample_Startup1(); //show at startup
			Triggers.Mouse[TMEdge.TopInCenter50] = ToolbarExample_ScreenEdge; //or use a trigger
			
			//window toolbars
			Triggers.Window[TWEvent.ActiveOnce, "*Notepad", "Notepad"] = ToolbarExample_Notepad; //attach toolbar ToolbarExample_Notepad to Notepad windows
			//Triggers.Window[TWEvent.ActiveOnce, "*Name1", "ClassName1"] = Toolbar_X; //attach toolbar Toolbar_X to a window
			//Triggers.Window[TWEvent.ActiveOnce, "*Name2", "ClassName2"] = Toolbar_X; //attach the same toolbar to another window
			//Triggers.Window[TWEvent.ActiveOnce, "*Name3", "ClassName3"] = o => { Toolbar_One(o); Toolbar_Two(o); } //attach 2 toolbars to this window
		}
	}
	
	//Examples of toolbars. Each toolbar is in a separate function.
	
	void ToolbarExample_Startup1() {
		var t = new toolbar();
		
		//settings
		if (t.FirstTime) {
			//Here you can set some initial properties of the toolbar. Later users can change them: move/resize the toolbar, use the right-click menu.
		}
		t.BorderColor = System.Drawing.Color.BlueViolet;
		
		//buttons
		t["A"] = o => { };
		t["B|Tooltip"] = o => { };
		t["|Tooltip", image: "*WeatherIcons.RainMix #2F33D0"] = o => { };
		t.Menu("C", m => { //drop-down menu
			m["X"] = o => { };
			m["Y"] = o => { };
		});
		t.Group("Examples"); //horizontal separator, optionally with text
		t.DisplayText = false;
		t["Run program"] = o => run.it(folders.System + @"notepad.exe");
		t["Script"] = o => script.run("Script example1.cs");
		t["Copy-paste"] = o => {
			string s = clipboard.copy(); //note: to test it, at first select some text somewhere, else it will fail
			s = s.Upper();
			clipboard.paste(s);
		};
		//	t["Close", ""] = o => { t.Close(); };
		
		t.Show();
	}
	
	void ToolbarExample_ScreenEdge(MouseTriggerArgs ta) {
		var t = new toolbar();
		
		t[""] = o => { };
		t[""] = o => { };
		t.Separator();
		t[""] = o => { };
		t[""] = o => { };
		t.Group();
		t.Menu("", m => {
			m[""] = o => { };
			m[""] = o => { };
		});
		t[""] = o => { };
		t[""] = o => { };
		
		//auto-hide at the screen edge of the mouse trigger. Above is the auto-hide part. Below is the always-visible part.
		t = t.AutoHideScreenEdge(ta, 5, ^5, 2);
		t.BorderColor = System.Drawing.Color.Orange;
		
		t.Show();
		ta.DisableTriggerUntilClosed(t); //single instance
	}
	
	void ToolbarExample_Notepad(TriggerArgs ta = null) {
		var t = new toolbar();
		if (t.FirstTime) {
			//Here you can set some initial properties of the toolbar. Later users can change them: move/resize the toolbar, use the right-click menu.
		}
		
		t["A"] = o => { };
		t["B|Tooltip"] = o => { };
		t["|Tooltip", image: "*Modern.TreeLeaf #73BF00"] = o => { };
		t.Menu("C", m => { //drop-down menu
			m["X"] = o => { };
			m["Y"] = o => { };
		});
		t.Separator();
		t["Script"] = o => script.run("Script example1.cs");
		
		////auto-hide. Above is the auto-hide part. Below is the visible part.
		//t = t.AutoHide();
		//if(t.FirstTime) {
		
		//}
		
		t.Show(ta);
	}
	
}
