## Version 1..0 (2024-)

### Editor
In **New trigger** tool added trigger property pages etc.

Menu **TT > Find triggers**. Finds triggers and scheduled tasks of current script.

Menu **TT > Schedule**. Tool to edit a scheduled task for current script.

Several bug fixes and improvements.

### Library
New classes:
- .

New members:
- **wpfBuilder.StartPanel** (adds panel of any type).

New parameters:
- **wpfBuilder.LabeledBy**: *bindVisibility*.
- **wpfBuilder.Validation**: *linkClick* callback.

Improved:
- **ExplorerFolder** supports multiple tabs. Added more functions and parameters.
- **EnumUI** supports more panel types.
- Improvements in **wpfBuilder.FormatText** and similar functions.

Fixed bugs:
- Some functions may throw **InputDesktopException**. Including **WndUtil.EnableActivate**, **dialog.show**, **run.it**, and some **elm** functions.

### Breaking changes
