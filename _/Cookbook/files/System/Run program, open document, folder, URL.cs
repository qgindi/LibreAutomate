/// The most universal function is <see cref="run.it"/>. To quickly insert code can be used riRunItSnippet. Or drag and drom from File Explorer. Or hotkey Ctrl+Shift+Q.

run.it(@"C:\folder\program.exe"); //run program
run.it(@"C:\folder\file.txt"); //open file in default program
run.it("notepad.exe", @"c:\file.txt"); //open file in specified program
run.it(@"C:\folder"); //open folder
run.it(@"C:\folder\shortcut.lnk"); //execute shortcut
run.it(folders.Documents); //special folder
run.it(folders.System + @"notepad.exe"); //file in a special folder
run.it(@"%folders.System%\notepad.exe"); //the same
run.it(@"%TMP%\file.txt"); //can start with an environment variable
run.it("notepad.exe"); //will search in common places and the registry

run.it(@".\file.exe"); //file in folders.ThisApp
run.it(folders.ThisApp + @"file.exe"); //the same
run.it(folders.ThisApp + @"folder\file.exe"); //relative path
run.it(folders.ThisApp + @"..\folder\file.exe"); //relative path (.. means parent folder)

run.it("https://www.example.com/"); //URL (open webpage in default web browser)
run.it("file:///C:/folder/file.txt"); //file path like URL
run.it("mailto:a@b.c"); //create new email message in default email app

run.it(folders.shell.ControlPanel); //virtual folder
run.it(/* Sound */ folders.shell.ControlPanel + "1e0071800000000000000000000082fcddf2128fdd4cb7dcd4fe1425aa4d"); //virtual folder item
run.it(/* Control Panel */ ":: 14001f706806ee260aa0d7449371beb064c98683"); //ITEMIDLIST
run.it(/* Sound */ "shell:::{F2DDFC82-8F12-4CDD-B7DC-D4FE1425AA4D}"); //shell object's parsing name

run.it(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"); //Microsoft Store app

/// Pass command line arguments.

run.it("program.exe", @"/c ""text"" /d 100");

/// Set initial working directory.

run.it("notepad.exe", dirEtc: @"C:\Windows");

/// Set initial window state. Most programs ignore it.

run.it("notepad.exe", dirEtc: new() { WindowState = ProcessWindowStyle.Maximized });

/// Use a verb (context menu command).

run.it(@"C:\Test\test.txt", dirEtc: new() { Verb = "print" });

/// Run as administrator.

run.it("notepad.exe", flags: RFlags.Admin);

/// Get the process id (if started new process).

int pid = run.it("notepad.exe").ProcessId;

/// Wait until the process exits and get the exit code.

int ec = run.it("notepad.exe", flags: RFlags.WaitForExit).ProcessExitCode;

/// Use <see cref="run.itSafe"/> if want to ignore exceptions such as "file not found".

run.itSafe(@"C:\folder\program.exe");

/// Run Notepad and wait for an active Notepad window.

run.it("notepad.exe");
wnd w1 = wnd.wait(10, true, "*- Notepad", "Notepad");
print.it(w1);

/// Run Notepad or activate a Notepad window.

wnd w2 = wnd.findOrRun("*- Notepad", run: () => run.it("notepad.exe"));
print.it(w2);

/// Run File Explorer and wait for new folder window. Ignores folder windows that already existed.

var w3 = wnd.runAndFind(
	() => run.it(@"explorer.exe"),
	10, cn: "CabinetWClass");
print.it(w3);

/// Run if the process does not exist.

if (!process.exists("notepad.exe")) run.it(@"notepad.exe");

/// Select a file in File Explorer (folder window). Opens the folder window if need.

run.selectInExplorer(@"C:\folder\file.txt");