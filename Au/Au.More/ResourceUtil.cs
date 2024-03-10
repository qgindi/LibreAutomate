using System.Resources;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows;
using System.Windows.Controls;

namespace Au.More {
	/// <summary>
	/// Gets managed resources from a .NET assembly.
	/// </summary>
	/// <remarks>
	/// Internally uses <see cref="ResourceManager"/>. Uses <see cref="CultureInfo.InvariantCulture"/>.
	///
	/// Loads resources from managed resource <c>"AssemblyName.g.resources"</c>. To add such resource files in Visual Studio, set file build action = <b>Resource</b>. Don't use <c>.resx</c> files and the <b>Resources</b> page in <b>Project Properties</b>.
	///
	/// By default loads resources from the app entry assembly. In script with role <b>miniProgram</b> - from the script's assembly. To specify another loaded assembly, use prefix like <c>"&lt;AssemblyName&gt;"</c> or <c>"*&lt;AssemblyName&gt;"</c>.
	///
	/// The resource name argument can optionally start with <c>"resource:"</c>.
	///
	/// Does not use caching. Creates new object even when loading the resource not the first time.
	/// </remarks>
	public static class ResourceUtil {
		/// <summary>
		/// Gets resource of any type.
		/// </summary>
		/// <param name="name">Resource name, like <c>"file.txt"</c> or <c>"sub/file.txt"</c>. More info: <see cref="ResourceUtil"/>.</param>
		/// <exception cref="FileNotFoundException">Cannot find assembly or resource.</exception>
		/// <exception cref="InvalidOperationException">The resource is of different type. This function does not convert.</exception>
		/// <exception cref="Exception">Other exceptions that may be thrown by used .NET functions.</exception>
		public static T Get<T>(string name) {
			var o = _GetObject(ref name);
			if (o is T r) return r;
			throw new InvalidOperationException($"Resource '{name}' is not {typeof(T).Name}; it is {o.GetType().Name}.");
		}
		
		/// <summary>
		/// Gets stream.
		/// </summary>
		/// <param name="name">Resource name, like <c>"file.png"</c> or <c>"sub/file.png"</c>. More info: <see cref="ResourceUtil"/>.</param>
		/// <exception cref="FileNotFoundException">Cannot find assembly or resource.</exception>
		/// <exception cref="InvalidOperationException">The resource type is not stream.</exception>
		/// <exception cref="Exception">Other exceptions that may be thrown by used .NET functions.</exception>
		/// <remarks>
		/// Don't need to dispose the stream.
		/// </remarks>
		public static UnmanagedMemoryStream GetStream(string name) {
			//if (name.Starts("pack:")) return _Pack(name); //rejected
			return Get<UnmanagedMemoryStream>(name);
		}
		
		/// <summary>
		/// Gets string.
		/// </summary>
		/// <param name="name">Resource name, like <c>"myString"</c> or <c>"file.txt"</c> or <c>"sub/file.txt"</c>. More info: <see cref="ResourceUtil"/>.</param>
		/// <exception cref="FileNotFoundException">Cannot find assembly or resource.</exception>
		/// <exception cref="InvalidOperationException">Unsupported resource type.</exception>
		/// <exception cref="Exception">Other exceptions that may be thrown by used .NET functions.</exception>
		/// <remarks>
		/// Supports resources of type <b>string</b>, <b>byte[]</b> (UTF-8), stream (UTF-8).
		/// </remarks>
		public static string GetString(string name) {
			var o = _GetObject(ref name);
			switch (o) {
			case string s: return s;
			case byte[] a: return Encoding.UTF8.GetString(a);
			case UnmanagedMemoryStream m: return new StreamReader(m, Encoding.UTF8).ReadToEnd();
			}
			throw new InvalidOperationException($"Resource '{name}' is not string, byte[] or stream; it is {o.GetType().Name}.");
		}
		
		internal static string TryGetString_(string name) => _TryGetObject(ref name) as string;
		
