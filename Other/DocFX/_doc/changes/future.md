## Version 0.17.0 (2023-)

### Editor
New tools:
- .

New cookbook recipes:
- .

Improved:
- Source code search (the "Go to definition" command when the symbol is not in your code).
- Some fonts.
- And more.

Some new options in "Font, colors".

### Library
New classes:
- **ComUtil**.
- **WBLink**. Can be used with **wpfBuilder.Text**.

New members:
- **elm.uiaCN**. Also **elm** and **elmFinder** functions that support `uiaid` now also support `uiacn`.
- **HttpResponseMessage** extension methods: **Download**, **DownloadAsync**.
- **wpfBuilder**: several new overloads.

New parameters:
- .

Improved:
- **internet.http** adds default header `"User-Agent: Au"`.
- **AutotextTriggerArgs.Menu**: null adds separator.
- **wpfBuilder.Padding** and **.Brush** support more element types.

### Bug fixes

Editor:
- Fixed: May crash when disabling triggers.
- Fixed: Cannot convert some COM type libraries.
- Fixed: After opening another workspace stops working the Files panel's context menu.
- Fixed: Exception when formatting code that starts with space.

Library:
- Fixed: **print.it** afraids some COM types.
- Fixed the _r bug in documentation.
