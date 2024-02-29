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
global using Win32Exception = System.ComponentModel.Win32Exception;
global using EditorBrowsableAttribute = System.ComponentModel.EditorBrowsableAttribute;
global using EditorBrowsableState = System.ComponentModel.EditorBrowsableState;
global using CancelEventArgs = System.ComponentModel.CancelEventArgs;
global using IEnumerable = System.Collections.IEnumerable;
global using IEnumerator = System.Collections.IEnumerator;

[module: DefaultCharSet(CharSet.Unicode)]

[assembly: AssemblyCompany("Gintaras Didžgalvis")]
[assembly: AssemblyProduct("LibreAutomate C#")]
[assembly: AssemblyCopyright("Copyright 2020-2024 Gintaras Didžgalvis")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("1.1.5")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
