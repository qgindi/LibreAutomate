//This small program copies Roslyn dll/xml files to _\Roslyn.
//Also exits editor.

//How to get Roslyn dlls:

//Download Roslyn solution to C:\Downloads\roslyn-main.
//Open Roslyn.sln.
//To make VS not so slow, select all folders and unload projects. Then load Microsoft.CodeAnalysis.CSharp.Features with entire dependency tree. It loads projects we need:
//	In folder Compilers: Core\Microsoft.CodeAnalysis, CSharp\Microsoft.CodeAnalysis.CSharp.
//	In folder Features: Microsoft.CodeAnalysis.CSharp.Features, Microsoft.CodeAnalysis.Features.
//	In folder Workspaces: Microsoft.CodeAnalysis.CSharp.Workspaces, Microsoft.CodeAnalysis.Workspaces.
//	Several other.
//Edit as described in the '#if false' block at the bottom of this file.
//Build Microsoft.CodeAnalysis.CSharp.Features. It also builds all dependency projects.

//In this project add or update Microsoft.CodeAnalysis.CSharp.Features from NuGet.
//In this file edit c_netVersion if need.
//Run this project. May need to build explicitly, depending on solution config.
//	Run it always after modifying Roslyn.

//In editor project are added references to the main 6 dlls in _\Roslyn, with 'Copy local' false.


const string c_netVersion = "net8.0"; //edit if need

try {
	CopyFiles();
}
catch (Exception ex) { Console.WriteLine(ex); }

static void CopyFiles() {
	var w = wnd.findFast("LibreAutomate");
	if (!w.Is0) {
		int id = w.ProcessId;
		w.Close(noWait: true);
		process.waitForExit(10, id, out _);
	}

	var artifacts = folders.Downloads + @"roslyn-main\artifacts\bin\";
	var roslyn = @"C:\code\au\_\Roslyn";

	foreach (var f in Directory.GetFiles(roslyn)) {
		filesystem.delete(f);
	}

	string[] a = {
		"Microsoft.CodeAnalysis",
		"Microsoft.CodeAnalysis.CSharp",
		"Microsoft.CodeAnalysis.Features",
		"Microsoft.CodeAnalysis.CSharp.Features",
		"Microsoft.CodeAnalysis.Workspaces",
		"Microsoft.CodeAnalysis.CSharp.Workspaces",
		"Microsoft.CodeAnalysis.Scripting",
	};
	foreach (var v in a) {
		var s = artifacts + v + $"\\Release\\{c_netVersion}\\";
		filesystem.copyTo(s + v + ".dll", roslyn);
        filesystem.copyTo(s + v + ".xml", roslyn);
	}

	foreach (var f in Directory.GetFiles(folders.ThisApp, "*.dll")) {
        if (f.Ends("CompilerDlls.dll")) continue;
        var f2 = roslyn + "\\" + pathname.getName(f);
        if (!filesystem.exists(f2)) filesystem.copy(f, f2);
	}
}

#if false
//Edit these manually, because either difficult to automate or Roslyn source in new version is likely changed in that place.
//Add only internal members (where possible). If public, need to declare it in PublicApi.Shipped.txt. Roslyn's internals are visible to the editor project.

// - In all 6 projects + Scripting.csproj from <TargetFrameworks> remove netstandard2.0 etc. Will compile faster and produce less garbage.

// - Set Release config. Try to build Microsoft.CodeAnalysis.CSharp.Features (it builds all).
//	May need to download the latest .NET SDK. Its version specified in global.json.

//(skip this if can compile) - Remove code that uses Scripting project:
//1. In Features\Core\Portable\Completion\Providers\Scripting\AbstractDirectivePathCompletionProvider.cs remove 2 Scripting usings and 1 code block that uses it.
//2. In Features\Core\Portable\Completion\Providers\Scripting\AbstractReferenceDirectiveCompletionProvider.cs remove 1 Scripting using and remove entire body of ProvideCompletionsAsync.
//3. Remove entire Features\Core\Portable\Completion\Providers\Scripting\GlobalAssemblyCacheCompletionHelper.cs.

// - In all 6 projects add link to Au.InternalsVisible.cs. It is in this project.

// - Add Symbols property to the CompletionItem class:
//1. Open CompletionItem.cs in project Microsoft.CodeAnalysis.Features.
//2. Find method private CompletionItem With(...). In it find: return new CompletionItem...{
//3. In the { } add line: Symbols = Symbols, //au
//4. Below the method add properties:
//        internal System.Collections.Generic.IReadOnlyList<ISymbol>? Symbols { get; set; } //au
//        internal object? Attach { get; set; } //au
//5. Open Features\Core\Portable\Completion\Providers\SymbolCompletionItem.cs.
//6. In method CreateWorker find statement that starts with: var item = CommonCompletionItem.Create(
//7. Below that statement add: item.Symbols = symbols; //au

// - Add Symbol property to the SymbolKeySignatureHelpItem class:
//1. Open Features\Core\Portable\SignatureHelp\AbstractSignatureHelpProvider.SymbolKeySignatureHelpItem.cs.
//2. Add property: internal ISymbol? Symbol { get; } = symbol; //au

// - Let it don't try to load VB assemblies, because then exception when debugging:
//In MefHostServices.cs, in s_defaultAssemblyNames init list, remove the 2 VB assemblies.

// - In project Microsoft.CodeAnalysis add link to Au.TestInternal.cs. It is in this project.

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

// - In project Microsoft.CodeAnalysis.Workspaces, in file DependencyProjectsFinder.cs, in GetInternalsVisibleToSet, insert before 'return':
            //au:
            RoslynMod.TestInternal.AppendInternalsVisible(assembly.Name, set);

// - (old bug fix; not applied. The code changed completely. Probably now the following new mod fixes that bug.) In SignatureHelpUtilities.cs, function GetSignatureHelpState, remove the 'if' block:
            //au: bug fix. This code replaces correct ArgumentIndex with incorrect. Then another function throws exception. Editor could handle the exception, but then no parameter info.
            //if (result is not null && parameterIndex >= 0)
            //{
            //    result.ArgumentIndex = parameterIndex;
            //}

// - (bug fix) In AbstractSignatureHelpProvider.cs, function CreateSignatureHelpItems:
            //if (parameterIndexOverride >= 0)
            if (parameterIndexOverride >= 0 && parameterIndexOverride < state.Value.ArgumentCount) //au: prevent exception in SignatureHelpItems ctor

// - (bug fix) In AbstractCSharpSignatureHelpProvider.LightweightOverloadResolution.cs, function FindParameterIndexIfCompatibleMethod:
                        //Au: bug fix. Would throw invalid index exception in eg dialog.show(x: 5,). Not perfect.
                        //if (parameterIndex >= 0)
                        if (parameterIndex == i)

#endif
