---
uid: nuget
---

# NuGet
In C# scripts you use classes and functions all the time. These come from various libraries, also known as assemblies or dll files. Many libraries are already installed (.NET, LibreAutomate), but there are many more great libraries available on the internet.

Two main places where you can find libraries are [GitHub](https://github.com/) and [NuGet](https://www.nuget.org/). GitHub mostly hosts source code, which you usually don't use directly. NuGet is a repository of compiled libraries, called packages. They're easy to install, update, and use in C# code.

In LibreAutomate, use the **NuGet packages** tool to install and manage NuGet packages.

## How to install a NuGet package
Find a package on the [NuGet](https://www.nuget.org/) website. Click the **Copy** button.

    Note: If the package is only for **.NET Framework**, it is outdated and incompatible.

    Tip: There is a link to the source repository on the right side. Visit it to assess the library better. Avoid abandoned libraries (last commit years ago).

Paste the copied text in the **NuGet packages** window. Or you can enter just the package name, and the tool will get the latest version. Or enter `Name --version 1.2.3`.

In the combo box you can select a folder or type a name for a new folder. Or use the default folder `-`. Finally click the **Install** button.

The **Add code** button inserts a meta-comment like `/*/ nuget folder\Package; /*/` in the current script. Another way - **Properties > NuGet**. You can use an installed package in multiple scripts (just add the meta-comment).

Many code examples in the Cookbook use NuGet packages.

## How and where are packages installed (TL;DR)
LibreAutomate and other NuGet package managers download and extract package files into the global/shared package cache folder `folders.Profile + @".nuget\packages"`. Different versions of a package are installed side by side. The folder is safe to delete.

When installing a package, LibreAutomate copies its files from the cache to `workspace\.nuget\folder specified in the combo box`. Then scripts and the code editor can use these files. When compiling an `exeProgram` script, runtime files are copied to the output folder.

You can install multiple packages in a folder. You may want to install large libraries (many dll files) each in its own folder. Folders also can be used to isolate incompatible packages if need (rarely). For example, `PackageX` version 1 in `FolderA`, and `PackageX` version 2 in `FolderB`. An installed package can be used by all scripts of the workspace. A script can use packages from multiple folders if they are compatible.

When a package depends on other packages, those are installed too. The list contains only the top-level package, but in scripts you can use all of them.

LibreAutomate uses standard, well-documented commands like `dotnet add package`. Prints them when installing. Uses `nuget.config` files (sources etc) from standard locations and `workspace\.nuget`.

Some NuGet packages don't install all required files, for example native dlls. Try this:
- Often the missing files are in other NuGet packages. Install these packages.
- Else you may have to download the missing files. In some cases they are in the `.nupkg` file (it is a zip file) in the packages cache folder. Click the **Folder** button and copy these files there. Set read-only attribute to prevent deleting them when managing packages. If the missing files are managed assemblies, in scripts that use them will need `/*/ r Dll; /*/` (use **Properties > Library**). If used in scripts with role `exeProgram`, copy these files to the output folder.

## Used software
Installing packages requires the .NET SDK. By default, LibreAutomate uses it if  installed; else downloads (~26 MB) and uses a private minimal SDK. See also **Options > Other > Use minimal SDK**. Portable LibreAutomate requires the full .NET SDK.

To ensure compatibility with all .NET Runtime versions, the minimal SDK uses the oldest build of the current major .NET SDK. Because of this, the Publish feature may fail to build code that uses the latest C# preview features. Solution: install the latest full SDK and ucheck **Options > Other > Use minimal SDK**. Note: the Publish feature is not used to install NuGet packages; it just uses the same minimal SDK.
