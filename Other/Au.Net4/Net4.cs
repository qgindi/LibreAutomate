using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

using _TYPELIBATTR = System.Runtime.InteropServices.ComTypes.TYPELIBATTR;

[module: DefaultCharSet(CharSet.Unicode)]

unsafe class Net4 {
	[STAThread]
	static int Main(string[] args) {
		Console.InputEncoding = Encoding.UTF8;
		Console.OutputEncoding = Encoding.UTF8;

		switch (args[0]) {
		case "/typelib":
			return ConvertTypelib(args[1]);
		}
		return 1;
	}

	static int ConvertTypelib(string param) {
		var a = param.Split('|');
		string asmDir = a[0], comDll = a[1];

		int hr = LoadTypeLibEx(comDll, 2, out var tl);
		if (hr != 0) {
			Print($"Failed to load type library '{comDll}'.\r\n\t{new Win32Exception(hr).Message}");
			return -1;
		}

		try {
			var c = new _TypelibConverter { saveDir = asmDir };
			c.Convert(tl);
		}
		catch (Exception ex) { Print($"Failed to convert type library '{comDll}'.\r\n\t{ex.Message}"); return -2; }
		return 0;
	}

	[DllImport("oleaut32.dll", EntryPoint = "#183", PreserveSig = true)]
	static extern int LoadTypeLibEx(string szFile, int regkind, out ITypeLib pptlib);

	class _TypelibConverter : ITypeLibImporterNotifySink {
		static Dictionary<string, AssemblyBuilder> s_converted = new Dictionary<string, AssemblyBuilder>();

		public string saveDir;

		public Assembly Convert(ITypeLib tl) {
			tl.GetLibAttr(out IntPtr ipta);
			var ta = *(_TYPELIBATTR*)ipta;
			tl.ReleaseTLibAttr(ipta);
			var hash = Fnv1((byte*)&ta, sizeof(_TYPELIBATTR) - 2).ToString("x");

			tl.GetDocumentation(-1, out var tlName, out var tlDescription, out var _, out var _);
			var fileName = $"{tlName} {ta.wMajorVerNum}.{ta.wMinorVerNum} #{hash}.dll";
			var netPath = saveDir + fileName;

			if (!s_converted.TryGetValue(fileName, out var asm) || !File.Exists(netPath)) {
				Print($"Converted: {tlName} ({tlDescription}) to \"{fileName}\".");

				var converter = new TypeLibConverter();
				asm = converter.ConvertTypeLibToAssembly(tl, netPath,
					TypeLibImporterFlags.ReflectionOnlyLoading | TypeLibImporterFlags.TransformDispRetVals | TypeLibImporterFlags.UnsafeInterfaces,
					this, null, null, tlName, null);
				asm.Save(fileName);
				s_converted[fileName] = asm;
			}
			return asm;
		}

		void ITypeLibImporterNotifySink.ReportEvent(ImporterEventKind eventKind, int eventCode, string eventMsg) {
			if (eventKind != ImporterEventKind.NOTIF_TYPECONVERTED) Print("Warning: " + eventMsg);
		}

		Assembly ITypeLibImporterNotifySink.ResolveRef(object typeLib) => Convert(typeLib as ITypeLib);
	}

	static void Print(object o) => Console.WriteLine(o?.ToString());

	//code copied from Hash
	static int Fnv1(byte* data, int lengthBytes) {
		uint hash = 2166136261;

		for (int i = 0; i < lengthBytes; i++)
			hash = (hash * 16777619) ^ data[i];

		return (int)hash;
	}

}
