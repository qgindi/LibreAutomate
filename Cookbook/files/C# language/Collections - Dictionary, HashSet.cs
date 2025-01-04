/// <google C# 'Dictionary T class'>Dictionary<> is a collection of key-value pairs where values can be easily found by key in a fast way.

var d1 = new Dictionary<string, int>(); //keys of type string, values of type int
d1.Add("one", 1);
d1.Add("two", 2);
//d1.Add("one", 3); //throws exception if the key already exists
if (!d1.TryAdd("one", 3)) print.it("failed to add"); //adds if the key does not exist
d1["one"] = 3; //adds or replaces

print.it(d1["one"], d1["two"], d1.Count); //get some values and the count of elements
//var v1 = d1["three"]; //throws exception, because key "three" does not exist
if (d1.TryGetValue("three", out var v1)) print.it(v1); //if key "three" exists, get its value
if (d1.ContainsKey("two")) print.it("contains"); //can be used when don't need the value

if (!d1.Remove("three")) print.it("OK, but the key did not exist"); //remove if exists
if (d1.Remove("two", out var v2)) print.it(v2); //remove if exists and get value
d1.Clear(); //remove all elements

//string-int dictionary with case-insensitive keys and 3 elements
var d2 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) {
	{ "January", 1 }, //same as d2.Add("January", 1);
	{ "February", 2 },
	{ "March", 3 },
};
print.it(d2["march"]);

/// <google C# 'HashSet T class'>HashSet<> is like a dictionary but without values.
