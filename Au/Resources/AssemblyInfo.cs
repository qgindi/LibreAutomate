
[assembly: AssemblyTitle("Au")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
//more in global.cs

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("d3087fac-a12d-4365-a620-7574cd89b17f")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
//[assembly: AssemblyVersion("1.0.0.*")]
//[assembly: AssemblyVersion("1.0.1.0")] //moved to global.cs
//rejected: auto increment.
//	Creates more problems and work than is useful. Eg after modifying this project always need to rebuild all exe projects, else fails to load this dll.
//	VS adds 20-300 to the revision at each build. Why not 1? Now ~ 23000. What happens when it becomes the max possible 0xffff?
//	VS does not auto-increment the build number when "1.0.*". Now 7288. Where it gets these values? And does not reset them when I change eg minor.

//[assembly: AssemblyFileVersion("1.0.0.0")]

//This can be used eg to find public/protected _Identifier.
//However most warnings are about uint. We disable them in project Properties: 3001,3002,3003,3009.
//[assembly: CLSCompliant(true)] //rejected. It isn't.

[assembly: InternalsVisibleTo("Au.Editor, PublicKey=0024000004800000940000000602000000240000525341310004000001000100095c6b7a0fe60fbe4a77e52dd10a09331ee3c3a7399aa9cc17db8a015647469a19784d5e33a2450a0a49c37bf17c0c3223674f64104eae649ba27c51a90c24989faec87d59217d7850efc8151109bbf9b027b7714fc01788317d2b991b2c2669836a7725e942f76607efde5cdacd8c497a45c5f9673fcf102fdbf92237a524a4")]
[assembly: InternalsVisibleTo("Au.Controls, PublicKey=0024000004800000940000000602000000240000525341310004000001000100095c6b7a0fe60fbe4a77e52dd10a09331ee3c3a7399aa9cc17db8a015647469a19784d5e33a2450a0a49c37bf17c0c3223674f64104eae649ba27c51a90c24989faec87d59217d7850efc8151109bbf9b027b7714fc01788317d2b991b2c2669836a7725e942f76607efde5cdacd8c497a45c5f9673fcf102fdbf92237a524a4")]
[assembly: InternalsVisibleTo("Au.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100095c6b7a0fe60fbe4a77e52dd10a09331ee3c3a7399aa9cc17db8a015647469a19784d5e33a2450a0a49c37bf17c0c3223674f64104eae649ba27c51a90c24989faec87d59217d7850efc8151109bbf9b027b7714fc01788317d2b991b2c2669836a7725e942f76607efde5cdacd8c497a45c5f9673fcf102fdbf92237a524a4")]
[assembly: InternalsVisibleTo("SdkConverter, PublicKey=0024000004800000940000000602000000240000525341310004000001000100095c6b7a0fe60fbe4a77e52dd10a09331ee3c3a7399aa9cc17db8a015647469a19784d5e33a2450a0a49c37bf17c0c3223674f64104eae649ba27c51a90c24989faec87d59217d7850efc8151109bbf9b027b7714fc01788317d2b991b2c2669836a7725e942f76607efde5cdacd8c497a45c5f9673fcf102fdbf92237a524a4")]

[assembly: AssemblyMetadata("RepositoryUrl", "https://github.com/qgindi/au")]
