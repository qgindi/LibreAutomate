## Version 1.15.0 (2025-)

### Editor

Added commands in menu **Edit > Find**:
- **Find next** (`F3` or click the **Find** button).
- **Find previous** (`Shift+F3` or right-click the **Find** button).
- **Next found** (`F4`). Selects next item in the **Found** panel.
- **Previous found** (`Shift+F4`).

Documentation improvements. New articles.

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
- `wpfBuilder.Validation` overload.
- `wpfBuilder.Add` overloads without parameter `flags`. The old overloads now are hidden. Instead of the "child of last" flag call function `Child` before `Add`. Instead of the "don't change properties" flag added parameter *raw* to one overload.
- `wpfBuilder.Child`.

New parameters:
- `JSettings.Load`: JSON serializer options.

Improved:
- .

Fixed bugs:
- `script.restart` does not work.
- `JSettings` can deadlock.
- `WTaskbarButton` exception if used in multiple threads.

### Breaking changes