		/// <summary>
		/// Gets <b>byte[]</b>.
		/// </summary>
		/// <param name="name">Resource name, like <c>"file.txt"</c> or <c>"sub/file.txt"</c>. More info: <see cref="ResourceUtil"/>.</param>
		/// <exception cref="FileNotFoundException">Cannot find assembly or resource.</exception>
		/// <exception cref="InvalidOperationException">Unsupported resource type.</exception>
		/// <exception cref="Exception">Other exceptions that may be thrown by used .NET functions.</exception>
		/// <remarks>
		/// Supports resources of type <b>byte[]</b>, <b>string</b> (gets UTF-8 bytes), stream.
		/// </remarks>
		public static byte[] GetBytes(string name) {
			var o = _GetObject(ref name);
			switch (o) {
			case byte[] a: return a;
			case string s: return Encoding.UTF8.GetBytes(s);
			case UnmanagedMemoryStream m:
				var b = new byte[m.Length];
				m.Read(b);
				return b;
			}
			throw new InvalidOperationException($"Resource '{name}' is not byte[], string or stream; it is {o.GetType().Name}.");
		}
		
		/// <summary>
		/// Gets GDI+ image.
		/// </summary>
		/// <param name="name">Resource name, like <c>"file.png"</c> or <c>"sub/file.png"</c>. More info: <see cref="ResourceUtil"/>.</param>
		/// <exception cref="FileNotFoundException">Cannot find assembly or resource.</exception>
		/// <exception cref="InvalidOperationException">The resource type is not stream.</exception>
		/// <exception cref="Exception">Other exceptions that may be thrown by used .NET functions.</exception>
		public static System.Drawing.Bitmap GetGdipBitmap(string name) {
			return new System.Drawing.Bitmap(GetStream(name));
		}
		
		//rejected. Too simple and rare.
		///// <summary>
		///// Gets GDI+ icon.
		///// </summary>
		///// <param name="name">Resource name, like <c>"file.ico"</c> or <c>"sub/file.ico"</c>. More info: <see cref="ResourceUtil"/>.</param>
		///// <exception cref="FileNotFoundException">Cannot find assembly or resource.</exception>
		///// <exception cref="InvalidOperationException">The resource type is not stream.</exception>
		///// <exception cref="Exception">Other exceptions that may be thrown by used .NET functions.</exception>
		//public static System.Drawing.Icon GetGdipIcon(string name) {
		//	return new System.Drawing.Icon(GetStream(name));
		//}
		
		/// <summary>
		/// Gets WPF image or icon that can be used as <b>ImageSource</b>.
		/// </summary>
		/// <param name="name">Resource name, like <c>"file.png"</c> or <c>"sub/file.png"</c>. More info: <see cref="ResourceUtil"/>.</param>
		/// <exception cref="FileNotFoundException">Cannot find assembly or resource.</exception>
		/// <exception cref="InvalidOperationException">The resource type is not stream.</exception>
		/// <exception cref="Exception">Other exceptions that may be thrown by used .NET functions.</exception>
		public static BitmapFrame GetWpfImage(string name) {
			return BitmapFrame.Create(GetStream(name));
		}
		
		/// <summary>
		/// Gets WPF object from XAML resource, for example image.
		/// </summary>
		/// <returns>An object of type of the XAML root object, for example <b>Viewbox</b> if <b>Image</b>.</returns>
		/// <param name="name">Resource name, like <c>"file.xaml"</c> or <c>"sub/file.xaml"</c>. More info: <see cref="ResourceUtil"/>.</param>
		/// <exception cref="FileNotFoundException">Cannot find assembly or resource.</exception>
		/// <exception cref="InvalidOperationException">The resource type is not stream.</exception>
		/// <exception cref="Exception">Other exceptions that may be thrown by used .NET functions.</exception>
		public static object GetXamlObject(string name) {
			return XamlReader.Load(GetStream(name));
		}
		
