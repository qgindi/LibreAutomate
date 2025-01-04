/// A script with errors can't run. Errors must be fixed.

print.it(1) //error, no semicolon
print.it(u); //error, u does not exist

/// Exceptions occur when something fails at run time. Then the script ends or jumps to a <.k>catch<> part of the nearest <.k>try-catch<> statement that embraces that code. <+lang try catch>More info<>.

print.it(1);
try {
	var w = wnd.find(0, "* - Notepadd");
	print.it(2);
}
catch {
	print.it("exception");
	return; //remove this, and the script will continue at print.it(3);
	//throw; //add this instead of return, and the script will end like without try/catch
	//throw new InvalidOperationException("message"); //or throw another exception
}
print.it(3);

/// Can get exception info.

try { run.it("notepad11.exe"); }
catch (Exception e) { print.it(e); }
print.it("continue");

/// Can differently handle exceptions of different types. Can use <.k>when<>.

string text = null;
try { text = File.ReadAllText(@"D:\file45678"); }
catch (FileNotFoundException) { } //continue
catch (Exception e) when (e is not ArgumentException) { //handle all other exceptions except ArgumentException
	print.it(e.ToStringWithoutStack());
	return;
}
print.it(text ?? "file not found");

/// Can use <.k>try-finally<> and <.k>try-catch-finally<>. <+lang try finally>More info<>. The <.k>finally<> code runs when the <.k>try<> code is leaved in whatever way (finished, exception, <.k>return<>, <.k>goto<>, etc).

print.it(1);
try {
	run.it("notepad22.exe");
	print.it(2);
	if (keys.isCapsLock) return;
}
//catch { print.it("exception"); } //optional. Try to uncomment this and observe the difference.
finally { print.it("finally"); }
print.it(3);

/// Note: The <.k>finally<> code does not run if <see cref="Environment.Exit"/> or some other function terminates the process.

/// With disposable objects instead of <_>try-finally-Dispose</_> can be used the <b><+lang using statement>using<><> statement.

/// To throw exceptions use the <b><+lang throw statement>throw<><> statement.

Func1(1);
Func1(-1);

void Func1(int i) {
	if (i < 0) throw new ArgumentException("Can't be negative", nameof(i));
	if (keys.isCapsLock) throw new InvalidOperationException("CapsLock");
	print.it(i);
}

/// Two ways to insert <.k>try<> code quickly:
/// - Type <.c>tryc<> and select <b>trySnippet<>.
/// - Click or select code, and use menu <b>Edit > Selection > Surround<>.
