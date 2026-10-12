## .NET major version

Install the .NET SDK for that .NET version.

VS must support that .NET version.

Run script `Create .NET ref and doc databases.cs`. Select the new .NET version in the dialog. Later (after building LA for the new .NET) rename the generated db files.

Update .NET version in all C# projects (`csproj`). Except those using .NET 4.8 (`Au.Net4`).

May need to update .NET SDK version in `global.json` (in solution folder).
	Once used it to force VS to use .NET 8 SDK.
	Because: if installed .NET 9 Preview SDK, VS used it to build projects that use .NET 8. Then app crashes when starting.
	Note: some scripts build just the `Au` project. To use correct SDK, I created a symlink to the solution's `global.json` in the project folder. Adding as a link in VS (in `csproj`) does not work.

Update `MetaComments.Defines`.

Find-replace .NET major version, like `NET 10`, `NET10`, `net10` and `10` everywhere.

Build solution.

Rename files `ref-new.db` and `doc-new.db` to `ref.db` and `doc.db`.

Run editor.

Run script `Minimal .NET SDK`. Upload.

Test menu **Tools > NuGet**. Test package `Microsoft.PowerShell.SDK` (because it's the most complex I know). Test how it works in `miniProgram` and `exeProgram` scripts.

Test menu **Run > Publish**.

Maybe more.

## Scintilla

Currently using version 5.5.7.  
Reviewed changed up to 5.6.7. Nothing interesting.  
The development is slow. Don't need to update frequently.

I don't remember how exactly I update. Probably:
- Copy this solution. In original solution replace the Scintilla source files; maybe except the project file, not sure, or maybe compare it at first. Open the copy (it contains the old modified Scintilla project) in new VS instance, so I can see both versions side by side. Or/and maybe I use Git diff, don't remember.
- Also open both projects in File Explorer side by side. See which files are used by both projects. From the new Scintilla project delete unused files. Try to build. May need to restore some files if deleted too many.
- All modifications are marked with comment `//au` (or `//Au`, case -insensitive). Search for it in the old modified project. Apply the same modifications in the new project. Many modifications, much work.

## PCRE

Currently using version 10.46.  
Reviewed changed up to 10.48. In 10.47 added one advanced syntax.  
The development is slow. Don't need to update frequently.

I don't remember how exactly I update. Probably like Scintilla. Much less modifications though.

## Roslyn (external)

Documented in `Roslyn.md` here.

## netcoredbg (external)

Documented in `netcoredbg.md` here.
