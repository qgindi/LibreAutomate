using Au.Triggers;

partial class Program {
	[Triggers]
	void AutotextTriggers() {
		var tt = Triggers.Autotext;
		var tr = Triggers.Autotext.SimpleReplace;
		
		//Add autotext triggers here.
		//To add triggers can be used triggerSnippet or menu TT > New trigger.
		//To add trigger scopes (window, program) can be used Ctrl+Shift+Q.
		//Click the Run button to apply changes after editing.
		//More info in Cookbook.
		
		
		
		if (!true) { //examples. To enable and test it, replace (!true) with (true) and run this script.
			tt["losa"] = o => o.Replace("Los Angeles (autotext example)");
			tt["WIndows", TAFlags.MatchCase] = o => o.Replace("Windows (autotext example)");
			
			tt.DefaultPostfixType = TAPostfix.None; //set some options for triggers added afterwards
			tt["<e>"] = o => o.Replace("<e>[[|]]</e>");
			
			Triggers.Options.BeforeAction = o => { opt.key.TextHow = OKeyText.Paste; }; //set opt options for trigger actions added afterwards
			tt["#file", TAFlags.RemovePostfix] = o => {
				o.Replace("");
				var fd = new FileOpenSaveDialog { Title = "Autotext example" };
				if (!fd.ShowOpen(out string name)) return;
				keys.sendt(name);
				o.SendPostfix();
			};
			
			Triggers.Options.BeforeAction = null; tt.DefaultPostfixType = default; //reset some options
			
			//examples of simple text replacements
			
			tr["#su"] = "Sunday (autotext example)"; //the same as tt["#su"] = o => o.Replace("Sunday");
			tr["#mo"] = "Monday (autotext example)";
			
			//these triggers will work only in Notepad window
			
			Triggers.Of.Window("* Notepad", "Notepad");
			tr["#tu"] = "Tuesday (autotext example)";
			tr["#we"] = "Wednesday (autotext example)";
			Triggers.Of.AllWindows(); //reset
			
			//with confirmation
			
			tt.DefaultFlags |= TAFlags.Confirm; //add flag
			tr["#th"] = "Thursday (autotext example)";
			tr["#fr", postfixType: TAPostfix.None] = "Friday (autotext example)";
			tt.DefaultFlags &= ~TAFlags.Confirm; //remove flag
			
			//menu
			
			tt["m1"] = o => o.Menu([
				"https://www.example.com",
				"<tag>[[|]]</tag>",
				new("Label example", "TEXT1"),
				new("HTML example", "TEXT2", "<b>TEXT2</b>"),
				new(null, "TEXT3"),
				]);
		}
	}
	
	
}
