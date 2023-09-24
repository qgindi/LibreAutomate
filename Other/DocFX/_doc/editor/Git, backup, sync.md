---
uid: git
---

# Git, backup, sync

## Git and GitHub

Git software can be used to make multiple backups of workspace files (scripts etc) and store them in local and cloud repositories.

GitHub is the cloud service where most programmers have their public and private Git repositories.

To backup workspace files can be used LibreAutomate or/and any Git software.

## How to start

You need a [GitHub](https://github.com/) account, and a private repository there. All it's free.

1. In your GitHub account [create new repository](https://github.com/new). It must be **private** and **empty** (no readme etc). Copy its URL.
2. In LibreAutomate click menu File -> Git -> Git setup. Paste the URL.
3. If Git not installed, click the Install button. It installs MinGit (minimal Git) in the LibreAutomate folder (subfolder "Git"). Or you can download and install Git for Windows (not in LA folder); let it set PATH variable.
4. Click OK. What it does:
    - Connects to your GitHub account to verify the repository.
    - Initializes local Git repository (hidden subfolder ".git" in the workspace folder).
    - If the GitHub repository is not empty, can clone it instead.
    - If the subfolder already exists, just updates the URL.
    - Creates files ".gitignore" and ".gitattributes" if missing.
    - Imports default Git script if missing.
5. Now you can see more items added to the Git menu (menu File -> Git). To access the Git menu faster, you can unhide the Git button in the File toolbar.

## How to use

Use menu File -> Git. Menu commands:

- Git status - print Git status info for this repository. It helps you to decide whether you want to commit, push, pull or do nothing.
- Commit - make local backup. Adds it to the local Git repository (hidden subfolder ".git" in the workspace folder). Does nothing if there are no changes since the last commit.
- Push to GitHub - commit if need, and upload new local commits to the GitHub repository.
- Pull from GitHub - if the GitHub repository contains new commits that are not in the local repository, downloads them to the local repository and updates workspace files (scripts etc).
- GitHub Desktop - run GitHub Desktop (if installed).
- Cmd - run cmd.exe. Then you can execute any Git commands.
- Workspace folder - open the workspace folder.
- Reload workspace - reload current workspace. Need it after an external program (GitHub Desktop, Git, etc) modified workspace files.
- GitHub sign out - delete GitHub credentials saved on this computer. You may want it when using portable LibreAutomate.
- Maintenance - tools to compact the Git repository.
- Git setup - Git setup dialog.

When you need something more, use a [Git GUI software](https://git-scm.com/downloads/guis) or Git command line. Try [GitHub Desktop](https://desktop.github.com/), it's free and easy to use. In its File menu select "Add local repository" and add the workspace folder.

## Remarks

Files and folders imported as links usually are not in the workspace folder and therefore are not included in backup.

### Sync

When using the same workspace on multiple computers (for example with portable LibreAutomate), use the Pull and Push commands to sync its copies (make identical). Use Pull when started to work with the workspace. Use Push when ending to work with the workspace.

```
Computer1 ending    --push-->  GitHub
Computer2 starting  <--pull--  GitHub
Computer2 ending    --push-->  GitHub
Computer1 starting  <--pull--  GitHub
```

When not using Pull and Push strictly in this way, the commit histories in the local and GitHub repositories may diverge. It meens, both repositories contain new different commits. The Pull command detects it and you'll have to choose which new changes you want to keep - local or remote (on GitHub). Git software also supports merging, but it is an advanced operation that can damage some files, and is not recommended to use with LibreAutomate workspaces.

### Excluded files

Git ignores (does not backup etc) files and directories specified in file named ".gitignore" in the workspace directory. The Git setup dialog creates it if missing. You can edit it ([how](https://www.google.com/search?q=.gitignore)). Default text:

```
/.nuget/*/*
/.interop/
/exe/
/dll/

# Don't change these defaults
!/.nuget/*/*.csproj
/.compiled/
/.temp/

```

The first 2 lines exclude directories that contain many big binary files (.dll etc) than can be easily restored. You may want to delete or comment out these lines if cannot easily restore, for example if using portable LibreAutomate. But then your local Git repository (folder ".git") can become very big after some time. Next 2 lines exclude default directories used for your created programs and libraries. The last 2 lines exclude directories where LibreAutomate stores temporary files. Lines starting with `!` reinclude some files in directories excluded by other lines.

If using the same workspace on multiple computers, you can exclude these small folders if don't want to sync:
- /.state/ - saved foldings, caret position, lists of open files and expanded folders, etc.
- /.toolbars/ - saved toolbar settings (position, context menu, etc).

If you don't exclude some big files that are unnecessary to backup, after some time the ".git" folder can become very big. Then you can try "Maintenance" in the Git menu. Or install and use git-filter-repo or similar tool. Afterwards you may want to add these files to .gitignore.

### Git script

Read this if you want to modify the Git menu or change its behavior.

Most Git menu commands are implemented in script named "Git script". The editor program implements only "Git setup" and displays the menu. The Git setup dialog imports default script into the workspace if does not find "Git script.cs". The default script supports only basic Git features. You can instead use a custom script file named "Git script". Don't edit the default script. Copy its text, rename or delete it, create new script named "Git script", paste and edit as you want.

When showing the Git menu, LibreAutomate finds C# file named "Git script.cs", extracts menu item texts etc from `case "Menu item text": ... //{JSON}` lines, and adds menu items. When clicked a menu item, it saves everything and runs the script. Passes the menu item text etc in `args`.