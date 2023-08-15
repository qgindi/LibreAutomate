using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;

/// JSON is often used to serialize (convert to string) and deserialize (convert from string) various objects (instances of classes and structs). Let's create an object. The class is at the end of this recipe.

var x = new Example {
	Property = 100,
	p = new(10, 20),
	a = new string[] { "one", "two" },
};

/// Will need a <see cref="JsonSerializerOptions"/>.
/// Important for speed: use the same options variable for all operations that use the same options.

JsonSerializerOptions options = new () {
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	IncludeFields = true,
	IgnoreReadOnlyFields = true,
	IgnoreReadOnlyProperties = true,
	WriteIndented = true,
	Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	AllowTrailingCommas = true,
};

/// Serialize the object with <see cref="JsonSerializer"/>.

string s = JsonSerializer.Serialize<Example>(x, options); //convert to JSON string
print.it(s);
//File.WriteAllBytes(@"C:\Test\test.json", JsonSerializer.SerializeToUtf8Bytes<Example>(v, options)); //save in file

/// Deserialize.

var y = JsonSerializer.Deserialize<Example>(s, options); //convert from JSON string
print.it(y);
//var y = JsonSerializer.Deserialize<Example>( //load file
//	File.ReadAllBytes(@"C:\Test\test.json"),
//	options);

/// To get values from a JSON string without converting it to an object, use class <see cref="JsonNode"/>. See recipe <+recipe>Http POST<>. The class also can be used to create a JSON string. Look for more info on the internet.

var j = JsonNode.Parse(File.ReadAllBytes(@"C:\Test\httpPost.json"));
print.it(j["headers"]["Host"]);
foreach (var v in j.AsObject()) {
	print.it(v);
}

/// JSON is a good format for various settings. The <see cref="JSettings"/> class uses it. See recipe <+recipe>saving variables<>.

/// An example class.

record class Example {
	public int Property { get; set; }
	public string field = "text";
	public POINT p;
	public string[] a;
	
	[JsonIgnore]
	public int excludedExplicitly = 1;
	int _excludedBecausePrivate = 2;
}
