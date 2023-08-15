/// Other programs can launch scripts in two ways.
/// 
/// 1. Let the editor program launch the script. <help editor/Command line>More info<>. Command line examples:
///
/// 	<mono>Au.Editor.exe Script5.cs<>
/// 	<mono>Au.Editor.exe \Folder\Script5.cs<>
/// 	<mono>Au.Editor.exe "Script name.cs" /argument1 "argument 2"<>
/// 	<mono>Au.Editor.exe *\Folder\Script5.cs<>
///
/// 	With * the program can wait until the script ends and capture text written with <see cref="script.writeResult"/>.
///
/// 	To easily create a command line string to run current script, use menu TT -> Script triggers. The tool also can create shortcuts and Windows Task Scheduler tasks.
/// 
/// 2. Run the script without the editor. For it need to <+recipe>create .exe program<> from the script. Then launch it like any other program. Example:
///
/// 	<mono>C:\Test\Script5.exe<>
///
/// 	Note: the <c green>ifRunning<> and <c green>uac<> options then aren't applied. To ensure single running instance, use <see cref="script.single"/>.
