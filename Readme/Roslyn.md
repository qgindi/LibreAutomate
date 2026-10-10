LA uses modified Roslyn dlls.
How to install, update and modify.

Fork and clone Roslyn:
1. Delete the old fork repo `https://github.com/qgindi/roslyn`.
2. Fork `https://github.com/dotnet/roslyn`. In repo settings set default branch = the latest release, eg `release/insiders`.
3. Git-clone `https://github.com/qgindi/roslyn.git` to `C:\code-lib\roslyn`. Do shallow clone of default branch (full >2 GB). I clone with TortoiseGit.

[Another way](https://docs.github.com/en/pull-requests/how-tos/work-with-forks/syncing-a-fork) to update, not tested: in the GitHub fork repo page click `Sync fork`. It discards modifications. Then git-sync eg in VS. Bad: bloats `.git`.

May need to install the SDK version specified in `global.json`.

Open `Roslyn.slnx`.

To make VS not so slow, select all folders and unload projects. Then load `Microsoft.CodeAnalysis.CSharp.Features` with entire dependency tree. Make it the startup project.
  It loads projects we need:
- In folder `Compilers`: `Core\Microsoft.CodeAnalysis`, `CSharp\Microsoft.CodeAnalysis.CSharp`.
- In folder `Features`: `Microsoft.CodeAnalysis.CSharp.Features`, `Microsoft.CodeAnalysis.Features`.
- In folder `Workspaces`: `Microsoft.CodeAnalysis.CSharp.Workspaces`, `Microsoft.CodeAnalysis.Workspaces`.
- Several other.

The last time the **Reload project with dependencies** and similar commands did not work. Navigation to project references too. Had to reload all projects. To make not so slow, opened the `slnx` file in a code editor and deleted unused projects - where name contains `Test`, `VisualBasic`, `Razor`. After waiting for background operations and restarting VS several times, managed to build.

Set `Release` config. Try to build `Microsoft.CodeAnalysis.CSharp.Features` (it builds all needed projects). When successful, delete the large `artifacts` folder.

Edit as described below in the `Edit` section.

Exit LA. Make a backup copy of folder `_\Roslyn`. ALl it's files will be deleted and then copied new files.

Make sure project `BuildEvents` (here) is built.

Build `Microsoft.CodeAnalysis.CSharp.Features`. It also builds all dependency projects.  
A build event runs `BuildEvents.exe`; it copies dll/xml files to `_\Roslyn`.

Build `Au.Editor`. Likely will be several errors because of changes in Roslyn's internal APIs used in `Au.Editor`.

Git commit/push (if git-cloned).

Once: In editor project add references to the main 6 dlls in `_\Roslyn`, with 'Copy local' false. 
For `Microsoft.CodeAnalysis.Workspaces` set aliases `CAW`.

Rejected: to make editor startup faster, publish `Microsoft.CodeAnalysis.CSharp.Features` with `<PublishReadyToRun>`.  
Tested, works, but: adds >15 MB to the setup file; makes just ~350 ms faster, barely noticeable.

## Edit

Edit these manually. Often Roslyn source in new version is changed in some of these places.

Add only internal members (where possible). If public, need to declare it in `PublicApi.Shipped.txt`. Roslyn's internals are visible to the editor project.

---

In all 6 projects + `Scripting.csproj` (but not `CSharpSyntaxGenerator.csproj`):  
From `<TargetFrameworks>` remove `netstandard2.0` etc (replace with eg `net10.0`). Later will compile faster.

---

Try to build `Microsoft.CodeAnalysis.CSharp.Features`.

---

In all 6 projects add:
```
    <InternalsVisibleTo Include="Au.Editor" Key="0024000004800000940000000602000000240000525341310004000001000100095c6b7a0fe60fbe4a77e52dd10a09331ee3c3a7399aa9cc17db8a015647469a19784d5e33a2450a0a49c37bf17c0c3223674f64104eae649ba27c51a90c24989faec87d59217d7850efc8151109bbf9b027b7714fc01788317d2b991b2c2669836a7725e942f76607efde5cdacd8c497a45c5f9673fcf102fdbf92237a524a4" />
```

---

In project `Microsoft.CodeAnalysis` add link to `Au.TestInternal.cs`. It is in project `BuildEvents` here.
```
    <Compile Include="C:\code\au\Other\BuildEvents\Au.TestInternal.cs" Link="Au.TestInternal.cs" />
```

---

In `Microsoft.CodeAnalysis.CSharp.Features.csproj`:
- In `<PropertyGroup>` add:
```
    <OutputPath>$(SolutionDir)au\output</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <AccelerateBuildsInVisualStudio>false</AccelerateBuildsInVisualStudio>
```

- In project references add `<Private>True</Private>`. Also add the `Scripting` project (just to copy the dll).
```
    <ProjectReference Include="..\..\Core\Portable\Microsoft.CodeAnalysis.Features.csproj">
      <Private>True</Private>
    </ProjectReference>
    <ProjectReference Include="..\..\..\Workspaces\CSharp\Portable\Microsoft.CodeAnalysis.CSharp.Workspaces.csproj">
      <Private>True</Private>
    </ProjectReference>
    <ProjectReference Include="..\..\..\Workspaces\Core\Portable\Microsoft.CodeAnalysis.Workspaces.csproj">
      <Private>True</Private>
    </ProjectReference>
    <ProjectReference Include="..\..\..\Compilers\CSharp\Portable\Microsoft.CodeAnalysis.CSharp.csproj">
      <Private>True</Private>
    </ProjectReference>
    <ProjectReference Include="..\..\..\Compilers\Core\Portable\Microsoft.CodeAnalysis.csproj">
      <Private>True</Private>
    </ProjectReference>
    <ProjectReference Include="..\..\..\Scripting\Core\Microsoft.CodeAnalysis.Scripting.csproj">
      <Private>True</Private>
    </ProjectReference>
```

- Add post-build event:
```
  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Exec Command="C:\code\au\Other\BuildEvents\bin\Debug\BuildEvents.exe roslynPostBuild $(OutDir)" />
  </Target>
```

---

In `.gitignore` add:
```
au/
```

---

Delete the `artifacts` folder.

---

Add property `Symbols` to the `CompletionItem` class:  
- Open `CompletionItem.cs` in project `Microsoft.CodeAnalysis.Features`.
- Find method `private CompletionItem With`. In it find: `return new CompletionItem...{`
- In the `{ }` add line:
```
            Symbols = Symbols, //au
```
- Below the method add properties:
```
    internal IReadOnlyList<ISymbol>? Symbols { get; set; } //au
    internal object? Attach { get; set; } //au
```
- Open `Features\Core\Portable\Completion\Providers\SymbolCompletionItem.cs`.
- In method `CreateWorker` find statement that starts with: `var item = CommonCompletionItem.Create(`
- Below that statement add: `item.Symbols = symbols; //au`

---

Add property `Symbol` to the `SymbolKeySignatureHelpItem` class:
- Open `Features\Core\Portable\SignatureHelp\AbstractSignatureHelpProvider.SymbolKeySignatureHelpItem.cs`.
- Add property: `internal ISymbol? Symbol { get; } = symbol; //au`

---

Let it don't try to load VB assemblies, because then exception when debugging:  
In `MefHostServices.cs`, in `DefaultAssemblyNames` init list, remove the 2 VisualBasic assemblies.

---

In project `Microsoft.CodeAnalysis`, in file `PublicAPI.Shipped.txt`, append:
```
RoslynMod.TestInternal
static RoslynMod.TestInternal.IsInternalsVisible(string thisName, string toName) -> System.Collections.Generic.IEnumerable<System.Collections.Immutable.ImmutableArray<byte>>
static RoslynMod.TestInternal.AppendInternalsVisible(string thisName, System.Collections.Generic.HashSet<string> toNames) -> void
RoslynMod.Print
static RoslynMod.Print.it(object o) -> void
```

---

In project `Microsoft.CodeAnalysis`, in `MetadataReader\PEAssembly.cs`, in `GetInternalsVisibleToPublicKeys`:  
replace
```
            return result ?? SpecializedCollections.EmptyEnumerable<ImmutableArray<byte>>();
```
with
```
            //au
            return result ?? RoslynMod.TestInternal.IsInternalsVisible(this.Identity.Name, simpleName);
```

---

In project `Microsoft.CodeAnalysis.CSharp`, in `Symbols\Source\SourceAssemblySymbol.cs`, replace `GetInternalsVisibleToPublicKeys` with:
```
        //au
        internal override IEnumerable<ImmutableArray<byte>> GetInternalsVisibleToPublicKeys(string simpleName)
        {
            EnsureAttributesAreBound();

            if (_lazyInternalsVisibleToMap != null && _lazyInternalsVisibleToMap.TryGetValue(simpleName, out var result))
                return result.Keys;

            return RoslynMod.TestInternal.IsInternalsVisible(this.Name, simpleName);
        }
        //internal override IEnumerable<ImmutableArray<byte>> GetInternalsVisibleToPublicKeys(string simpleName)
        //{
        //    //EDMAURER assume that if EnsureAttributesAreBound() returns, then the internals visible to map has been populated.
        //    //Do not optimize by checking if m_lazyInternalsVisibleToMap is Nothing. It may be non-null yet still
        //    //incomplete because another thread is in the process of building it.

        //    EnsureAttributesAreBound();

        //    if (_lazyInternalsVisibleToMap == null)
        //        return SpecializedCollections.EmptyEnumerable<ImmutableArray<byte>>();

        //    ConcurrentDictionary<ImmutableArray<byte>, Tuple<Location, string>> result = null;

        //    _lazyInternalsVisibleToMap.TryGetValue(simpleName, out result);

        //    return (result != null) ? result.Keys : SpecializedCollections.EmptyEnumerable<ImmutableArray<byte>>();
        //}
```

---

In project `Microsoft.CodeAnalysis.Workspaces`, in file `DependentProjectsFinder.cs`, in `GetInternalsVisibleToSet`, insert before `return`:
```
        //au:
        RoslynMod.TestInternal.AppendInternalsVisible(assembly.Name, set);
```
