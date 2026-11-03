# Version 1.17.0 (2026-)

## Editor

New tools:
- .

New cookbook recipes:
- .

Improved:
- .

Fixed bugs:

## Library

New classes:
- `Path2`. Replaces `pathname`.
- `PathFilter`. Path glob matching etc.

New members:
- `filesystem.list`. Replaces the `enumX` methods. Supports glob patterns, more options, faster, and can return familiar .NET types.
- New overloads of `filesystem.copy` and some other methods have the same improvements as in `filesystem.list`.

New parameters:
- `EnumUI` callback function for filtering and setting text/tooltip.

Improved:
- Can use `using` with `wpfBuilder` panels: `using (b.R.StartGrid()) { /*auto-indented code*/ } //don't need b.End()`.

Fixed bugs:

### Breaking changes
