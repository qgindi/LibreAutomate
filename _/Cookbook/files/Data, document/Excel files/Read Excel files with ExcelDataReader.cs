/// <link https://github.com/ExcelDataReader/ExcelDataReader>ExcelDataReader<> is a lightweight, fast and free library for reading Excel files (<_>.xlsx, .xls, .csv</_>). NuGet: <+nuget>ExcelDataReader<> or <+nuget>ExcelDataReader.DataSet<>.

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
