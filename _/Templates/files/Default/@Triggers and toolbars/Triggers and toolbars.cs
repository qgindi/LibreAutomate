/*/ ifRunning restart; noWarnings 162; /*/
using Au.Triggers;
using System.Reflection;

partial class Program {
	static void Main(string[] a) => new Program(a);
	Program(string[] args) {
		script.setup();
		
		//Here you can add code that runs at startup. Set variables, etc.
		//Add triggers and toolbars in other files of this folder. More info in Cookbook.
		
		
		
		RunTriggersAndToolbars();
	}
}

//. RunTriggersAndToolbars

//This code manages your triggers and toolbars. Don't delete, it is not an example code.

partial class Program {
	
	/// <summary>
	/// Use this to add triggers, set trigger options, etc. Example:<br/> <c>Triggers.Hotkey["Ctrl+E"] = o => print.it(o.Trigger);</c>.
	/// </summary>
	ActionTriggers Triggers { get; } = new();
	
	/// <summary>
	/// Calls your functions that have attribute [Triggers] or [Toolbars]. Finally calls <c>Triggers.Run();</c>.
	/// Let [Triggers] functions add triggers. Let [Toolbars] functions create some toolbars and/or add triggers for other toolbars.
	/// </summary>
	/// <remarks>
	/// Before calling each function, resets all trigger options (Options, Of, FuncOf, etc), to avoid inheriting options changed in other functions.
	/// </remarks>
	void RunTriggersAndToolbars() {
		void _TriggerOptions(bool toolbars) {
			//Here you can set options that are common to all your triggers. Examples:
			//		if (!toolbars) {
			//			Triggers.Options.Thread(0, ifRunningWaitMS: 500);
			//			Triggers.Options.BeforeAction = o => {
			//				opt.key.SleepFinally = 50;
			//			};
			//		}
		}
		
		foreach (var mi in typeof(Program).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance)) {
			bool tb = mi.IsDefined(typeof(ToolbarsAttribute), false);
			if (!(tb || mi.IsDefined(typeof(TriggersAttribute), false))) continue;
			
			Triggers.ResetOptions();
			_TriggerOptions(toolbars: tb);
			if (tb) Triggers.Options.ThreadThis();
			
			try { mi.Invoke(this, null); }
			catch (TargetInvocationException ex) { print.it(ex.InnerException); return; }
		}
		
		//run triggers in other thread, to avoid blocking hooks when getting icons etc
		Triggers.RunThread();
	}
	
	[AttributeUsage(AttributeTargets.Method)] class TriggersAttribute : Attribute { }
	[AttributeUsage(AttributeTargets.Method)] class ToolbarsAttribute : Attribute { }
	
}

//..
