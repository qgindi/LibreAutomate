/*/ nuget -\SSH.NET; /*/
#define SSH
using System.Net;

partial class AuDocs {
	public static void CompressAndUpload(string siteDir) {
		if (1 != dialog.show("Upload?", null, "1 Yes|2 No"/*, secondsTimeout: 5*/)) return;
		var tarDir = pathname.getDirectory(siteDir);
		_Compress(siteDir, tarDir);
		_Upload(tarDir);
	}

#if SSH //extracting with ssh is much faster than with php script, although it seems the host makes it much slower that should be (tested ssh in powershell, the same)
	static void _Compress(string siteDir, string tarDir) {
		var sevenZip = @"C:\Program Files\7-Zip\7z.exe";

		filesystem.delete(tarDir + @"\site.tar");
		filesystem.delete(tarDir + @"\site.tar.gz");

		int r1 = run.console(out var s, sevenZip, $@"a site.tar .\site", tarDir);
		if (r1 != 0) { print.it(s); return; }
		int r2 = run.console(out s, sevenZip, $@"a site.tar.gz site.tar", tarDir);
		if (r2 != 0) { print.it(s); return; }

		filesystem.delete(tarDir + @"\site.tar");

		print.it("Compressed");
	}

	static void _Upload(string tarDir) {
		var rk = @"HKEY_CURRENT_USER\Software\Au\Help";
		var ip = Registry.GetValue(rk, "kur", null) as string;
		var port = (int)Registry.GetValue(rk, "kur2", null);
		var user = Registry.GetValue(rk, "kas", null) as string;
		var pass = Registry.GetValue(rk, "kaip", null) as string;
		if (ip == null || user == null || pass == null) throw new FileNotFoundException("connection info not found in registry");

		//upload. Use SFTP. Maybe possible with SSH, but I tried scp command and failed.
		pass = Encoding.UTF8.GetString(Convert.FromBase64String(pass)); //to encode: print.it(Convert.ToBase64String(Encoding.UTF8.GetBytes(pass)));
		var name = @"/site.tar.gz";
		var path = tarDir + name;
#pragma warning disable SYSLIB0014 // Type or member is obsolete
		using (var client = new WebClient()) {
			client.Credentials = new NetworkCredential(user, pass);
			//client.UploadFile("ftp://quickmacros.com/public_html/au" + name, WebRequestMethods.Ftp.UploadFile, path);
			client.UploadFile("ftp://185.224.138.106/au" + name, WebRequestMethods.Ftp.UploadFile, path); //public_html is default at hostinger
		}
#pragma warning restore SYSLIB0014 // Type or member is obsolete
		filesystem.delete(path);
		print.it("Uploaded");

		//extract
		//perf.first();
		using (var client = new Renci.SshNet.SshClient(ip, port, user, pass)) {
			client.Connect();
			//_Cmd("cd public_html/test"); _Cmd("pwd"); //cd does not work when separate command. Not tested: ShellStream.
			//_Cmd("cd public_html/test && pwd"); //ok
			//perf.next();
			_Cmd2("tar -zxf site.tar.gz");
			_Cmd2("rm -r help", silent: true);
			_Cmd2("mv site help");
			_Cmd2("rm site.tar.gz", silent: true);
			//perf.nw(); //tar ~20 s, others fast
			client.Disconnect();

			void _Cmd(string s, bool silent = false) {
				var c = client.RunCommand(s);
				//print.it($"ec={c.ExitStatus}, result={c.Result}, error={c.Error}");
				if (!silent && c.ExitStatus != 0) throw new Exception(c.Error);
			}

			void _Cmd2(string s, bool silent = false) => _Cmd("cd public_html/au && " + s, silent);
		}
		print.it("<>Extracted to <link>https://www.quickmacros.com/au/help/</link>");
	}

}
#else
	static void Compress(string siteDir)
	{
		var sevenZip = @"C:\Program Files\7-Zip\7z.exe";
		var tarDir = pathname.getDirectory(siteDir);

		filesystem.delete(tarDir + @"\site.tar");
		filesystem.delete(tarDir + @"\site.tar.bz2");

		int r1 = run.console(out var s, sevenZip, $@"a site.tar .\site\*", tarDir);
		if(r1 != 0) { print.it(s); return; }
		int r2 = run.console(out s, sevenZip, $@"a site.tar.bz2 site.tar", tarDir);
		if(r2 != 0) { print.it(s); return; }

		filesystem.delete(tarDir + @"\site.tar");

		print.it("Compressed");
	}

	static void Upload(string tarDir)
	{
		var user = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Au\Help", "kas", null) as string;
		var pass = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Au\Help", "kaip", null) as string;
		var pass2 = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Au\Help", "kaip2", null) as string;
		if(user == null || pass == null || pass2 == null) throw new FileNotFoundException("user or password not found in registry");

		//upload
		pass = Encoding.UTF8.GetString(Convert.FromBase64String(pass)); //to encode: print.it(Convert.ToBase64String(Encoding.UTF8.GetBytes(pass)));
		var name = @"/site.tar.bz2";
		var path = tarDir + name;
		using(var client = new WebClient()) {
			client.Credentials = new NetworkCredential(user, pass);
			//client.UploadFile("ftp://quickmacros.com/public_html/au" + name, WebRequestMethods.Ftp.UploadFile, path);
			client.UploadFile("ftp://185.224.138.106/au" + name, WebRequestMethods.Ftp.UploadFile, path);
		}
		filesystem.delete(path);
		print.it("Uploaded");

		//extract
		using(var client = new WebClient()) {
			string r1 = client.DownloadString($"https://www.quickmacros.com/au/extract_help.php?kaip={pass2}");
			if(r1 != "done") { print.it(r1); return; }
			//extracting used to be few seconds, but now with Hostinger maybe several minutes, and sometimes WebException: The operation has timed out.
			//	The slowness is not in rrmdir, because first time was slow too. Or maybe partially in rrmdir.
		}
		print.it("<>Extracted to <link>https://www.quickmacros.com/au/help/</link>");
	}

}

