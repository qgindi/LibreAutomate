using System.Windows;

namespace Au.Controls;

public class XamlResources {
#if IDE_LA
	static XamlResources() {
		//Dictionary = System.Windows.Markup.XamlReader.Load(typeof(XamlResources).Assembly.GetManifestResourceStream("Generic.xaml")) as ResourceDictionary;
		
		//workaround for: at run time fails to load XAML if without `;assembly=Au.Controls`, but VS fails to compile if with.
		using var stream = typeof(XamlResources).Assembly.GetManifestResourceStream("Generic.xaml");
		string xaml = new StreamReader(stream).ReadToEnd();
		xaml = xaml.Replace("clr-namespace:Au.Controls", "clr-namespace:Au.Controls;assembly=Au.Controls");
		using var xmlReader = System.Xml.XmlReader.Create(new StringReader(xaml));
		Dictionary = (ResourceDictionary)System.Windows.Markup.XamlReader.Load(xmlReader);
	}
	
	public static readonly ResourceDictionary Dictionary;
#else
	public static readonly ResourceDictionary Dictionary = Application.LoadComponent(new("/Au.Controls;component/resources/generic.xaml", UriKind.Relative)) as ResourceDictionary; //build action = Page (default)
	//public static ResourceDictionary Dictionary = System.Windows.Markup.XamlReader.Load(typeof(XamlResources).Assembly.GetManifestResourceStream("Au.Controls.Themes.Generic.xaml")) as ResourceDictionary; //build action = embedded resource. Works, but debugger prints handled exception. And probably slower.
#endif
}
