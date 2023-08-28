using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Au.Controls;
using System.IO.Compression;
using System.Windows.Documents;
using System.Text.Json.Nodes;

static partial class GitSync {
	public class DGit : KDialogWindow {
		public static void AaShow() {
			ShowSingle(() => new DGit());
		}
		
		public DGit() {
			_Start(setup: 1);
#if SCRIPT
			Title = "Git setup";
#else
			InitWinProp("Git setup", App.Wmain);
#endif
			
			var b = new wpfBuilder(this).WinSize();
			b.Columns(-1, 0);
			
			b.R.Add(out KCheckBox cUse, "Use Git and GitHub to backup or sync this workspace");
			
			b.R.StartGrid().Hidden();
			var panel = b.Panel;
			
			WBLink lHelp = new("Help", _ => HelpUtil.AuHelp("editor/git")),
				lFolder = new("Workspace folder", _ => run.itSafe(_dir)),
				lIgnore = new("File .gitignore", _ => _GitignoreFile()),
				lSizes = new("Print folder sizes", _ => Task.Run(_PrintFolderSizes)),
				lGithub = new("GitHub", _sett.repoUrl.NullIfEmpty_() ?? "https://github.com");
			b.R.Add<TextBlock>().Text(lHelp, "    ", lFolder, "    ", lIgnore, "    ", lSizes, "    ", lGithub);
			
			b.R.Add<AdornerDecorator>("Repository", out _).Add(out TextBox tUrl, _sett.repoUrl, flags: WBAdd.ChildOfLast).Watermark("https://github.com/owner/name")
				.Validation(o => cUse.IsChecked && !_ParseURL(tUrl.Text, out _, out _) ? (tUrl.Text.NE() ? "Repository URL empty" : "Repository URL must be like https://github.com/owner/name") : null);
			
			b.R.StartStack();
			TextBlock tGitStatus = null;
			CancellationTokenSource cts = null;
			b.AddButton(out var bPrivate, "Install private minimal Git", _InstallGit)
				.Validation(o => _FindGit(out _, out _) ? null : "git.exe not found. Click 'Install...'.");
			b.Add(out tGitStatus);
			b.Window.Closed += (_, _) => { cts?.Cancel(); };
			_SetGitStatusText();
			b.End();
			
			b.R.Add(out KCheckBox cInclude, "Include .nuget and .interop folders");
			switch (_GitignoreIncludeNugetEtc()) { case true: cInclude.IsChecked = true; break; case null: cInclude.IsEnabled = false; break; }
			b.R.Add("Message", out TextBox tMessage, _sett.message);
			b.R.Add("Script", out TextBox tScript, _sett.script);
#if !SCRIPT
			b.Validation(o => cUse.IsChecked && tScript.Text is var s1 && !s1.NE() && null == App.Model.FindCodeFile(s1) ? "Script not found" : null);
#endif
			b.End();
			
			b.R.AddOkCancel();
			b.End();
			
			cUse.CheckChanged += (_, _) => panel.Visibility = cUse.IsChecked ? Visibility.Visible : Visibility.Hidden;
			cUse.IsChecked = _sett.use;
			
			b.OkApply += async e => {
				_sett.repoUrl = tUrl.Text;
				_sett.message = tMessage.TextOrNull();
				_sett.script = tScript.TextOrNull();
				if (_sett.use = cUse.IsChecked) {
					_GitignoreApply();
					_ScriptApply();
					e.Cancel = true;
					b.Window.IsEnabled = false;
					await GitSync._DGitApply();
					b.Window.Close();
				}
			};
			
			async void _InstallGit(WBButtonClickArgs k) {
				k.Button.IsEnabled = false;
				tGitStatus.Text = "Downloading";
				cts = new();
				await _InstallGit2();
				cts = null;
				_SetGitStatusText();
				k.Button.IsEnabled = true;
			}
			
			async Task<bool> _InstallGit2() {
				try {
					//can create files in LA dir?
					var zip = folders.ThisAppBS + "mingit.zip";
					using (File.Create(zip, 0, FileOptions.DeleteOnClose)) { }
					
					//get URL of the latest mingit zip
#if true
					var r = _GithubGet($"repos/git-for-windows/git/releases/latest");
					var e1 = r["assets"].AsArray().Where(o => ((string)o["name"]).Like("MinGit-*-64-bit.zip", true));
					r = e1.FirstOrDefault(o => ((string)o["name"]).Contains("-busybox-")) ?? e1.First(); //get the smallest, which is busybox, or any if busybox removed
					var url = (string)r["browser_download_url"];
#else
					var gh = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("LA"));
					var rel = await gh.Repository.Release.GetLatest("git-for-windows", "git");
					var url = rel.Assets
						.Where(o => o.Name.Like("MinGit-*-64-bit.zip", true))
						.OrderBy(o => o.Name.Contains("-busybox-") ? 0 : 1) //get the smallest, which is busybox, or any if busybox removed
						.First().BrowserDownloadUrl;
#endif
					
					//download and unzip
					try {
						var rm = internet.http.Get(url, dontWait: true);
						if (!await rm.DownloadAsync(zip, p => { tGitStatus.Text = $"Downloading, {p.Percent}%"; }, cts.Token).ConfigureAwait(false)) return false;
						var gitDir = folders.ThisAppBS + "Git";
						filesystem.delete(gitDir);
						ZipFile.ExtractToDirectory(zip, gitDir, overwriteFiles: true);
					}
					finally {
						try { filesystem.delete(zip); } catch { }
					}
				}
				catch (Exception e1) {
					print.it(e1.Message);
					if (e1 is UnauthorizedAccessException && !uacInfo.isAdmin) print.it("\tRestart this program as administrator.");
					return false;
				}
				return true;
			}
			
