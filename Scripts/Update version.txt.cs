/*/ role editorExtension; c Sftp.cs; /*/
using System.Runtime.Loader;

var ver = typeof(osVersion).Assembly.GetName().Version.ToString(3);
var ver2 = AssemblyLoadContext.Default.Assemblies.First(o => o.GetName().Name == "Au.Editor").GetName().Version.ToString(3);
if (ver2 != ver) throw new InvalidOperationException("editor project not compiled");

var file = folders.ThisAppBS + "version.txt";
filesystem.saveText(file, ver);
Sftp.UploadToLA("domains/libreautomate.com/public_html", file);

print.it("Uploaded: version.txt", ver);
