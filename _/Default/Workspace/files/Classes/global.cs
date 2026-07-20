/// Class file "global.cs" is compiled with every script etc, like with /*/ c global.cs /*/.
/// Where don't need it, define NO_GLOBAL in meta comments: /*/ define NO_GLOBAL; /*/.
/// You can edit this file:
/// 	Add/remove global usings, classes, attributes.
/// 	Add more global class files and library references: /*/ c \file.cs; r Lib.dll; /*/.
/// Note: editing this file affects all C# code files, not only files created afterwards.

#if !NO_GLOBAL

global using Au;
global using Au.Types;
global using System;
global using System.Collections.Generic;

global using System.Linq;
global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.Globalization;
global using System.IO;
global using System.IO.Compression;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Text.RegularExpressions;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.Win32;
global using Au.More;


//type aliases
//global using Alias1 = Namespace1.Type1;

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
