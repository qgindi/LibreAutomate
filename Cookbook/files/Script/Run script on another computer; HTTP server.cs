/// Assume you want to run some codes or scripts on computer named <i>SERVER<> from computer named <i>CLIENT<>. The computers can be on local network or internet or it can be the same computer. CLIENT can be any device that can use the internet (PC, smartphone, etc).
/// 
/// On SERVER computer run code like this. It can be a script or independent program.

//HTTP server. Runs all the time.
HttpServerSession.Listen<HttpServerSessionExamples>();

class HttpServerSessionExamples : HttpServerSession {
	protected override void MessageReceived(HSMessage m, HSResponse r) {
		//Here can be any code. It runs when the HTTP server receives a HTTP request message. Below are examples.
		
		#region simple
		
		//Prevent executing the rest of code without a password.
		Auth((u, p) => p == "password278351");
		
		//Print some properties of the HTTP request message.
		print.it(m.Method, m.TargetPath, m.UrlParameters, m.Text, m.Headers);
		
		//Return some text.
		r.SetContentText("text");
		
		//Several examples of code on CLIENT computer.
		//To make easier to read this recipe, client code examples here are below corresponding server code examples,
		//	in local functions like this. Normally these codes would be in a script on CLIENT computer, not here.
		static void client_example_simple() {
			//Simplest HTTP request.
			internet.http.Get("http://SERVER:4455");
			
			//HTTP request with password (basic authentication).
			internet.http.Get("http://SERVER:4455", auth: ":password278351");
			
			//If server and client are on the same computer, SERVER can be "localhost" or "127.0.0.1" or "[::1]".
			internet.http.Get("http://[::1]:4455");
			
			//Get text returned by MessageReceived.
			string r = internet.http.Get("http://SERVER:4455").Text();
			print.it(r);
		}
		
		#endregion
		
		#region run script
		
		//Examples of how SERVER can process "run script" requests.
		if (m.Method == "GET" && m.TargetPath == "/script.run") {
			try {
				//Without arguments.
				script.run(m.UrlParameters["name"]);
				
				//With 1 argument (in that script it will be args[0]).
				script.run(m.UrlParameters["name"], m.UrlParameters["a0"]);
				
				//Run the script and return its results written with script.writeResult.
				script.runWait(out string res, m.UrlParameters["name"]);
				r.SetContentText(res);
				
				//The scripts must be in current workspace on SERVER computer.
				
				//Or you can place script code in the server script. For example, in separate functions, and let MessageReceived call them.
				//But there are some bad things:
				//-	To apply changes in script code will need to restart the server process.
				//- May be difficult or impossible to stop the script without killing the server process.
				//- A failed script in some cases may kill the server process.
				//- Some bugs in scripts and used libraries may create memory leaks and other mess in the server process.
			}
			catch { r.Status = System.Net.HttpStatusCode.NotFound; }
		}
		
		//Examples of how CLIENT can send "run script" requests.
		static void client_example_run_script() {
			//Run script "Script name.cs" with argument "hello" (in that script it will be args[0]).
			internet.http.Get("http://SERVER:4455/script.run?name=Script+name.cs&a0=hello");
			
			//To URL-encode the script name and arguments can be used internet.urlAppend.
			internet.http.Get(internet.urlAppend("http://SERVER:4455/script.run", "name=Script name.cs", "a0=hello"));
		}
		
		#endregion
		
		#region run script using POST
		
		//Example of how SERVER can process "run script" requests using POST request.
		if (m.Method == "POST" && m.TargetPath == "/script.run") {
			try { script.run(m.Multipart["name"], m.Multipart["a0"]); }
			catch { r.Status = System.Net.HttpStatusCode.NotFound; }
		}
		
		//Example of code on CLIENT computer.
		static void client_example_run_script_post() {
			internet.http.Post("http://SERVER:4455/script.run", internet.formContent(("name", "Script name.cs"), ("a0", "long text")));
		}
		
		#endregion
		
		#region send and run script code
		
		//The editor program can run only scripts that exist in current workspace, and does not have importing API.
		//Workaround: manually create an empty script on SERVER, and let MessageReceived replace its text when CLIENT sends script code.
		//To test this example, at first please create folder "Volatile" in the editor on SERVER computer. Then in it create script "Code-1".
		
		//Example of how SERVER can process "run script" requests when CLIENT sends script code.
		//Receives script code, saves it in an existing script (overwrites), and runs the script.
		Auth((u, p) => p == "password441+k");
		if (m.Method == "POST" && m.TargetPath == "/script.run/code") {
			string file = m.Multipart["file"], code = m.Multipart["code"];
			string fullPath = folders.Workspace + $@"files\Volatile\{file}";
			if (code != filesystem.loadText(fullPath)) filesystem.saveText(fullPath, code);
			script.run($@"\Volatile\{file}");
		}
		
		//Example of code on CLIENT computer.
		static void client_example_run_script_code() {
			var content = internet.formContent(("file", @"Code-1.cs"), ("code", """print.it("test");"""));
			internet.http.Post("http://SERVER:4455/script.run/code", content, auth: ":password441+k");
		}
		
		#endregion
		
		#region pass objects using JSON
		
		//Example of how SERVER can receive and return an object using JSON.
		if (m.Method == "POST" && m.TargetPath == "/json") {
			var point = m.Json<POINT>();
			print.it(point);
			var rect = new RECT(point.x * 2, point.y * 2, 100, 100);
			r.SetContentJson(rect);
		}
		
		//Example of code on CLIENT computer.
		static void client_example_json() {
			var point = new POINT(1, 2);
			var rect = internet.http.Post("http://SERVER:4455/json", internet.jsonContent(point)).Json<RECT>();
			print.it(rect);
		}
		
		//This example uses known types (POINT and RECT), but you can define and use classes or structs for it.
		
		#endregion
		
		#region upload file
		
		//Example of how SERVER can receive a file.
		if (m.Method == "POST" && m.TargetPath == "/files") {
			var v = m.Multipart["file"];
			filesystem.saveBytes(@"C:\Test\HttpServerSession\files\" + v.FileName, v.Content);
		}
		
		//Example of code on CLIENT computer.
		static void client_example_upload_file() {
			internet.http.Post("http://SERVER:4455/files", internet.formContent(("some", "info")).AddFile("file", @"C:\Test\file.txt"));
		}
		
		#endregion
		
		#region download file
		
		//Example of how SERVER can return a file.
		if (m.Method == "GET") {
			var file = m.TargetPath; if (file == "/") file = "index.html";
			file = pathname.combine(@"C:\Test\HttpServerSession", file);
			try {
				r.Content = File.ReadAllBytes(file);
				r.Headers["Content-Type"] = "text/html; charset=utf-8";
			}
			catch { r.Status = System.Net.HttpStatusCode.NotFound; }
		}
		
		//Example of code on CLIENT computer.
		static void client_example_download_file() {
			var r = internet.http.Get("http://SERVER:4455/example.html");
			filesystem.saveBytes(@"C:\Test\Somewhere\example.html", r.Bytes());
		}
		
		#endregion
		
		#region internet
		
		//Usually the HTTP server is not directly accessible from the internet. For it you can use ngrok or similar software.
		//For example, on the SERVER computer install/register ngrok, and in cmd execute: ngrok http 4455.
		//Then on CLIENT computer use the URL displayed in the ngrok console window on the SERVER computer.
		
		//Example of code on CLIENT computer.
		static void client_example_internet() {
			internet.http.Get("https://1111-11-111-11-111.ngrok-free.app/", auth: ":password278351");
		}
		
		#endregion
		
		#region more info
		
		//Instead of a C# script on CLIENT computer can be used a web browser or any program or programming language/library that can send HTTP requests. Any device, any OS.
		
		//If you use the URL in a web browser, and SERVER requires a password (calls Auth like in the example), you can either use URL like http://user:password@www.example.com or the web browser will ask for a user name and password.
		
		//If port 4455 unavailable, use some other port. Examples:
		//SERVER: HttpServerSession.Listen<HttpServerSessionExamples>(33333);
		//CLIENT: internet.http.Get("http://SERVER:33333");
		
		//If HTTP requests on LAN work with a delay, possibly because of slow DNS (computer name to IP translation).
		//Workaround: let CLIENT use server IP instead of computer name.
		//Also don't use IPv4 with HttpServerSession.Listen.
		static void client_example_ip() {
			internet.http.Get("http://123.45.67.89:4455");
			
			//Print IP of computer named "SERVER".
			print.it(System.Net.Dns.GetHostAddresses("SERVER"));
		}
		
		#endregion
	}
}

/// See also: <link>https://www.libreautomate.com/forum/showthread.php?tid=7468<>
