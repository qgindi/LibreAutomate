/// Sometimes you want to save values of some variables used in a script or class, and next time use the saved values. For it you can use the registry, but it is a slow and awkward way. Usually it's better to use a file.
/// The fastest possible way is to put the variables in a record class derived from <see cref="JSettings"/>. They will be automatically saved in a JSON file. Run this script two times to see how it works.

MySettings sett = MySettings.Load(); //in a class you would use a static field or property, but this example uses a local variable for simplicity

print.it(sett.i);
sett.i++;

if (dialog.showInput(out string s, "example", editText: sett.s)) {
	sett.s = s;
}


record MySettings : JSettings { //you may want to change the name (MySettings)
	public static readonly string File = folders.ThisAppDocuments + @"MySettings.json"; //change this

	public static MySettings Load() => Load<MySettings>(File);

	// examples of settings
	public int i;
	public string s = "default";
	
	//Can be used most types, not only int and string. For example bool, DateTime, array, Dictionary. More examples in the JSettings webpage.
}
