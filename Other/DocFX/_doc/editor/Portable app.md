---
uid: portable
---

# Portable mode
LibreAutomate can run as a [portable application](https://en.wikipedia.org/wiki/Portable_application). In portable mode its data (scripts, settings, caches, temporary files, etc) is inside subfolder `data` of its program folder. The presence of `data` folder tells LibreAutomate to use portable mode.

## Setup
In non-portable LibreAutomate use menu **Tools > Portable**. The tool installs portable LibreAutomate, for example in a USB drive.

The tool copies program files, .NET runtime, current workspace (scripts etc) and program user documents folder (settings etc).

## Run
Run `Au.Editor.exe` (it may be displayed as `LibreAutomate C#`).

The portable program can run on any Windows 7/8/10/11 64-bit computer. Don't need to install .NET runtime.

The portable program does not have the "always run as administrator" feature, but you can right-click and select **Run as administrator**. See [UAC](xref:uac).

The portable program does not write files outside of its folder. And does not write to the Registry.

LibreAutomate uses these special folders: [folders.ThisAppDocuments](), [folders.ThisAppDataLocal](), [folders.ThisAppTemp](). In portable mode these folders are in subfolder `data` of the portable program folder.

Scripts can write anywhere, but should use only the above special folders. Other useful functions: [ScriptEditor.IsPortable](), [folders.ThisAppDriveBS]().

The `data` subfolder can be a relative symbolic link to another folder. The tool creates a link if text in the data folder field is not `data`.

If non-portable LibreAutomate is installed on that computer:
- Portable and non-portable programs cannot run simultaneously.
- Portable and non-portable programs don't share data and settings.

## PortableApps.com
If you use the PortableApps.com platform, install portable LibreAutomate in its folder, for example `D:\PortableApps\LibreAutomate`. Then **LibreAutomate C#** will be in its menu (the first time may need to click **Apps > Refresh**). By default it is in category **Other**. You can right click it and move to an existing or new category. You can right click it and click **Run as administrator** or check **Start automatically**. The menu contains all exe files found in the LibreAutomate folder, but other files are not useful in the menu, and you can hide them.
