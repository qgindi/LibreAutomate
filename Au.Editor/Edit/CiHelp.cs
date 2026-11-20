extern alias CAW;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;

namespace LA;

static class CiHelp {
	public static void OpenSymbolEtcFromPosHelp() {
		string url = null;
		if (!CodeInfo.GetContextAndDocument(out var cd)) return;
		var (sym, keyword, helpKind, _) = CiUtil.GetSymbolEtcFromPos(cd, forHelp: true);
		if (sym != null) {
			url = GetSymbolHelpUrl(sym, out bool isAu);
			if (isAu) {
				HelpUtil.AuHelp(url);
				return;
			}
		} else if (keyword != null) {
			var s = helpKind switch {
				HelpKind.PreprocKeyword => "preprocessor directive",
				HelpKind.AttributeTarget => "attributes, ",
				_ => "keyword"
			};
			s = $"C# {s} \"{keyword}\"";
			//print.it(s); return;
			url = CiUtil.GoogleURL(s);
		} else if (helpKind == HelpKind.String) {
			int i = popupMenu.showSimple("1 C# strings|2 String formatting|3 Wildcard expression|4 Output tags|11 Regex tool (Ctrl+Space)|12 Keys tool (Ctrl+Space)", PMFlags.ByCaret);
			switch (i) {
			case 1: url = "C# strings"; break;
			case 2: url = "C# string formatting"; break;
			case 3: HelpUtil.AuHelp("articles/Wildcard expression"); break;
			case 4: HelpUtil.AuHelp("articles/Output tags"); break;
			case 11: CiTools.CmdShowRegexWindow(); break;
			case 12: CiTools.CmdShowKeysWindow(); break;
			}
			if (url != null) url = CiUtil.GoogleURL(url);
		}
		if (url != null) run.itSafe(url);
	}
	
	/// <param name="isAu">It's an Au symbol. Returns not URL but just HTML filename like <c>"Au.Something"</c> or <c>"Au.Types.Generic-1"</c>. Call <see cref="HelpUtil.AuHelp"/>. Another way to detect it - <see cref="HelpUtil.IsAuHelp_"/>.</param>
	public static string GetSymbolHelpUrl(ISymbol sym, out bool isAu) {
		isAu = false;
		//print.it(sym);
		//print.it(sym.IsInSource(), sym.IsFromSource());
		if (sym is IParameterSymbol or ITypeParameterSymbol) return null;
		string query;
		IModuleSymbol metadata = null;
		foreach (var loc in sym.Locations) {
			if ((metadata = loc.MetadataModule) != null) break;
		}
		
		isAu = metadata?.Name == "Au.dll";
		if (isAu && !sym.HasPublicResultantVisibility()) metadata = null; //no online doc for other than public, protected, protected internal. But the google search is useful eg if it's an Api class member visible because of meta testInternal.
		
		if (metadata != null) {
			if (isAu && sym.IsEnumMember()) sym = sym.ContainingType;
			//print.it(sym, sym.GetType(), sym.GetType().GetInterfaces());
			if (sym is INamedTypeSymbol nt && nt.IsGenericType) {
				var qn = sym.QualifiedName(noDirectName: true);
				if (isAu) query = qn + "." + sym.MetadataName.Replace('`', '-');
				else query = $"{qn}.{sym.Name}<{string.Join(", ", nt.TypeParameters)}>";
			} else {
				query = sym.QualifiedName();
			}
			
			if (query.Ends("..ctor")) query = query.ReplaceAt(^6.., isAu ? ".-ctor" : " constructor");
			else if (query.Ends(".this[]")) query = query.ReplaceAt(^7.., ".Item");
			
			if (isAu) return query;
			if (metadata.Name.Starts("Au.")) return null;
			
			string kind = (sym is INamedTypeSymbol ints) ? ints.TypeKind.ToString() : sym.Kind.ToString();
			query = query + " " + kind.Lower();
		} else if (!sym.IsInSource() && !isAu) { //eg an operator of string etc
			if (!(sym is IMethodSymbol me && me.MethodKind == MethodKind.BuiltinOperator)) return null;
			//print.it(sym, sym.Kind, sym.QualifiedName());
			//query = "C# " + sym.ToString(); //eg "string.operator +(string, string)", and Google finds just Equality
			//query = "C# " + sym.QualifiedName(); //eg "System.String.op_Addition", and Google finds nothing
			query = "C# " + sym.ToString().RxReplace(@"\(.+\)$", "", 1).Replace('.', ' '); //eg C# string operator +, not bad
		} else if (sym.IsExtern) { //[DllImport]
			query = _GetDllImportName(sym) + " function";
		} else if (sym is INamedTypeSymbol nt1 && nt1.IsComImport) { //[ComImport] interface or coclass
			query = sym.Name + " " + nt1.TypeKind.ToString().Lower();
		} else if (sym.ContainingType?.IsComImport == true) { //[ComImport] interface method
			query = sym.ContainingType.Name + "." + sym.Name;
		} else if (_IsNativeApiClass(sym.ContainingType)) {
			if (sym is INamedTypeSymbol nt2) query = sym.Name + " " + (nt2.TypeKind switch { TypeKind.Struct => "structure", TypeKind.Enum => "enumeration", TypeKind.Delegate => "callback function", _ => null });
			else query = sym.Name; //constant or Guid/etc
		} else if (sym is IFieldSymbol && _IsNativeApiClass(sym.ContainingType.ContainingType)) {
			query = sym.ContainingType.Name + "." + sym.Name; //struct field or enum member
		} else {
			return null;
		}
		
		return CiUtil.GoogleURL(query);
		
		static bool _IsNativeApiClass(INamedTypeSymbol t)
			=> t?.TypeKind is TypeKind.Class or TypeKind.Struct
			&& (t.BaseType?.Name == "NativeApi" || t.Name.Contains("Native") || t.Name.Ends("Api"));
		
		static string _GetDllImportName(ISymbol sym) {
			AttributeData ad = sym.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "DllImportAttribute");
			string s = ad?.NamedArguments.FirstOrDefault(n => n.Key == "EntryPoint").Value.Value as string;
			return s ?? sym.Name;
		}
	}
	
}
