//This file is used by several projects: Au, Au.Controls, Au.Editor.

global using Au;
global using Au.Types;
global using Au.More;
global using System;
global using System.Collections.Generic;
global using System.Collections.Concurrent;
global using System.Linq;
global using System.Text;
global using System.Diagnostics;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.IO;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Reflection;
global using System.Globalization;

global using SystemInformation = System.Windows.Forms.SystemInformation;
global using RStr = System.ReadOnlySpan<char>;
global using RByte = System.ReadOnlySpan<byte>;
global using System.ComponentModel;
global using IEnumerable = System.Collections.IEnumerable;
global using IEnumerator = System.Collections.IEnumerator;

[module: DefaultCharSet(CharSet.Unicode)]
[assembly: ComVisible(false)]

[assembly: AssemblyCompany("Gintaras Didžgalvis")]
[assembly: AssemblyProduct("LibreAutomate")]
[assembly: AssemblyCopyright("Copyright 2020-2025 Gintaras Didžgalvis")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion(Au_.Version)]

#if AU
namespace Au.More;

///
[EditorBrowsable(EditorBrowsableState.Never)]
public class Au_ {
	///
	public const string Version = "1.13.1";
}
#endif
