/// <google>LINQ<> adds many extension methods to arrays, lists, strings and other <+recipe>collections<>. This recipe shows how to use some of them.

//create a collection (array) to use with other examples
var a = new string[] { "One", "Two", "Three", "Four", "Five" };

/// Get the first matching item. The <+recipe>callback function<> decides what items match; it can compare any property/condition/etc. This example looks for a string that starts with <.c>"F"<>.

string s1 = a.FirstOrDefault(o => o.Starts("F"));
print.it(s1);

/// How it works: function <b>FirstOrDefault<> calls the callback function (lambda) for each item until it returns <.c>true<>. Then <b>FirstOrDefault<> returns that item.

/// Function <b>FirstOrDefault<> returns <.c>null<> if not found. Function <b>First<> throws exception instead. Function <b>Any<> returns <.c>true<>/<.c>false<>. Function <b>Count<> tells how many.

if (a.Any(o => o.Eqi("three"))) print.it("found"); else print.it("not found");
if (!a.Any()) print.it("the collection is empty");
int n1 = a.Count(), n2 = a.Count(o => o.Starts("F"));
print.it(n1, n2);

/// Function <b>Where<> returns all matching items.

foreach (var v in a.Where(o => o.Starts("F"))) {
	print.it(v);
}

/// Many functions return a lazy <b>IEnumerable<> object that may retrieve items later. Convert it to array or <b>List<> if need.

string[] a2 = a
	.Where(o => o.Starts("F"))
	.ToArray(); //or ToList

/// Remove duplicate or similar elements.

var ai = new List<int> { 3, 4, 3, 5, 5, 1, 2, 1 };
print.it("Dictinct", ai.Distinct());
print.it("DictinctBy", a.DistinctBy(o => o[0])); //get strings with unique first character

/// Remove specified elements.

print.it("Except", ai.Except(new int[] { 1, 5 }));

/// Reverse.

print.it("reverse array", a.Reverse());
print.it("reverse string", "abc".Reverse());

/// Compare two collections.

var c1 = new int[] { 5, 2, 9 };
var c2 = new int[] { 5, 2, 9 };
var c3 = new int[] { 5, 3, 9 };
print.it(c1.SequenceEqual(c2), c1.SequenceEqual(c3));

/// Get elements that have/inherit the specified type.

var ao = new object[] { 3, "blue", 6.2, "yellow" };
print.it("strings", ao.OfType<string>());

/// Sorting.

print.it("sort", a.OrderBy(o => o));
print.it("sort descending, case-insensitive", a.OrderByDescending(o => o, StringComparer.OrdinalIgnoreCase));
print.it("sort by property", a.OrderBy(o => o.Length));

var at = new (int i, string s)[] { (5, "five"), (8, "eight"), (2, "eight") }; //array of tuples
print.it("sort by member", at.OrderBy(o => o.s));
print.it("sort by two members", at.OrderBy(o => o.s).ThenBy(o => o.i));

/// Transform elements from one type to other type.

print.it("Select: get a property", a.Select(o => o.Length)); //create ints from strings
print.it("Select: create objects of other type", a.Select(o => (o.Length, o))); //create tuples from strings
