/// Use class <see cref="XElement"/> and namespaces <see cref="System.Xml.Linq"/> and <see cref="System.Xml.XPath"/>.
/// This recipe contains just some basic ingredients; look for more info and tutorials on the internet.

using System.Xml.Linq;
using System.Xml.XPath;

/// XML string example.

var s = """
<root>
	<q>aaa</q>
	<e a="nnn">bbb</e>
	<e a="mmm" b="kkk"/>
	<f>
		<g>ggg</g>
	</f>
	<z>zzz</z>
</root>
""";

/// Load XML string or file.

XElement x = XElement.Parse(s); //load from string
//XElement x = XElement.Load(@"C:\Test\test.xml"); //load from file

/// Enumerate direct child elements.

foreach (var v in x.Elements()) { //or x.Elements("name")
	print.it(v);
}

/// Get a direct child element by name. Get its text.

var e1 = x.Element("q");
print.it(e1, e1.Value);

/// Get a direct child element by name and attribute. Get its another attribute.

var e2 = x.Elem("e", "a", "mmm");
print.it(e2, e2.Attr("b"));

/// Get elements using XPath.

var e3 = x.XPathSelectElement("/f/g");
print.it(e3);
foreach (var v in x.XPathSelectElements("e")) print.it(v);

/// Another way to get descendant elements is <google>LINQ to XML queries in C#<>.

/// Add element with value.

x.Add(new XElement("new", "value"));

/// Add element with 2 attributes and value.

x.Add(new XElement("new",
	new XAttribute("a1", "uuu"),
	new XAttribute("a2", "vvv"),
	"value"
	));

/// More examples and info: <google>Create XML trees in C#<>.

/// Add, change or remove an attribute.

e1.SetAttributeValue("r", "rrr"); //add or change
//e1.SetAttributeValue("r", null); //remove

/// Remove element if exists.

x.Element("z")?.Remove();

/// Convert to CSV string or save in file.

var s2 = x.ToString();
print.it(s2);
//x.Save(@"C:\Test\test.xml");
