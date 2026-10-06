//var a=new List<string>();
print.clear();
var b = new StringBuilder();
var s1 = File.ReadAllText(folders.Editor + @"..\Au\Api\Api_const.cs");
foreach (var m in s1.RxFindAll(@"(?m)^\h*internal const int (WM_\w+) *= *(\w+);")) {
	var s = m[1].Value;
	if (s.Ends("FIRST") || s.Ends("LAST") || s.Starts("WM_PSD_") || s.Starts("WM_DDE_") || s.Starts("WM_CHOOSEFONT_") || s == "WM_WININICHANGE") {
		//print.it(s);
		continue;
	}
	//print.it(s, m[2]);
	//a.Add(s);
	b.AppendFormat("0x{0:X} => \"{1}\",\r\n", m[2].Value.ToInt(), s);
}
b.Append("_ => null\r\n");
//a.Sort();
//print.it(a);
var s2 = b.ToString();
print.it(s2);
clipboard.text = s2;
