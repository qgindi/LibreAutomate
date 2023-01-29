# Read Excel files
<a href='https://github.com/ExcelDataReader/ExcelDataReader'>ExcelDataReader</a> is a lightweight, fast and free library for reading Excel files (.xlsx, .xls, .csv). NuGet: <u title='Paste the underlined text in menu -> Tools -> NuGet'>ExcelDataReader</u> or <u title='Paste the underlined text in menu -> Tools -> NuGet'>ExcelDataReader.DataSet</u>.

```csharp
/*/ nuget -\ExcelDataReader; /*/
using ExcelDataReader;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

string file = folders.Downloads + "Financial Sample.xlsx";
using (var stream = File.OpenRead(file)) {
	using var r = ExcelReaderFactory.CreateReader(stream);
	while (r.Read()) {
		print.it(r.GetString(0), r[1]);
	}
}
```

