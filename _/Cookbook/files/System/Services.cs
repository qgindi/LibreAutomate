/// Use NuGet package <+nuget>System.ServiceProcess.ServiceController<>.

/*/ nuget -\System.ServiceProcess.ServiceController; /*/
using System.ServiceProcess;

//Get all non-driver services and print names if running.
print.clear();
var a = ServiceController.GetServices();
foreach (var v in a) {
	if (v.Status != ServiceControllerStatus.Running) continue;
	print.it(v.ServiceName, v.DisplayName);
}

//Service actions.
var se = new ServiceController("WerSvc"); //Windows Error Reporting
int button = dialog.showList("1 Start|2 Stop|3 Pause|4 Continue|5 Wait for running", "Service \"Windows Error Reporting\"", "Current status: " + se.Status);
switch (button) {
case 1: se.Start(); break;
case 2: se.Stop(); break;
case 3: se.Pause(); break;
case 4: se.Continue(); break;
case 5:
	se.WaitForStatus(ServiceControllerStatus.Running);
	print.it("running");
	break;
}
