/// Library info: <link https://github.com/xoofx/markdig>Markdig<>. NuGet: <+nuget>Markdig<>.

/*/ nuget -\Markdig; /*/

var md = """
## Header
Text.
""";
var s = Markdig.Markdown.ToHtml(md);
print.it(s);
