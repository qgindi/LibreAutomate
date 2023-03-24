/*
  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Exec Command="cd &quot;$(TargetDir)&quot;&#xD;&#xA;&quot;$(SolutionDir)Other\Programs\ResourceHacker.exe&quot; -script &quot;$(ProjectDir)Resources\ResourceHacker.txt&quot;&#xD;&#xA;del &quot;$(TargetDir)$(TargetName).*.json&quot;&#xD;&#xA;" />
  </Target>
*/

static class TestScript {

	static void _Main() {
		dialog.show("");
	}

//UNDO

	//static async Task _MainAsync() {
	//}

	//[STAThread]
	//static void Main(string[] args) {
	//	//foreach (var v in AppDomain.CurrentDomain.GetAssemblies()) {
	//	//	print.it(v, v.Location);
	//	//}
	//	process.thisProcessCultureIsInvariant = true;
	//	var s1 = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
	//	//print.it(s1.Split(';'));
	//	print.it(s1.Split(';').OrderBy(o => o));
	//	//print.it(s1.Split(';').Select(o=>pathname.getNameNoExt(o)).OrderBy(o => o));

	//	//print.it(AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES"));

	//	//print.it(typeof(System.Text.Json.Nodes.JsonNode).Assembly);

	//}

	[STAThread]
	//static async Task Main(string[] args) {
	static void Main(string[] args) {
		process.thisProcessCultureIsInvariant = true;
		//print.qm2.use = true;
		//print.clear();

		//perf.first();
		try {
			_Main();
			//await _MainAsync();
		}
		catch (Exception ex) { print.it(ex); }
	}
}
