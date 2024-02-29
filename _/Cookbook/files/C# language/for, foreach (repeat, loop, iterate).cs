/// The <b><+lang iteration statements, "for">for<><> statement repeatedly executes statements.

for (int i = 0; i < 3; i++) { //increment i: 0, 1, 2
	print.it(i);
}

/// The above code declares variable <i>i<> = 0. Then, while <i>i<> is less than 3, executes statements in the <.c>{ }<> block and increments <i>i<> (<.c>i++<>).
/// 
/// Several ways to insert <.k>for<> code quickly:
/// - Type text like <.c>3t<> (it means "repeat 3 times").
/// - Type <.c>forr<> and select <b>forRepeatLoopSnippet<>.
/// - Click or select code, and use menu <b>Edit > Surround<>.

for (int i = 3; --i >= 0; ) { //decrement i: 2, 1, 0
	print.it(i);
}

for (;;) { //repeat forever or until break/return/goto/throw
	500.ms(); //wait 500 ms
	print.it(1);
	if (keys.isCtrl) break; //exit the for block
	if (keys.isShift) continue; //skip remaining statements
	print.it(2);
}

for (int i = 0, j = 0; i < 5 && !keys.isShift; i++, j+=10) {
	print.it(i, j);
	500.ms();
}

//find space or tab in string
string s = "Abc def";
int k = 0;
for (; k < s.Length; k++) if (s[k] is ' ' or '\t') break;
print.it(k);

/// The <.k>foreach<> statement executes statements for each item in a collection (array, list, etc).

var a = new int[] { 3, 5, 10 }; //array
foreach (var v in a) {
	print.it(v);
}

/// The <.k>while<> statement is similar to <.k>for<>.

while (!keys.isCtrl) { //same as for (; !keys.isCtrl; )
	print.it("waiting for Ctrl");
	100.ms();
}