			void _SetGitStatusText() {
				bool found = _FindGit(out var s, out bool isShared);
				tGitStatus.Text = !found ? "Private Git not installed. Shared Git not found." : isShared ? "Not installed. Found shared Git." : "Installed.";
			}
			
			//returns: false - no file, or exist all /.nuget/ etc; true - exist none of /.nuget/ etc; null exist some of /.nuget/ etc.
			bool? _GitignoreIncludeNugetEtc() {
				var file = _dir + "\\.gitignore";
				if (!filesystem.exists(file)) return false;
				var s = filesystem.loadText(file);
				bool nuget = s.RxIsMatch(@"(?m)^\Q/.nuget/*/*\E$");
				bool interop = s.RxIsMatch(@"(?m)^\Q/.interop/\E$");
				if (nuget && interop) return false;
				if (!nuget && !interop) return true;
				return null;
			}
			
			void _GitignoreApply() {
				var file = _dir + "\\.gitignore";
				if (!filesystem.exists(file)) filesystem.saveText(file, c_defaultGitignore);
				bool? include = _GitignoreIncludeNugetEtc();
				if (include == null || include.Value == cInclude.IsChecked) return;
				var s = filesystem.loadText(file);
				if (cInclude.IsChecked) s = s.RxReplace(@"(?m)^(?:\Q/.nuget/*/*\E|\Q/.interop/\E)\R", "");
				else s = "/.nuget/*/*\r\n/.interop/\r\n" + s;
				filesystem.saveText(file, s);
			}
			
			void _GitignoreFile() {
				var file = _dir + "\\.gitignore";
				string s1;
				if (filesystem.exists(file)) {
					run.selectInExplorer(file);
					s1 = "exists and you can edit it. Else this dialog would";
				} else {
					s1 = "still does not exist. This dialog will";
				}
				print.it($"<><lc #C0E0A0>File <b>.gitignore<> {s1} create it and write this text. The 'Include...' checkbox deletes some lines.<>");
				print.it(c_defaultGitignore);
			}
			
			void _ScriptApply() {
#if !SCRIPT
				if (_sett.script.NE()) { //else validated
					var f = App.Model.FindCodeFile("Git script.cs");
					if (f == null && App.Model.FoundMultiple == null) {
						App.Model.NewItemL(@"Default\Git script.cs");
					}
				}
#endif
			}
			
			static void _PrintFolderSizes() {
				FileTree.PrintSizes(_dir, true, .1, dirFilter: o => !(o.Level is 0 && o.Name.Lower() is ".git" or ".compiled" or ".temp"));
			}
		}
		
#if !SCRIPT
		protected override void OnSourceInitialized(EventArgs e) {
			App.Model.UnloadingThisWorkspace += Close;
			base.OnSourceInitialized(e);
		}
		
		protected override void OnClosed(EventArgs e) {
			App.Model.UnloadingThisWorkspace -= Close;
			base.OnClosed(e);
		}
#endif
	}
	
	const string c_defaultGitignore = """
/.nuget/*/*
/.interop/
/exe/
/dll/

# Don't change these defaults
!/.nuget/*/*.csproj
/.compiled/
/.temp/

""";
}
