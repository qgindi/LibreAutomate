/// In scripts can be used everything from .NET libraries. Don't need to add assembly references. Add just <.k>using<> directives that aren't in file <.c>global.cs<>.

using System.Xml.Linq;

var x = XElement.Load(@"C:\Test\file.xml");

/// Many other managed libraries can be downloaded, for example from <google>NuGet<> or <google GitHub .NET libraries>GitHub<>. To install NuGet packages, use menu <b>Tools > NuGet<>. For it at first need to install .NET SDK.
///
/// NuGet packages also can be used without installing SDK, but then installing them isn't so easy. Need to download and extract packages and their dependencies. NuGet packages are <_>zip</_> files with file extension <_>.nupkg</_>. Usually need just the <_>dll</_> file, and <_>xml</_> file if exists. In <_>nupkg</_> files usually they are in <.c>/lib/netX<> or <.c>/lib/netstandardX<> or <.c>/runtimes/win/lib/netX<>. Extract them for example to a subfolder of the editor's folder, or to the <.c>dll<> subfolder of the workspace folder, or anywhere.
///
/// When a NuGet package or some other library is installed, need to add its reference to scripts where you want to use it. You can do it in the <b>NuGet<> dialog or in the <b>Properties<> dialog. Then you'll find one or more new namespaces in the <mono>Ctrl+Space<> list, and can use them.

/*/ nuget -\Humanizer; /*/
using Humanizer;

var s = "one two";
s = s.Titleize();
print.it(s);

/// If a library has several versions for different .NET frameworks, use the newest .NET version, if available, else .NETStandard. If these are unavailable, probably the library is abandoned and should not be used. Don't use libraries for .NET Framework 4.x and older.
