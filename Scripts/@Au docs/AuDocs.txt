DocFX should use the latest VS.

When updating DocFX, also may need to update the memberpage NuGet package.
May need to update memberpage version in docfx.json.

info: we don't use the full text search feature.
	Uses CPU 10-20 s after each page [re]load.
	Results are poorly sorted.
	The Google site search does it much better, and often faster.
	info: To enable it, add "_enableSearch": true in "globalMetadata".

With some VS versions fails. Error "... Method not found: 'System.ReadOnlySpan1<Char> Microsoft.IO.Path.GetFileName(System.ReadOnlySpan1)'".
Workaround: https://github.com/dotnet/docfx/issues/8136#issuecomment-1219512721
Apply the same workaround to the memberpage package in C:\Users\G\.nuget\packages\memberpage\
