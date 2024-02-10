/// The <b><+lang jump statements, return>return<><> statement is used to exit current function.

Func1();
print.it(3);

void Func1() {
	print.it(1);
	if(keys.isShift) return;
	print.it(2);
}

/// If the function's return type is not <.k>void<>, the statement also returns a value.

print.it(Add(2, 2));

int Add(int a, int b) {
	return a + b;
}

/// If <.k>return<> is directly in the script and not in a function, the script will exit.

if(keys.isShift) return;

/// To exit the script from anywhere, can be used <see cref="Environment.Exit"/> or <see cref="script.end"/>. However it isn't a normal exit; for example <.k>finally<> blocks will not be executed. Consider to throw an exception instead.

Func2();

void Func2() {
	print.it(1);
	if(keys.isShift) Environment.Exit(1);
	if(keys.isCtrl) throw new InvalidOperationException("Ctrl pressed");
	print.it(2);
}

/// The <.k>goto<> statement is used to jump to a label in current function.

print.it(1);
if(keys.isShift) goto label1;
print.it(2);
label1:
print.it(3);

if (keys.isCtrl) {
	if(keys.isShift) goto g1;
	print.it(4);
	g1:; //need ; if the label is at the end of a { code block }
}
print.it(5);
