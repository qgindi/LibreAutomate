using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

//This small program modifies the Roslyn solution.
//Setup:
//Download Roslyn solution to Q:\Downloads\roslyn-main.
//Open Roslyn.sln.
//To make VS not so slow, select all folders and unload projects. Then load Microsoft.CodeAnalysis.CSharp.Features with entire dependency tree. It loads projects we need:
//	In folder Compilers: Core\Microsoft.CodeAnalysis, CSharp\Microsoft.CodeAnalysis.CSharp.
//	In folder Features: Microsoft.CodeAnalysis.CSharp.Features, Microsoft.CodeAnalysis.Features.
//	In folder Workspaces: Microsoft.CodeAnalysis.CSharp.Workspaces, Microsoft.CodeAnalysis.Workspaces.
//	Several other.
//(skip this if can compile) From Microsoft.CodeAnalysis.Features dependencies remove Scripting. Unload Scripting project. Because it does not compile, and not useful.
//Edit as described in the '#if false' block at the bottom of this file.
//Run this project. It modifies Roslyn solution project files.
//In Roslyn solution compile Microsoft.CodeAnalysis.CSharp.Features. It also compiles all dependency projects. Copies 7 or 8 dlls to Q:\app\Au\Other\CompilerDlls.
//In editor project do this once:
//	Add references to the 6 dlls from folder Q:\app\Au\Other\CompilerDlls (listed above).
//	Add this in editor project for each Roslyn reference: <DestinationSubDirectory>Roslyn\</DestinationSubDirectory>
//	On build VS will copy the dlls to _\Roslyn.
//	VS will detect when the dlls modified when building Roslyn.
//	Edit AppHost.cpp, let it add subfolder Roslyn like it does for subfolder Libraries.
//To get other dlls:
//	Install or update Microsoft.CodeAnalysis.CSharp.Features from NuGet in this project.
//	Compile. Copy these dlls from the bin folder to _\Roslyn:
//		Microsoft.DiaSymReader.dll
//		System.Composition.AttributedModel.dll
//		System.Composition.Hosting.dll
//		System.Composition.Runtime.dll
//		System.Composition.TypedParts.dll
//		Microsoft.VisualStudio.Debugger.Contracts.dll
//		Also copy all other Roslyn dlls from there, although it seems works without.
//		In the past also needed Microsoft.CodeAnalysis.FlowAnalysis.Utilities.dll, but now it is removed, and works without it.
//In Roslyn artifacts folder find xml doc files and copy to CompilerDlls. VS will copy it to _\Roslyn when building editor.

namespace CompilerDlls
{
	class Program
	{
		static void Main(string[] args)
		{
			try {
				ModRoslyn();
			}
			catch(Exception ex) { Console.WriteLine(ex); }
		}

		static void ModRoslyn()
		{
			bool writeFile = true;

			string roslynDir = @"Q:\Downloads\roslyn-main\src\";

			var project = @"</Project>";
			var copy = @"  <Target Name=""PostBuild"" AfterTargets=""PostBuildEvent"">
    <Exec Command=""copy &quot;$(TargetPath)&quot; &quot;Q:\app\Au\Other\CompilerDlls\$(TargetFileName)&quot; /y"" />
  </Target>
";
			_Mod(@"Features\CSharp\Portable\Microsoft.CodeAnalysis.CSharp.Features.csproj", (project, copy, -1));
			_Mod(@"Features\Core\Portable\Microsoft.CodeAnalysis.Features.csproj", (project, copy, -1));
			_Mod(@"Compilers\CSharp\Portable\Microsoft.CodeAnalysis.CSharp.csproj", (project, copy, -1));
			_Mod(@"Compilers\Core\Portable\Microsoft.CodeAnalysis.csproj", (project, copy, -1));
			_Mod(@"Workspaces\CSharp\Portable\Microsoft.CodeAnalysis.CSharp.Workspaces.csproj", (project, copy, -1));
			_Mod(@"Workspaces\Core\Portable\Microsoft.CodeAnalysis.Workspaces.csproj", (project, copy, -1));
			_Mod(@"Tools\Source\CompilerGeneratorTools\Source\CSharpSyntaxGenerator\CSharpSyntaxGenerator.csproj", (project, copy, -1));
			_Mod(@"Scripting\Core\Microsoft.CodeAnalysis.Scripting.csproj", (project, copy, -1));

			//how: 0 replace, 1 insert after, -1 insert before
			void _Mod(string file, params (string find, string add, int how)[] p)
			{
				file = roslynDir + file;
				var s = File.ReadAllText(file);
				int moded = 0;
				foreach(var v in p) {
					if(_Mod1(ref s, v.find, v.add, v.how)) moded++;
				}
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine($"Made {moded} mods in {file}");
				Console.ForegroundColor = ConsoleColor.White;
				if(moded == 0) return;
				if(writeFile) File.WriteAllText(file, s);
				else Console.WriteLine(s);
			}

			bool _Mod1(ref string s, string find, string add, int how)
			{
				//if(s.Contains(add)) return false;
				var s2 = s.Replace("\r", "");
				var add2 = add.Replace("\r", "");
				if(s2.Contains(add2)) return false;

				int i = 0, len = 0;
				if(find.Length > 0) {
					var m = Regex.Match(s, "(?m)^[ \t]*" + Regex.Escape(find) + (how < 0 ? "$" : "\r?\n"), RegexOptions.CultureInvariant);
					if(!m.Success) throw new Exception($"Cannot find '{find}'.");
					i = m.Index;
					len = m.Length;
				}
				switch(how) {
				case 0:
					s = s.Remove(i, len);
					break;
				case 1:
					i += len;
					break;
				}
				s = s.Insert(i, add);

				return true;
			}

			Console.WriteLine(@"Roslyn source has heen modified successfully.");
		}
	}
}

