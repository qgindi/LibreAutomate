/// Class file "global.cs" is compiled with every script etc, like with /*/ c global.cs /*/.
/// Where don't need it, define NO_GLOBAL in meta comments: /*/ define NO_GLOBAL; /*/.
/// You can edit this file:
/// 	Add/remove global usings, classes, attributes.
/// 	Add more global class files and library references: /*/ c \file.cs; r Lib.dll; /*/.
/// 	Edit completion list filters (read more below).
/// Note: editing this file affects all C# code files, not only files created afterwards.

#if !NO_GLOBAL

global using Au; //automation
global using Au.Types; //types of parameters etc
global using System; //.NET types used everywhere
global using System.Collections.Generic; //List, Dictionary and other collections

//less important namespaces
global using System.Linq; //extension methods for collections
global using System.Collections.Concurrent; //thread-safe collections
global using System.Diagnostics; //debug [+~ ConditionalAttribute Debug Debugger EventLog FileVersionInfo Process ProcessStartInfo StackFrame StackTrace Trace]
global using System.Globalization; //[+~ CultureInfo Number* StringInfo UnicodeCategory]
global using System.IO; //file, directory
global using System.IO.Compression; //zip
global using System.Runtime.CompilerServices; //[-~ Caller* ConditionalWeakTable InternalsVisibleToAttribute MethodImpl* ModuleInitializerAttribute SkipLocalsInitAttribute Unsafe - *]
global using System.Runtime.InteropServices; //types for Windows API etc [-~]
global using System.Text; //[+~ Encoding Rune StringBuilder]
global using System.Text.RegularExpressions; //[+ Regex*]
global using System.Threading; //threads, synchronization [-~]
global using System.Threading.Tasks; //thread pool [+~ Task]
global using Microsoft.Win32; //[+~ Registry* SystemEvents]
global using Au.More; //rarely used in automation scripts [-~]

//The //[comments] are completion list filters. Filters are used to hide or descend some types in completion lists that contain all types.
// 	Examples:	//[- Hide These Types], //[+ Hide Other Types], //[-~ Descend These Types], //[+~ Descend Other Types],
// 				//[- Start* *End], //[-] (hide all), //[-~ Descend These - Hide These], //[-~ Descend These - *].
//	What is "descend": move to the bottom, gray text, low-priority auto-select.
//	Filters can be specified for "global using" directives in any code files included in a compilation. Also for "using" directives in current file.
//	If a directive appears twice (eg in global.cs and in current file), is used the last filter; if no filter, previous filter is discarded.
//	A directive can have single filter and it must be at the end of //comments in that line.

//type aliases

//usings for class examples
//global using my;
////global using static my.Example;

#endif

//attributes
#if !NO_DEFAULT_CHARSET_UNICODE
[module: System.Runtime.InteropServices.DefaultCharSet(System.Runtime.InteropServices.CharSet.Unicode)]
#endif

//class examples

//#if !NO_GLOBAL
//namespace my;
///// <summary>
///// Example.
///// </summary>
//static class Example {
//	/// <summary>
//	/// Example.
//	/// </summary>
//	/// <param name="a"></param>
//	/// <param name="b"></param>
//	/// <returns></returns>
//	/// <example>
//	/// <code><![CDATA[
//	/// print.it(Example.Add(2, 7));
//	/// print.it(Add(2, 7)); //if uncommented the 'global using static my.Example;'
//	/// ]]></code>
//	/// </example>
//	public static int Add(int a, int b) {
//		return a + b;
//	}
//}
//#endif
