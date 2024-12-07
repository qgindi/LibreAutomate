/// Library info: <link https://html-agility-pack.net/>HtmlAgilityPack<>. NuGet: <+nuget>HtmlAgilityPack<>.

/*/ nuget -\HtmlAgilityPack; /*/
using HtmlAgilityPack;

print.clear();

/// Parse a HTML string or file.

var html = """
<html>
<body>
 <p>Text</p>
 <a id="example" href="https://www.example1.com">Link1</a>
 <ul>
  <li><a href="https://www.example2.com">Link2</a></li>
  <li><a href="https://www.example3.com">Link3</a></li>
 </ul>
</body>
</html>
""";

var doc1 = new HtmlDocument();
doc1.LoadHtml(html); //load from string
//doc1.Load(@"C:\Test\test.xml"); //load from file

var p = doc1.GetElementbyId("example"); 
print.it(p.OuterHtml);

print.it("All links:");
foreach (var link in doc1.DocumentNode.Descendants("a")) {
	print.it(link.InnerText, link.GetAttributeValue("href", null));
}

print.it("Select elements using XPath:");
var a = doc1.DocumentNode.SelectNodes("//body/ul/li/a");
if (a != null) {
	foreach (var link in a) {
		print.it(link.InnerText, link.GetAttributeValue("href", null));
	}
}

/// Download a web page. Get its title and text.

var web = new HtmlWeb();
var doc2 = web.Load("https://www.example.com");
var title = doc2.DocumentNode.SelectSingleNode("//head/title").InnerText;
print.it("Title:");
print.it(title);
var text = doc2.DocumentNode.SelectSingleNode("//body").InnerText;
print.it("Text:");
print.it(HtmlEntity.DeEntitize(text));

/// More info and examples in the <link https://html-agility-pack.net/>HtmlAgilityPack<> website.

/// Get web page HTML from web browser window. Then get all links.

var w = wnd.find(1, "*- Google Chrome", "Chrome_WidgetWin_1");
var e = w.Elm["web:DOCUMENT"].Find(30);
//w.Elm["web:LINK", "Example"].Find(30); //wait until the web page is loaded and displays an element (link "Example")
var html2 = e.Html(true);
//print.it(html2);
var doc3 = new HtmlDocument();
doc3.LoadHtml(html2);
var body = doc3.DocumentNode;
foreach (var link in body.Descendants("a")) {
	print.it(link.InnerText, link.GetAttributeValue("href", null));
}

/// See also recipe <+recipe>Web browser automation with Playwright<>.
