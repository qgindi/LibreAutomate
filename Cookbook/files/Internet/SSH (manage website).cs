/// Library info: <link https://github.com/sshnet/SSH.NET/>SSH.NET<>. NuGet: <+nuget>SSH.NET<>.

/*/ nuget -\SSH.NET; /*/
using Renci.SshNet;

var host = "example.com"; //or IP
var port = 12345;
var user = "abcdefgh";
var password = "ijklmnop";
var ftpDir = "public_html/test";

using (var client = new SshClient(host, port, user, password)) {
	client.Connect();
	
	//this function executes a command and returns results if available
	string _Run(string s, bool ignoreError=false) {
		using var c = client.RunCommand(s);
		if(!ignoreError && c.ExitStatus!=0) throw new Exception(c.Error);
		return c.Result;
	}
	
	//cd - change current directory.
	//	It seems need it for all file commands, therefore lets make a function.
	//	Multiple commands can be executed. Use separator ; or &&.
	string _Run2(string s, bool ignoreError=false) => _Run($"cd {ftpDir}; {s}", ignoreError);
	
	//mkdir - create directory if does not exist. Then create another directory in it.
	_Run2("mkdir Dir1", true);
	_Run2("mkdir Dir1/Dir2", true);
	
	//With file commands can be used relative paths, like in the above example.
	
	//cp - copy file or directory. To copy non-empty directory need -r.
	_Run2("cp test.php test2.php");
	_Run2("cp -r Dir1 Dir3");
	_Run2("cp test.php Dir1/"); //copy into directory
	
	//mv - move or rename file or directory.
	_Run2("mv test2.php test3.php");
	_Run2("mv test3.php Dir1/");
	
	//cat - get file text.
	var text = _Run2("cat test.php");
	print.it(text);
	
	//ls - list files and directories in current directory.
	print.it(_Run2("ls -l"));
	
	//unzip - extract zip file.
	//_Run2("unzip Z.zip"); //to current directory
	_Run2("unzip Z.zip -d Dir1"); //to another directory
	
	//rm - delete file or directory. To delete non-empty directory need -r.
	_Run2("rm Z.zip", true);
	_Run2("rm -r Dir1", true);
	
	//Also there are more commands. And more options of the above commands. Look for info on the internet.
}
