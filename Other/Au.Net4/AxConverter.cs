using System;
using System.Windows.Forms.Design;

class AxConverter {
	public static int Convert(string activeX, string axFilename) {
		try {
			var x = new AxImporter(new() {
				outputDirectory = Environment.CurrentDirectory,
				outputName = axFilename
			});

			var s = x.GenerateFromFile(new(activeX));
			Console.WriteLine(s);
		}
		catch (Exception ex) {
			Console.WriteLine(ex);
			return -2;
		}

		return 0;
	}
}
