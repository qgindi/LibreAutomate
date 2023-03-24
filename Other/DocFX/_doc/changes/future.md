## Version 0.14.0 (2023-02-)

### Editor
Multiple 'Find' etc results pages in the 'Found' panel.

Menu Edit -> Find references.

Menu Edit -> Find implementations.

Menu Edit -> Go to base.

Highlights references of the symbol at the text cursor.

Highlights matching braces and directives of the brace/directive at the text cursor.

Improved replacing text in multiple files. Multi-file undo/redo.

New options:
- Options -> Font, colors -> highlight, selection.
- Options -> Code editor -> Formatting.
- Options -> Hotkeys -> Capture image.

Now `/*/ pr /*/` can be used in any file of a project, not just in the main file.

### Library
New members:
- Event **computer.suspendResumeEvent**.
- Flag **IFFlags.Parallel** for **uiimage.find**.


### Bug fixes and improvements

Editor:
- Fixed: cannot use NuGet packages that depend on .NET 7 assemblies.
- Fixed: error message box on exit.
- Fixed several memory leaks and other bugs.
- Improvements in several editor features.

Library:
- Fixed "TextForFind" bug in documentation.


### Breaking changes
When converting **int**/**uint** to **ColorInt**, now by default sets alpha 255 only if it is 0 in the specified value. It allows to specify alpha in implicit conversion.
