/// Library info: <link https://github.com/sshnet/SSH.NET/>SSH.NET<>. NuGet: <+nuget>SSH.NET<>.

/*/ nuget -\SSH.NET; /*/
using Renci.SshNet;

var host = "example.com"; //or IP
var port = 12345;
var user = "abcdefgh";
var password = "ijklmnop";
var ftpDir = "public_html/test";

var file = @"C:\Test\Z.zip";
var file2 = @"C:\Test\Z2.zip";

using (var client = new SftpClient(host, port, user, password)) {
	client.Connect();
	client.ChangeDirectory(ftpDir);
	
	//upload
	using (var stream = File.OpenRead(file)) {
		client.UploadFile(stream, Path2.GetName(file));
	}
	
	//download
	using (var stream = File.Create(file2)) {
		client.DownloadFile(Path2.GetName(file), stream);
	}
	
	//Also there are functions to create, delete, rename, exists, get/set attributes, get times, get list, change permissions, synchronize directories. More info on the internet.
}
