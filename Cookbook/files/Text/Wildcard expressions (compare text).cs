/// Function <see cref="Au.Types.ExtString.Like">string.Like<> compares strings using wildcard characters `*` and `?`.

string s = "file.txt";
if (s.Like("*.txt")) print.it("s ends with .txt");

int i = s.Like(true, "*.xml", "*.json", "*.txt");
print.it(i);

/// Many "find object" functions support <help articles/Wildcard expression>wildcard expressions<>.

var w1 = wnd.find("*- Notepad");
print.it(w1);

var w2 = wnd.find("**r (Edge|Chrome)$"); //regular expression
print.it(w2);

var w3 = wnd.find("**m *Edge||*Chrome"); //ends with Edge or Chrome
print.it(w3);

/// To create such functions use class <see cref="wildex"/>.

int Find(string[] names, string name) {
	wildex wild = name;
	for (int i = 0; i < names.Length; i++) {
		if (wild.Match(names[i])) return i + 1;
	}
	return 0;
}

string[] a = { "file1.bmp", "file2.gif", "file3.png" };
int j = Find(a, "*.png");
print.it(j);
