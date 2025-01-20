LA uses modified Roslyn dlls.
How to install, update and modify:

Once: Build this project. Don't run; it will run automatically when building Roslyn project Microsoft.CodeAnalysis.CSharp.Features.

Download https://github.com/dotnet/roslyn and unzip to C:\code-lib\roslyn. Or git clone, if you need extra 2 GB.
Open Roslyn.sln.
To make VS not so slow, select all folders and unload projects. Then load Microsoft.CodeAnalysis.CSharp.Features with entire dependency tree.
  It loads projects we need:
    In folder Compilers: Core\Microsoft.CodeAnalysis, CSharp\Microsoft.CodeAnalysis.CSharp.
    In folder Features: Microsoft.CodeAnalysis.CSharp.Features, Microsoft.CodeAnalysis.Features.
    In folder Workspaces: Microsoft.CodeAnalysis.CSharp.Workspaces, Microsoft.CodeAnalysis.Workspaces.
    Several other.
Optionally create new local git repo. Then later you can conveniently see modifications.
Edit as described below after _________.
Build Microsoft.CodeAnalysis.CSharp.Features. It also builds all dependency projects. It runs this exe.

Once: In editor project add references to the main 6 dlls in _\Roslyn, with 'Copy local' false.
  For Microsoft.CodeAnalysis.Workspaces set aliases CAW.

Rejected: to make editor startup faster, publish Microsoft.CodeAnalysis.CSharp.Features with <PublishReadyToRun>.
  Tested, works, but: adds ~14 MB to the setup file; makes just ~350 ms faster, barely noticeable.

FUTURE: bug in latest Roslyn (2024-11-22): error if `for` has >1 uninited vars, like `for (int i = 0, j, k; i < 9; i++) {}`.
  Now in VS too. In VS was no error when I first found this; it used an older Roslyn.

_________________________________________________________________

//Edit these manually. Often Roslyn source in new version is changed in some of these places.
//Add only internal members (where possible). If public, need to declare it in PublicApi.Shipped.txt. Roslyn's internals are visible to the editor project.

// - In all 6 projects + Scripting.csproj from <TargetFrameworks> remove netstandard2.0 etc (replace with eg net9.0). Later will compile faster.
//  Don't modify CSharpSyntaxGenerator.csproj.
//  If will fail to compile, do this AFTER the first compilation.

// - Set Release config. Try to build Microsoft.CodeAnalysis.CSharp.Features (it builds all).
//	If error "SDK not found", install the latest .NET SDK or edit .NET version in global.json.

// - In all 6 projects add:
    <InternalsVisibleTo Include="Au.Editor" Key="0024000004800000940000000602000000240000525341310004000001000100095c6b7a0fe60fbe4a77e52dd10a09331ee3c3a7399aa9cc17db8a015647469a19784d5e33a2450a0a49c37bf17c0c3223674f64104eae649ba27c51a90c24989faec87d59217d7850efc8151109bbf9b027b7714fc01788317d2b991b2c2669836a7725e942f76607efde5cdacd8c497a45c5f9673fcf102fdbf92237a524a4" />

// - In project Microsoft.CodeAnalysis add link to Au.TestInternal.cs. It is in this project.
    <Compile Include="C:\code\au\Other\BuildEvents\Au.TestInternal.cs" Link="Au.TestInternal.cs" />

// - In Microsoft.CodeAnalysis.CSharp.Features.csproj:
// -- In <PropertyGroup> add:
    <OutputPath>$(SolutionDir)au\output</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <AccelerateBuildsInVisualStudio>false</AccelerateBuildsInVisualStudio>

// -- In project references add <Private>True</Private>. Also add the Scripting project (just to copy the dll).
  <ItemGroup Label="Project References">
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
  </ItemGroup>

// -- Add prebuild and postbuild:
  <Target Name="PreBuild" BeforeTargets="PreBuildEvent">
    <Exec Command="C:\code\au\Other\BuildEvents\bin\Debug\BuildEvents.exe roslynPreBuild" />
  </Target>
  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Exec Command="C:\code\au\Other\BuildEvents\bin\Debug\BuildEvents.exe roslynPostBuild $(OutDir)" />
  </Target>

// - In .gitignore add:
au/


// - Add Symbols property to the CompletionItem class:
//1. Open CompletionItem.cs in project Microsoft.CodeAnalysis.Features.
//2. Find method private CompletionItem With(...). In it find: return new CompletionItem...{
//3. In the { } add line:
            Symbols = Symbols, //au
//4. Below the method add properties:
    internal IReadOnlyList<ISymbol>? Symbols { get; set; } //au
    internal object? Attach { get; set; } //au
//5. Open Features\Core\Portable\Completion\Providers\SymbolCompletionItem.cs.
//6. In method CreateWorker find statement that starts with: var item = CommonCompletionItem.Create(
//7. Below that statement add: item.Symbols = symbols; //au

// - Add Symbol property to the SymbolKeySignatureHelpItem class:
//1. Open Features\Core\Portable\SignatureHelp\AbstractSignatureHelpProvider.SymbolKeySignatureHelpItem.cs.
//2. Add property: internal ISymbol? Symbol { get; } = symbol; //au

// - Let it don't try to load VB assemblies, because then exception when debugging:
//In MefHostServices.cs, in s_defaultAssemblyNames init list, remove the 2 VB assemblies.

// - In project Microsoft.CodeAnalysis, in file PublicAPI.Shipped.txt, append:
RoslynMod.TestInternal
static RoslynMod.TestInternal.IsInternalsVisible(string thisName, string toName) -> System.Collections.Generic.IEnumerable<System.Collections.Immutable.ImmutableArray<byte>>
static RoslynMod.TestInternal.AppendInternalsVisible(string thisName, System.Collections.Generic.HashSet<string> toNames) -> void
RoslynMod.Print
static RoslynMod.Print.it(object o) -> void

// - In project Microsoft.CodeAnalysis, in MetadataReader\PEAssembly.cs, in GetInternalsVisibleToPublicKeys:
//	replace
            return result ?? SpecializedCollections.EmptyEnumerable<ImmutableArray<byte>>();
//	with
            //au
            return result ?? RoslynMod.TestInternal.IsInternalsVisible(this.Identity.Name, simpleName);

// - In project Microsoft.CodeAnalysis.CSharp, in Symbols\Source\SourceAssemblySymbol.cs, replace GetInternalsVisibleToPublicKeys with:

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

// - In project Microsoft.CodeAnalysis.Workspaces, in file DependentProjectsFinder.cs, in GetInternalsVisibleToSet, insert before 'return':
        //au:
        RoslynMod.TestInternal.AppendInternalsVisible(assembly.Name, set);
