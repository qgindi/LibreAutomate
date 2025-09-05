/// To run a script when the editor program starts (actually whenever this workspace loaded), add it in <b>Options > Workspace > Startup scripts<>. Use path like <.c>\Folder\Script.cs<> or name like <.c>Script123.cs<>. Can be several scripts in separate lines. To temporarily disable a script, prepend <.c>//<>, like <.c>//\Folder\Script.cs<>.

/// To run a script when Windows starts and don't start the editor too, need to <help editor/Publish>create .exe program<> from the script. Then launch it like any other program, for example create shortcut in the Startup folder or use Windows Task Scheduler.

/// Yet another way to run a script - a <help articles/Output tags>link<> in the output panel. In another script use code like this:

print.it("<>Run script <script>Test<>");
