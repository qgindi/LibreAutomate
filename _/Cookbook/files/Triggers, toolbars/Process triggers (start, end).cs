/// Use <see cref="process.triggers"/>.
/// See also recipe <+recipe>Other triggers<>.

/// Print all started and ended processes.

foreach (var v in process.triggers()) {
	print.it(v);
	if (v.Started)
		print.it("\t",
			process.getDescription(v.Id),
			process.getName(v.Id, fullPath: true),
			process.getCommandLine(v.Id));
}

/// Print started notepad processes in current user session.

foreach (var v in process.triggers(started: true, "notepad.exe", ofThisSession: true)) {
	print.it(v);
}

/// Wait for a process and continue the script. Then wait until the process ends.

int pid = 0;
foreach (var v in process.triggers(started: true, "notepad.exe", ofThisSession: true)) {
	print.it(v);
	pid = v.Id;
	break;
}
print.it("waiting until the process ends");
process.waitForExit(0, pid, out _);
print.it("ended");

/// If the loop code is slow, move it to another thread or script process, as usually. Else it may miss some trigger events.

foreach (var v in process.triggers(true, "**m winword.exe||*pad.exe")) {
	if (v.Name.Eqi("notepad.exe")) {
		Task.Run(() => {
			dialog.show("Thread pool", v.Name);
		});
	} else if (v.Name.Eqi("wordpad.exe")) {
		run.thread(() => {
			lock ("example5390487563428729") { //use this if don't want to run multiple threads simultaneously
				dialog.show("Thread " + Environment.CurrentManagedThreadId, v.Name);
			}
		});
	} else if (v.Name.Eqi("winword.exe")) {
		script.run("Script1002.cs", v.Name, v.Id.ToS());
		//Script1002:
		//string name = args[0];
		//int id = args[1].ToInt();
		//dialog.show("Script processs", $"name={name}, id={id}");
	}
}

/// Wait for a "notepad.exe" process if not already running.

wait.forCondition(0, () => process.exists("notepad.exe"));
