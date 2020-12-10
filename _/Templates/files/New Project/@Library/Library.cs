﻿/*/ role classLibrary; outputPath %AFolders.ThisApp%\Libraries /*/
using Au; using Au.Types; using System; using System.Collections.Generic; using System.IO; using System.Linq;
using System.Reflection;

[assembly: AssemblyVersion("1.0.0.0")]

// attributes for native version resource, all optional
//[assembly: AssemblyFileVersion("1.0.0.0")] //if missing, uses AssemblyVersion
//[assembly: AssemblyTitle("File description")]
//[assembly: AssemblyDescription("Comments")]
//[assembly: AssemblyCompany("Company name")]
//[assembly: AssemblyProduct("Product name")]
//[assembly: AssemblyInformationalVersion("1.0.0.0")] //product version
//[assembly: AssemblyCopyright("Copyright © 2020")]
//[assembly: AssemblyTrademark("Legal trademarks")]

/*
When you compile this, .dll and other required files are created in the output directory (outputPath).
Before deploying the files, in Properties set optimize true and compile.
The library can be used on computers with installed .NET Core Runtime 5. Download: https://dotnet.microsoft.com/download
*/

namespace Library {

public class Class1 {
	
	public static void Function1() {
		
	}
	
	
}

}