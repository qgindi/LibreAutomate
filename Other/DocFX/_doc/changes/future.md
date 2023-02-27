## Version 0.14.0 (2023-02-)

### Editor
Multiple 'Find' etc results tabs in the 'Found' panel.

Menu Edit -> Find references.

New options:
- Options -> Code -> Formatting.
- Options -> Hotkeys -> Capture image.
- Options -> Font -> Find highlight.

### Library
New members:
- Event **computer.suspendResumeEvent**.
- Flag **IFFlags.Parallel** for **uiimage.find**.


### Bug fixes and improvements

Editor:
- Fixed: cannot use NuGet packages that depend on .NET 7 assemblies.
- Fixed several memory leaks and other bugs.

Library:
- Fixed "TextForFind" bug in documentation.


### Breaking changes
