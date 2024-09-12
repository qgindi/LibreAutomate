/*/ testInternal Au; /*/
print.clear();
var x = new SdkConverter.Converter();

#if !true
//x.Convert(
//	@"C:\code\au\Other\SdkConverter\Data\Header.h",
//	//@"C:\code\au\Other\SdkPreprocess\Cpp.cpp",
//	@"C:\code\au\Other\Api\Api-small.cs", false);

x.Convert(@"C:\code\au\Other\Api\Api-preprocessed-64.cpp", @"C:\code\au\Other\Api\Api-64.cs", false);
print.scrollToTop();
#else
x.Convert(@"C:\code\au\Other\Api\Api-preprocessed-64.cpp", @"C:\code\au\Other\Api\Api-64.cs", false);

if (true) {
	new SdkConverter.Converter().Convert(@"C:\code\au\Other\Api\Api-preprocessed-32.cpp", @"C:\code\au\Other\Api\Api-32.cs", true);
}
#endif
