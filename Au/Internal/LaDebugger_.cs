namespace Au.More;

class LaDebugger_ {
	//Called by netcoredbg as a breakpoint condition when specified Message.
	static bool Logpoint(bool condition, string s, string link) {
		if (condition) print.it($"<><lc #f8f8d0><open {link}><c #40B000>♦<><> {s}<>");
		return false;
	}
	
	//Same problems as with LaDebugger_<T>. Also fails if struct.
	//static string Print<T>(T t) {
	//	var s = print.util.toString(t).Limit(1_000_000);
	//	return Convert.ToBase64String(Encoding.UTF8.GetBytes(s)); 
	//}
}

//Functions called by netcoredbg -var-create to get a value in the print.it format.
//netcoredbg does not support cast to object etc. This is the only way that works.
//Fails if array.
//Fails if ref struct.
//Garbage if object or dynamic.
class LaDebugger_<T> {
	static string Print(T t) => _Print(t, false);
	static string PrintCompact(T t) => _Print(t, true);
	
	static string _Print(T t, bool compact) {
		var s = print.util.toString(t, compact).Limit(1_000_000);
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
	}
	
	//static string PrintArray(T[] a) => "OK"; //fails too
}
