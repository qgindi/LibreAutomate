# Run script at startup, output link
To run a script when the editor program starts (actually whenever this workspace loaded), add it in Options -> General -> Run script when this workspace loaded. Use path like `\Folder\Script.cs` or name like `Script123.cs`. Can be several scripts in separate lines. To temporarily disable a script, prepend `//`, like `//\Folder\Script.cs`.

To run a script when Windows starts and don't start the editor too, need to <a href='Create dotexe program.md'>create .exe program</a> from the script. Then launch it like any other program, for example create shortcut in the Startup folder or use Windows Task Scheduler.

Yet another way to run a script - a <a href='/articles/Output tags.html'>link</a> in the output panel. In another script use code like this:

```csharp
print.it("<>Run script <script>Test<>");
```