#if false
//Edit these manually, because either difficult to automate or Roslyn source in new version is very likely changed in that place.
//Add only internal members. If public, need to declare it in PublicApi.Shipped.txt. Roslyn's internals are visible to the editor project.

// - In all 6 projects (not in all projects!) .csproj replace <TargetFrameworks>netcoreapp3.1;netstandard2.0</TargetFrameworks> with <TargetFramework>netcoreapp3.1</TargetFramework>

// - Set Release config. Try to compile.

//(skip this if can compile) - Remove code that uses Scripting project:
//1. In Features\Core\Portable\Completion\Providers\Scripting\AbstractDirectivePathCompletionProvider.cs remove 2 Scripting usings and 1 code block that uses it.
//2. In Features\Core\Portable\Completion\Providers\Scripting\AbstractReferenceDirectiveCompletionProvider.cs remove 1 Scripting using and remove entire body of ProvideCompletionsAsync.
//3. Remove entire Features\Core\Portable\Completion\Providers\Scripting\GlobalAssemblyCacheCompletionHelper.cs.

// - In all 6 projects add link to Au.InternalsVisible.cs. It is in this project.

// - Add Symbols property to the CompletionItem class:
//1. Open Features\Core\Portable\Completion\CompletionItem.cs in project Microsoft.CodeAnalysis.Features.
//2. Find method private CompletionItem With(...). In it find: return new CompletionItem...{
//3. In the { } add line: Symbols = Symbols, //au
//4. Below the method add property: internal System.Collections.Generic.IReadOnlyList<ISymbol> Symbols { get; set; } //au
//5. Open Features\Core\Portable\Completion\Providers\SymbolCompletionItem.cs.
//6. In method CreateWorker find statement that starts with: var item = CommonCompletionItem.Create(
//7. Below that statement add: item.Symbols = symbols; //au

// - Add Symbol property to the SymbolKeySignatureHelpItem class:
//1. Open Features\Core\Portable\SignatureHelp\AbstractSignatureHelpProvider.SymbolKeySignatureHelpItem.cs.
//2. Add property: internal ISymbol? Symbol { get; } //au
//3. In ctor add:  Symbol = symbol; //au

// - Let it don't try to load VB assemblies, because then exception when debugging:
//In MefHostServices.cs, in s_defaultAssemblyNames init list, remove the 2 VB assemblies.

// - In project Microsoft.CodeAnalysis, in MetadataReader\PEAssembly.cs, replace GetInternalsVisibleToPublicKeys with:

        internal IEnumerable<ImmutableArray<byte>> GetInternalsVisibleToPublicKeys(string simpleName)
        {
            if (_lazyInternalsVisibleToMap == null)
                Interlocked.CompareExchange(ref _lazyInternalsVisibleToMap, BuildInternalsVisibleToMap(), null);

            List<ImmutableArray<byte>> result;

            _lazyInternalsVisibleToMap.TryGetValue(simpleName, out result);

            //au
            return result
                ?? GetAuInternalsVisible(this.Identity.Name, simpleName, false)
                ?? SpecializedCollections.EmptyEnumerable<ImmutableArray<byte>>();
        }

        //au
        internal static Func<string, string, bool, bool> AuInternalsVisible;
        static ImmutableArray<byte>[] s_aivIA;
        internal static ImmutableArray<byte>[] GetAuInternalsVisible(string thisName, string toName, bool source)
        {
            if (AuInternalsVisible?.Invoke(thisName, toName, source) ?? false)
                return s_aivIA ??= new ImmutableArray<byte>[1] { default };
            return null;
        }

// - In project Microsoft.CodeAnalysis.CSharp, in Symbols\Source\SourceAssemblySymbol.cs, replace GetInternalsVisibleToPublicKeys with:

        //au
        internal override IEnumerable<ImmutableArray<byte>> GetInternalsVisibleToPublicKeys(string simpleName)
        {
            EnsureAttributesAreBound();

            if (_lazyInternalsVisibleToMap != null && _lazyInternalsVisibleToMap.TryGetValue(simpleName, out var result))
                return result.Keys;

            return PEAssembly.GetAuInternalsVisible(this.Name, simpleName, true)
                ?? SpecializedCollections.EmptyEnumerable<ImmutableArray<byte>>();
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
#endif
