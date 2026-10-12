
## Version info

The program version format is `major.minor.build`, like 1.20.5.
- Minor: changed when added some significant features.
- Build: changed when fixed bugs between minor versions. Also may have some new features.

Script `LA version.cs` updates version info everywhere. Script `Release new LA version.cs` displays a link to run it.

All significant changes of that version must be documented in `DocFX/_doc/changes/vMajor.Minor.md`. Do it whenever making changes.

## How to release a new version of LibreAutomate

Run script `Release new LA version.cs`.
It shows a window with all info and links to run scripts etc.

### Later

Periodically check false positives on virustotal. Script `Virustotal false positives.cs` does it every day.
Also sometimes test whether Chromium still allows to download. Because v1.15 after some time had these problems: many false positives, and Chromium blocks the download.
