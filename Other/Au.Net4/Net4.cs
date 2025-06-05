using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

[module: DefaultCharSet(CharSet.Unicode)]

class Net4 {
	[STAThread]
	static int Main(string[] args) {
		Console.InputEncoding = Encoding.UTF8;
		Console.OutputEncoding = Encoding.UTF8;

		switch (args[0]) {
		case "/typelib":
			return TypelibConverter.Convert(args[1]);
		}
		return 1;
	}
}
