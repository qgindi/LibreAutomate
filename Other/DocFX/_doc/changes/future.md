## Version 0.17.0 (2023-)

### Editor
New tools:
- .

New cookbook recipes:
- .

Improved:
- Does not use NTFS links for folders imported as links.
- Does not use locked files (state.db) in workspace folder.
- Source code search (the "Go to definition" command when the symbol is not in your code).
- Some fonts.
- And more.

Options -> Font, colors: output font, italic.

### Library
New classes:
- **ComUtil**.
- **WBLink**. Can be used with **wpfBuilder.Text**.

New members:
- **elm.uiaCN**. Also **elm** and **elmFinder** functions that support `uiaid` now also support `uiacn`.
- **HttpResponseMessage** extension methods: **Download**, **DownloadAsync**.
- **wpfBuilder**: **Wrap**, and several new overloads of other functions.
- **folders.sourceCodeMain**.
- **script.sourcePath**.
- **JSettings** properties: **NoAutoSave**, **NoAutoSaveTimer**.

New parameters:
- **filesystem.more.isSameFile**: *useSymlink**.

Improved:
- **internet.http** adds default header `"User-Agent: Au"`.
- **AutotextTriggerArgs.Menu**: null adds separator.
- **wpfBuilder.Padding** and **.Brush** support more element types.
- **filesystem.more.createSymbolicLink** with *deleteOld* true now does not delete the old link if fails to create new.
- **filesystem.more.createSymbolicLink** can create symbolic links without admin rights if in Windows Settings enabled developer mode.
- **Hash.MD5Context.Add**: no exception when data is empty.

### Bug fixes

Editor:
- Fixed: May crash when disabling triggers.
- Fixed: Cannot convert some COM type libraries.
- Fixed: After opening another workspace stops working the Files panel's context menu.
- Fixed: Exception when formatting code that starts with space.

Library:
- Fixed: **print.it** afraids some COM types.
- Fixed the _r bug in documentation.
- Fixed: when **ExtString.ToInt** fails, *numberEndIndex* isn't 0 if used flag **IsHexWithout0x**.
