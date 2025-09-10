## Version 1.15.0 (2025-)

### Editor

Added commands in menu **Edit > Find**:
- **Find next** (`F3` or click the **Find** button).
- **Find previous** (`Shift+F3` or right-click the **Find** button).
- **Next found** (`F4`). Selects next item in the **Found** panel.
- **Previous found** (`Shift+F4`).

Documentation improvements. New articles.

Added menu command **Edit > View/mode > Insert new line before ) ] ;**. Temporarily disables the statement auto-completion on `Enter` feature. And toolbar button.

New tools:
- .

New cookbook recipes:
- .

Improved:
- .

Fixed bugs:
- .

### Library
New classes:
- .

New members:
- `wpfBuilder.Add` overloads without parameter `flags`. The old overloads now are hidden. Flags replaced with: function `Child`; parameter *raw* of one `Add` overload.
- `dialog`: fluent methods for setting dialog properties. The old functions now are hidden.
- And more.

New parameters:
- 

New features:
- `dialog`: easier to add links.
- `dialog`: supports XAML icons.

Improved:
- .

Fixed bugs:
- `script.restart` does not work.
- `JSettings` can deadlock.
- `WTaskbarButton` exception if used in multiple threads.

### Breaking changes
