---
uid: git
---

# Git, backup, sync

## Git and GitHub

Git software can be used to make multiple backups of workspace files (scripts etc) and store them in local and cloud repositories.

GitHub is the cloud service where most programmers have their public and private Git repositories.

To backup workspace files can be used LibreAutomate or/and any Git software.

## How to start

You need a [GitHub](https://github.com/) account, and a private repository there. It's free.

1. In your GitHub account [create new repository](https://github.com/new). It must be private and empty (no readme etc). Copy its URL.
2. In LibreAutomate click menu **File > Git > Git setup**. Paste the URL.
3. If Git not installed, click the **Install** button. It installs MinGit (minimal Git) in the LibreAutomate folder (subfolder `Git`). Or you can download and install Git for Windows (not in LA folder); let it set *PATH* variable.
4. Click OK. What it does:
    - Connects to your GitHub account to verify the repository.
    - Creates/initializes local Git repository (hidden subfolder `.git` in the workspace folder).
    - If local repository already exists, just updates its settings (URL).
    - Creates files `.gitignore` and `.gitattributes` if missing.
5. Now you can use other **Git** menu commands. To access it faster, you can unhide the **Git** button in the **File** toolbar.

## How to use

Use menu **File > Git**. Menu commands:

- **Git status** - print Git status info for this repository. It helps you to decide whether you want to commit, push, pull or do nothing.
- **Commit** - make local backup. Adds it to the local Git repository (hidden subfolder `.git` in the workspace folder). Does nothing if there are no changes since the last commit.
- **Push to GitHub** - commit if need, and upload new local commits to the GitHub repository.
- **Pull from GitHub** - if the GitHub repository contains new commits that are not in the local repository, downloads them to the local repository and updates workspace files (scripts etc).
- **GitHub Desktop** - run GitHub Desktop (if installed).
- **Cmd** - run `cmd.exe`. Then you can execute any Git commands.
- **Workspace folder** - open the workspace folder.
- **Reload workspace** - reload current workspace. Need it after an external program (GitHub Desktop, Git, etc) modified workspace files.
- **GitHub sign out** - delete GitHub credentials saved on this computer. You may want it when using portable LibreAutomate.
- **Maintenance** - tools to compact the Git repository.
- **Git setup** - Git setup dialog.

## Remarks

LibreAutomate uses Git command line programs to perform most of the work. Therefore Git must be installed. Either private Git (in LibreAutomate folder) or regular Git. Often LibreAutomate prints the used command line (blue text), and sometimes also the output text (red if failed).

Files and folders imported as links usually are not in the workspace folder and therefore are not included in backup.

The current branch must be `main`. Else **Git** menu commands don't work.

Default Git credential helper is Git Credential Manager. If it does not work on your computer, use another helper.

### Other Git software

Git has many features not included in the **Git** menu. To name a few:
- See the commit history.
- See workspace changes since the last commit, and changes in each commit.
- Restore files from a backup (ie from a commit).
- Pull and merge diverged commits.
- Use branches.

Git can be used via command line. Or you can install a [Git GUI client](https://git-scm.com/downloads/guis). Try [GitHub Desktop](https://desktop.github.com/), it's free and easy to use. In its **File** menu select **Add local repository** and add the workspace folder. If you need a GUI client with more features, try TortoiseGit.

When using external Git software to manage the current LibreAutomate workspace, you can temporarily exit LibreAutomate. It isn't necessary, however you should know that:
- If external Git software made changes in the workspace while LibreAutomate is running, click **Reload workspace** in the **Git** menu.
- LibreAutomate saves everything when another app becomes active (active window). Still there is a small possibility that it may change some workspace files while you use Git.

Use external Git software carefully. Else it can modify workspace files not as you wanted. Consider making a temporary copy of entire workspace folder. The Git menu is safer; the **Pull** command can modify workspace files, but the old version isn't lost anyway.

### Sync

When using the same workspace on multiple computers (for example with portable LibreAutomate), use the **Pull** and **Push** commands to sync its copies (make identical). Use **Pull** when started to work with the workspace. Use **Push** when ending to work with the workspace.

```
Computer1 ending    --push-->  GitHub
Computer2 starting  <--pull--  GitHub
Computer2 ending    --push-->  GitHub
Computer1 starting  <--pull--  GitHub
```

When not using **Pull** and **Push** strictly in this way, the commit histories in the local and GitHub repositories may diverge. It meens, both repositories contain new different commits. The **Pull** command detects it and you'll have to choose which new changes you want to keep - local or remote (on GitHub). Git software also supports merging, but it is an advanced operation that can damage some files, and is not recommended to use with LibreAutomate workspaces.

### Excluded files

Git ignores (does not backup etc) files and directories specified in file named `.gitignore` in the workspace directory. The Git setup dialog creates it if missing. You can edit it ([how](https://www.google.com/search?q=.gitignore)). Default text:

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

The first 2 lines exclude directories that contain many big binary files (.dll etc) than can be easily restored. You may want to delete or comment out these lines if cannot easily restore, for example if using portable LibreAutomate. But then your local Git repository (folder `.git`) can become very big after some time. Next 2 lines exclude default directories used for your created programs and libraries. The last 2 lines exclude directories where LibreAutomate stores temporary files. Lines starting with `!` reinclude some files in directories excluded by other lines.

If using the same workspace on multiple computers, you can exclude these small folders if don't want to sync:
- `/.state/` - saved foldings, caret position, lists of open files and expanded folders, etc.
- `/.toolbars/` - saved toolbar settings (position, context menu, etc).

If you don't exclude some big files that are unnecessary to backup, after some time the `.git` folder can become very big. Then you can try **Maintenance** in the **Git** menu. Or install and use git-filter-repo or similar tool. Afterwards you may want to add these files to .gitignore.

### Restoring workspace files from backup

If you have the workspace folder with Git repository on current computer, you can restore workspace files to the state saved at the time of the last or some older commit. A commit is a backup; you make it with **Git** menu command **Commit** or **Push to GitHub**. To restore, use [Git software](#other-git-software).

In GitHub Desktop:
- If there are files displayed in the **Changes** tab and you want to restore them from the last commit:
    - To restore all files: menu **Branch > Discard all changes**.
    - To restore a file: right click it and select **Discard changes**. With `Ctrl` or `Shift` you can select multiple files.
    - Alternative way to restore all files: commit and then revert (read below).
- Or you can restore the workspace from an older commit. In **History** right click the top commit and select **Revert**. It retains all commits and adds 1 new commit. If need, repeat this for other commits made after the wanted commit, in top-down order.

After restoring, if LibreAutomate is running, in its **Git** menu select **Reload workspace**. Or exit LibreAutomate before restoring.

If you don't have the workspace folder with Git repository on current computer, you can clone (download) it from a GitHub repository. Run LibreAutomate, and it will open the last used or new workspace. In the **Git** menu click **Git setup**, enter repository URL, select **Clone**, **OK**. Then you can open and use the cloned workspace. Or you can clone using any Git software.

### Auto-backup

**Options > Workspace > Auto-backup (Git commit)**.
