## Version 0.11.0 (2022-11-?)

### Editor
Added meta comment "file", for copying files to .exe output or finding native dlls. Look in Properties.

New Cookbook recipes:
- Editor extension - modify UI.


### Library
New features:
- Class **ExtWpf**: several new functions and parameters.
- **ScriptEditor.Open**: new parameters *line* and *offset*.


### Bug fixes and improvements

Editor:
- Fixed: in Properties dialog cannot specify preBuild/postBuild arguments.
- Fixed: in editorExtension script does not work unmanaged dll resolving at run time.
- Fixed several other bugs.
- Improved: now in `/*/ r A.dll; /*/` don't need to specify dlls used by A.dll but not used in code.

Library:
- Fixed: **print** class functions may not work in non-console program started from a console program (cmd, .bat, etc).
