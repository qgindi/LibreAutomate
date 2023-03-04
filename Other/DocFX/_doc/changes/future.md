## Version 0.14.0 (2023-02-)

### Editor
Multiple 'Find' etc results tabs in the 'Found' panel.

Menu Edit -> Find references.

Menu Edit -> Find implementations.

Highlights references of the symbol at the text cursor.

Highlights matching braces and directives of the brace/directive at the text cursor.

New options:
- Options -> Code -> Formatting.
- Options -> Hotkeys -> Capture image.
- Options -> Font -> Text/symbol/brace highlight.

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
