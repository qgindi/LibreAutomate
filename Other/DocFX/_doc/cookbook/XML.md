# XML
Use class <a href='https://www.google.com/search?q=System.Xml.Linq.XElement+class'>XElement</a> and namespaces <a href='https://www.google.com/search?q=System.Xml.Linq+namespace'>System.Xml.Linq</a> and <a href='https://www.google.com/search?q=System.Xml.XPath+namespace'>System.Xml.XPath</a>.
This recipe contains just some basic ingredients; look for more info and tutorials on the internet.

```csharp
using System.Xml.Linq;
using System.Xml.XPath;
```

XML string example.

```csharp
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
```

Load XML string or file.

```csharp
XElement x = XElement.Parse(s); //load from string
//XElement x = XElement.Load(@"C:\Test\test.xml"); //load from file
```

Enumerate direct child elements.

```csharp
foreach (var v in x.Elements()) { //or x.Elements("name")
	print.it(v);
}
```

Get a direct child element by name. Get its text.

```csharp
var e1 = x.Element("q");
print.it(e1, e1.Value);
```

Get a direct child element by name and attribute. Get its another attribute.

```csharp
var e2 = x.Elem("e", "a", "mmm");
print.it(e2, e2.Attr("b"));
```

Get elements using XPath.

```csharp
var e3 = x.XPathSelectElement("/f/g");
print.it(e3);
foreach (var v in x.XPathSelectElements("e")) print.it(v);
```

Another way to get descendant elements is <a href='https://www.google.com/search?q=LINQ+to+XML+queries+in+C%23'>LINQ to XML queries in C#</a>.

Add element with value.

```csharp
x.Add(new XElement("new", "value"));
```

Add element with 2 attributes and value.

```csharp
x.Add(new XElement("new",
	new XAttribute("a1", "uuu"),
	new XAttribute("a2", "vvv"),
	"value"
	));
```

More examples and info: <a href='https://www.google.com/search?q=Create+XML+trees+in+C%23'>Create XML trees in C#</a>.

Add, change or remove an attribute.

```csharp
e1.SetAttributeValue("r", "rrr"); //add or change
//e1.SetAttributeValue("r", null); //remove
```

Remove element if exists.

```csharp
x.Element("z")?.Remove();
```

Convert to CSV string or save in file.

```csharp
var s2 = x.ToString();
print.it(s2);
//x.Save(@"C:\Test\test.xml");
```

