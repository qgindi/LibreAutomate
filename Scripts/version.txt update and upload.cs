/*/ role editorExtension; /*/

//var ver = osVersion.onaString;
//print.it(ver);

using System.Runtime.Loader;

var ver = typeof(osVersion).Assembly.GetName().Version.ToString(3);
var ver2 = AssemblyLoadContext.Default.Assemblies.First(o => o.GetName().Name == "Au.Editor").GetName().Version.ToString(3);
if (ver2 != ver) throw new InvalidOperationException("editor project not compiled");
print.it(ver2);

//var verOld = filesystem.loadText(folders.ThisAppBS + "version.txt");
//if (verOld == ver) { print.it("Already OK"); }

var file = folders.ThisAppBS + "version.txt";
filesystem.saveText(file, ver);
interne