#if Extract // Eclipse: extract_help.php:

<?php

if($_REQUEST['kaip'] != 'pass2') die('invalid data'); //replace pass2 with the registry value

$bz2=__DIR__ . '/site.tar.bz2';
$help=__DIR__ . '/help';
$tar=__DIR__ . '/site.tar';
rrmdir($help);
if(is_file($tar)) unlink($tar);
$p = new PharData($bz2);
$p->decompress(); // creates $tar
$p = new PharData($tar);
$p->extractTo($help); // extract all files
unlink($tar);
echo("done");

function rrmdir($src) {
    if(!is_dir($src)) return;
    $dir = opendir($src);
    while(false !== ( $file = readdir($dir)) ) {
        if (( $file != '.' ) && ( $file != '..' )) {
            $full = $src . '/' . $file;
            if ( is_dir($full) ) {
                rrmdir($full);
            }
            else {
                unlink($full);
            }
        }
    }
    closedir($dir);
    rmdir($src);
}

?>

#endif
#endif

#if Disqus //add this to the bottom of help pages

<hr style="margin-top: 50px"/>
<div id="disqus_thread"></div>
<script>
/**
*  RECOMMENDED CONFIGURATION VARIABLES: EDIT AND UNCOMMENT THE SECTION BELOW TO INSERT DYNAMIC VALUES FROM YOUR PLATFORM OR CMS.
*  LEARN WHY DEFINING THESE VARIABLES IS IMPORTANT: https://disqus.com/admin/universalcode/#configuration-variables*/
/*
var disqus_config = function () {
this.page.url = PAGE_URL;  // Replace PAGE_URL with your page's canonical URL variable
this.page.identifier = PAGE_IDENTIFIER; // Replace PAGE_IDENTIFIER with your page's unique identifier variable
};
*/
(function() { // DON'T EDIT BELOW THIS LINE
var d = document, s = d.createElement('script');
s.src = 'https://autoandcs.disqus.com/embed.js';
s.setAttribute('data-timestamp', +new Date());
(d.head || d.body).appendChild(s);
})();
</script>

#endif
