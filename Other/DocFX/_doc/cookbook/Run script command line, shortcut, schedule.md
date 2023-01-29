# Run script command line, shortcut, schedule
Other programs can launch scripts in two ways.

1. Let the editor program launch the script. <a href='/editor/Command line.html'>More info</a>. Command line examples:

	`Au.Editor.exe Script5.cs`
	`Au.Editor.exe \Folder\Script5.cs`
	`Au.Editor.exe "Script name.cs" /argument1 "argument 2"`
	`Au.Editor.exe *\Folder\Script5.cs`

	With * the program can wait until the script ends and capture text written with <a href='/api/Au.script.writeResult.html'>script.writeResult</a>.

	To easily create a command line string to run current script, use menu TT -> Script triggers. The tool also can create shortcuts and Windows Task Scheduler tasks.

2. Run the script without the editor. For it need to <a href='Create dotexe program.md'>create .exe program</a> from the script. Then launch it like any other program. Example:

	`C:\Test\Script5.exe`

	Note: the <span style='color:green'>ifRunning</span> and <span style='color:green'>uac</span> options then aren't applied. To ensure single running instance, use <a href='/api/Au.script.single.html'>script.single</a>.
