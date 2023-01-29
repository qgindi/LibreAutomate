/*/ role editorExtension; /*/

//compiler always copies the true Au.dll to outputPath. Let's replace it with our Au.dll.
var outputPath = args[0];
filesystem.copyTo(@"C:\code\ok\dll\Au.dll", outputPath, FIfExists.Delete);