		/// <summary>
		/// Gets WPF image element from XAML or other image resource.
		/// </summary>
		/// <param name="name">Resource name, like <c>"file.png"</c> or <c>"sub/file.xaml"</c>. More info: <see cref="ResourceUtil"/>.</param>
		/// <exception cref="FileNotFoundException">Cannot find assembly or resource.</exception>
		/// <exception cref="InvalidOperationException">The resource type is not stream.</exception>
		/// <exception cref="Exception">Other exceptions that may be thrown by used .NET functions.</exception>
		/// <remarks>
		/// If <i>name</i> ends with <c>".xaml"</c> (case-insensitive), calls <see cref="GetXamlObject"/>. Else returns <see cref="Image"/> with <b>Source</b> = <see cref="GetWpfImage"/>.
		/// </remarks>
		public static FrameworkElement GetWpfImageElement(string name) {
			if (name.Ends(".xaml", true)) return (FrameworkElement)GetXamlObject(name);
			return new Image { Source = GetWpfImage(name) };
		}
		
		//probably not useful
		///// <summary>
		///// Gets WPF image as <b>BitmapImage</b>.
		///// </summary>
		///// <param name="name">Resource name, like <c>"file.png"</c> or <c>"sub/file.png"</c>. More info: <see cref="ResourceUtil"/>.</param>
		///// <exception cref="FileNotFoundException">Cannot find assembly or resource.</exception>
		///// <exception cref="InvalidOperationException">The resource type is not stream.</exception>
		///// <exception cref="Exception">Other exceptions that may be thrown by used .NET functions.</exception>
		//public static BitmapImage GetWpfBitmapImage(string name) {
		//	var st = GetStream(name);
		//	var bi = new BitmapImage();
		//	bi.BeginInit();
		//	bi.CacheOption = BitmapCacheOption.OnLoad;
		//	bi.StreamSource = st;
		//	bi.EndInit();
		//	return bi;
		//}
		
		/// <summary>
		/// Returns <c>true</c> if string starts with <c>"resource:"</c> or <c>"resources/"</c>.
		/// </summary>
		public static bool HasResourcePrefix(string s) {
			return s.Starts("resource:") || s.Starts("resources/")/* || s.Starts("pack:")*/;
		}
		
		//[MethodImpl(MethodImplOptions.NoInlining)] //avoid loading WPF dlls if no "pack:"
		//static UnmanagedMemoryStream _Pack(string name) {
		//	if (script.role == SRole.MiniProgram && !name.Contains(";component/") && name.Starts("pack://application:,,,/")) name = name.Insert(23, script.name + ";component/");
		//	if (Application.Current == null) new Application();
		//	return Application.GetResourceStream(new Uri(name)).Stream as UnmanagedMemoryStream;
		//}
		
		static object _GetObject(ref string name)
			=> _TryGetObject(ref name) ?? throw new FileNotFoundException($"Cannot find resource '{name}'.");
		
		static object _TryGetObject(ref string name) {
			var rs = _RS(ref name, true);
			if (rs == null) return null;
			var r = rs.GetObject(name);
			if (r == null) r = rs.GetObject(name.Lower());
			return r;
		}
		
		static ResourceSet _RS(ref string name, bool noThrow = false) {
			if (name.Starts("resource:")) name = name[9..];
			string asmName = "";
			if (name is ['<', ..] or ['*', '<', ..]) {
				int i = name[0] == '*' ? 2 : 1;
				int j = name.IndexOf('>', i);
				if (j >= i) {
					asmName = name[i..j];
					name = name[++j..];
				}
			}
			
			lock (s_dict) {
				if (!s_dict.TryGetValue(asmName, out var rs)) {
					var asm = asmName.Length == 0 ? AssemblyUtil_.GetEntryAssembly() : _FindAssembly(asmName);
					if (asm == null) return noThrow ? null : throw new FileNotFoundException($"Cannot find loaded resource assembly '{asmName}'.");
					var rm = new ResourceManager(asm.GetName().Name + ".g", asm);
					rs = rm.GetResourceSet(CultureInfo.InvariantCulture, true, false);
					if (rs == null) return noThrow ? null : throw new FileNotFoundException($"Cannot find resources in assembly '{asmName}'.");
					s_dict.Add(asmName, rs);
				}
				return rs;
			}
		}
		
		static readonly Dictionary<string, ResourceSet> s_dict = new(StringComparer.OrdinalIgnoreCase);
		
		static Assembly _FindAssembly(string name) {
			foreach (var v in AppDomain.CurrentDomain.GetAssemblies()) if (v.GetName().Name.Eqi(name)) return v;
			return null;
		}
	}
}
