## Version 0.18.0 (2023-)

### Editor
You can use Git and GitHub to backup and sync workspace files (scripts etc): menu File -> Git.

In autocompletion list you can press the [+] button or Ctrl+Space to include all types or extension methods, not only those from 'using' namespaces.

New cookbook recipes:
- Dialog - use triggers.

Small improvements.

Fixed bugs:
- Incorrectly formats code if it contains multiline block comments.

### Library
New classes:
- **FileTree**.
- **AppSingleInstance**.

New members:
- **script.restart**.
- **filesystem.setAttributes**.
- **wpfBuilder.FormatText**, **wpfBuilder.FormattedText**.

New parameters:
- .

Improved:
- .

Fixed bugs:
- **dialog**: default button isn't the first in the list as documented.
- **WaitLoop** and **wait.forCondition**: incorrectly combines *options* with **opt.wait**.
