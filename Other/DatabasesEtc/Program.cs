static class Program {
	public const string c_outputDirBS = @"C:\code\au\_\";

	[STAThread]
	static void Main(string[] args) {
		script.setup();
		print.qm2.use = true;
		print.clear();

		//RefTxt.Create();
		//RefAndDoc.Create();
		//Icons.CreateDB();
	}
}
