//This file is used by several projects: Au, Au.Controls, Au.Editor.

#if !NO_GLOBAL
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

global using RStr = System.ReadOnlySpan<char>;
global using Win32Exception = System.ComponentModel.Win32Exception;
global using EditorBrowsableAttribute = System.ComponentModel.EditorBrowsableAttribute;
global using EditorBrowsableState = System.ComponentModel.EditorBrowsableState;
global using InvalidEnumArgumentException = System.ComponentModel.InvalidEnumArgumentException;
global using CancelEventHandler = System.ComponentModel.CancelEventHandler;
global using CancelEventArgs = System.ComponentModel.CancelEventArgs;
global using IEnumerable = System.Collections.IEnumerable;
global using IEnumerator = System.Collections.IEnumerator;
#else
using System.Reflection;
using System.Runtime.InteropServices;
#endif

[module: DefaultCharSet(CharSet.Unicode)]

[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("C# Uiscripter")]
[assembly: AssemblyCopyright("Copyright 2022 Gintaras Didžgalvis")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("0.6.0")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
