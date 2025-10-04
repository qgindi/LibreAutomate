## Version 1.15.0 (2025-)

### Editor

Documentation prepared for AI-based search.

New features in the **Icons** tool:
- Use AI to find icons by name or/and image.
- Can display bigger icons. Look in the **Options** expander.

Improved UI element capturing (tool **Find UI element**) in some apps and modes.

Tools like **Find UI element** now run in separate process.

Menu **Edit > Assist > Insert new line before ) ] ;**. Temporarily disables statement auto-completion on `Enter`. And toolbar button.

New code editor feature: continue a vertical method chain by typing `.` on the next line after the `;`.

Added commands in menu **Edit > Find**:
- **Find next** (`F3` or click the **Find** button).
- **Find previous** (`Shift+F3` or right-click the **Find** button).
- **Next found** (`F4`). Selects next item in the **Found** panel.
- **Previous found** (`Shift+F4`).

Quickly hide toolbar buttons using the context menu (in LA window).

Several small improvements.

Fixed bugs:
- .

### Library
Updated PCRE regex library to v10.46.

New classes:
- .

New members:
- `wpfBuilder.Add` overloads without parameter `flags`. The old overloads now are hidden. Flags replaced with: function `Child`; parameter *raw* of one `Add` overload.
- `dialog`: fluent API methods for setting dialog properties. The old functions now are hidden.
- And more.

New parameters:
- .

New `dialog` features:
- easier to add links.
- supports XAML icons.

Improved:
- .

Fixed bugs:
- `script.restart` does not work.
- `JSettings` can deadlock.
- `WTaskbarButton` exception if used in multiple threads.

### Breaking changes

Changed namespaces in `Au.Editor` project. Edit your `editorExtension` scripts that use its internal classes (unlikely).