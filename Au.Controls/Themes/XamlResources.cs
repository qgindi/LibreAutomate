using System.Windows;

namespace Au.Controls;

public class XamlResources {
#if IDE_LA
	public static ResourceDictionary Dictionary = System.Windows.Markup.XamlReader.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream("Generic.xaml")) as ResourceDictionary;
#else
	public static ResourceDictionary Dictionary = Application.LoadComponent(new("/Au.Controls;component/themes/generic.xaml", UriKind.Relative)) as ResourceDictionary;
	//build action = default (Page).
#endif
	
	//public static ResourceDictionary Dictionary = XamlReader.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream("Au.Controls.Themes.Generic.xaml")) as ResourceDictionary;
	//build action = embedded resource. Normally works, but somehow exception when debugger.
}
