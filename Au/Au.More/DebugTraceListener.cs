namespace Au.More;

/// <summary>
/// Replaces default trace listener with listener that overrides its <see cref="DefaultTraceListener.Fail(string?, string?)"/> method. On failed assertion (<see cref="Debug.Assert"/>, <see cref="Trace.Assert"/>, <see cref="Debug.Fail"/>, <see cref="Trace.Fail"/>) it shows message box with buttons <b>Exit</b> <b>Debug</b> <b>Ignore</b>, unless debugger is attached or !<b>AssertUiEnabled</b>.
/// </summary>
public class DebugTraceListener : DefaultTraceListener {
	/// <summary>
	/// Replaces default trace listener.
	/// </summary>
	/// <param name="usePrint">Also set <see cref="print.redirectDebugOutput"/> = <c>true</c>.</param>
	//[Conditional("DEBUG"), Conditional("TRACE")] //no, in most cases this is called by this library, not directly by the app
	public static void Setup(bool usePrint) {
		if (!s_setup) {
			s_setup = true;
			Trace.Listeners.Remove("Default"); //remove DefaultTraceListener. It calls Environment.FailFast which shows message box "Unknown hard error".
			Trace.Listeners.Add(new DebugTraceListener());
		}
		print.redirectDebugOutput = usePrint;
	}
	static bool s_setup;

	///
	public override void Fail(string message, string detailMessage) {
		var s = message;
		if (s.NE()) s = detailMessage; else if (!detailMessage.NE()) s = message + "\r\n" + detailMessage;
		if (!s.NE()) s += "\r\n";

		string st = new StackTrace(2, true).ToString(), st1 = null;
		if (st.RxMatch(@"(?m)^\s+at (?!System\.Diagnostics\.)", 0, out RXGroup g)) {
			st = st[g.Start..];
			st1 = st.Lines(true)[0];
		}

		var s2 = "---- Debug assertion failed ----\r\n" + s + st;
		Trace.WriteLine(s2);
		if (!(print.redirectDebugOutput && print.qm2.use)) print.qm2.write(s2);

		if (Debugger.IsAttached) {
			Debugger.Break();
		} else {
			if (!AssertUiEnabled) return; //like default listener

			s = $"{s}{st1}\n\nProcess id: {process.thisProcessId}";
			int r = dialog.showWarning("Debug assertion failed", s, "1 Exit|2 Ignore|3 script.debug|4 Debugger.Launch", expandedText: st);
			if (r == 1) Api.ExitProcess(-1);
			if (r == 2) return;
			if (r == 4) Debugger.Launch();
			else {
				script.debug();
				Debugger.Break();
			}
		}
	}
}
