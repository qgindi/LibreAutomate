## Version 1.4.0 (2024-07-15)

### Editor

The **Files** panel reflects workspace changes made not in the editor (added/deleted files). See also **Options > Workspace > Hide/ignore files and folders**.

Improvements in the **Portable LibreAutomate setup** tool.

**editorExtension** scripts can use `await`. Previously would hang.

Removed feature "surround selected text when typed `(` etc".

Fixed some bugs.

### Library

Fixed bugs:
- **print.it** and **print.list** skip first empty-string items of **IEnumerable**.

### Breaking changes

The **Files** panel now is synchronized with the filesystem. Because of this, some unwanted files/folders may be added. The program prints the added files; please review them.