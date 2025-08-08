/// Other programs can launch scripts in two ways.
/// 
/// 1. Let the editor program launch the script. <help editor/Command line>More info<>. Command line examples:
///
/// 	<q>Au.Editor.exe Script5.cs<>
/// 	<q>Au.Editor.exe \Folder\Script5.cs<>
/// 	<q>Au.Editor.exe "Script name.cs" /argument1 "argument 2"<>
/// 	<q>Au.Editor.exe *Script5.cs<>
///
/// 	With <q>*<> the program can wait until the script ends and capture text written with <see cref="script.writeResult"/>.
///
/// 	To create a command line string to run current script, use menu <b>TT > Script launchers<>. Also there you'll find tools to create Windows Task Scheduler tasks (triggers: date/time, event log, startup, session connect/disconnect/lock/unlock, idle), shortcuts and <link https://www.libreautomate.com/forum/showthread.php?tid=7819>shell context menu items<>.
/// 
/// 2. Run the script without the editor. For it need to <help editor/Publish>create .exe program<> from the script. Then launch it like any other program. Example:
///
/// 	<q>C:\Test\Script5.exe<>
///
/// 	Note: the <b>ifRunning<> and <b>uac<> options then aren't applied. To ensure single running instance, use <see cref="script.single"/>.
