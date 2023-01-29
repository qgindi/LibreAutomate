# JSON

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
```

JSON is often used to serialize (convert to string) and deserialize (convert from string) various objects (instances of classes and structs). Let's create an object. The class is at the end of this recipe.

```csharp
var x = new Example {
	Property = 100,
	p = new(10, 20),
	a = new string[] { "one", "two" },
};
```

Will need a <a href='https://www.google.com/search?q=System.Text.Json.JsonSerializerOptions+class'>JsonSerializerOptions</a>.
Important for speed: use the same options variable for all operations that use the same options.

```csharp
JsonSerializerOptions options = new () {
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	IncludeFields = true,
	IgnoreReadOnlyFields = true,
	IgnoreReadOnlyProperties = true,
	WriteIndented = true,
	Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	AllowTrailingCommas = true,
};
```

Serialize the object with <a href='https://www.google.com/search?q=System.Text.Json.JsonSerializer+class'>JsonSerializer</a>.

```csharp
string s = JsonSerializer.Serialize<Example>(x, options); //convert to JSON string
print.it(s);
//File.WriteAllBytes(@"C:\Test\test.json", JsonSerializer.SerializeToUtf8Bytes<Example>(v, options)); //save in file
```

Deserialize.

```csharp
var y = JsonSerializer.Deserialize<Example>(s, options); //convert from JSON string
print.it(y);
//var y = JsonSerializer.Deserialize<Example>( //load file
//	File.ReadAllBytes(@"C:\Test\test.json"),
//	options);
```

To get values from a JSON string without converting it to an object, use class <a href='https://www.google.com/search?q=System.Text.Json.Nodes.JsonNode+class'>JsonNode</a>. See recipe <a href='Http post web form, JSON.md'>Http POST</a>. The class also can be used to create a JSON string. Look for more info on the internet.

```csharp
var j = JsonNode.Parse(File.ReadAllBytes(@"C:\Test\httpPost.json"));
print.it(j["headers"]["Host"]);
foreach (var v in j.AsObject()) {
	print.it(v);
}
```

JSON is a good format for various settings. The <a href='/api/Au.Types.JSettings.html'>JSettings</a> class uses it. See recipe <a href='Saving variables, settings.md'>saving variables</a>.

An example class.

```csharp
record class Example {
	public int Property { get; set; }
	public string field = "text";
	public POINT p;
	public string[] a;
	
	[JsonIgnore]
	public int excludedExplicitly = 1;
	int _excludedBecausePrivate = 2;
}
```

