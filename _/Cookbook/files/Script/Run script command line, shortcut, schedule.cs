/// Other programs can launch scripts in two ways.
/// 
/// 1. Let the editor program launch the script. <help editor/Command line>More info<>. Command line examples:
///
/// 	<.c>Au.Editor.exe Script5.cs<>
/// 	<.c>Au.Editor.exe \Folder\Script5.cs<>
/// 	<.c>Au.Editor.exe "Script name.cs" /argument1 "argument 2"<>
/// 	<.c>Au.Editor.exe *\Folder\Script5.cs<>
///
/// 	With * the program can wait until the script ends and capture text written with <see cref="script.writeResult"/>.
///
/// 	To easily create a command line string to run current script, use menu <b>TT > Script triggers<>. The tool also can create shortcuts and Windows Task Scheduler tasks.
/// 
/// 2. Run the script without the editor. For it need to <help editor/Publish>create .exe program<> from the script. Then launch it like any other program. Example:
///
/// 	<.c>C:\Test\Script5.exe<>
///
/// 	Note: the <b>ifRunning<> and <b>uac<> options then aren't applied. To ensure single running instance, use <see cref="script.single"/>.
