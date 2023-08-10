using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Au.Controls;
using System.IO.Compression;

static partial class GitSync {
	public class DGit : KDialogWindow {
		public static void AaShow() {
			ShowSingle(() => new DGit());
		}
		
		public DGit() {
			_Start(setup: true);
#if SCRIPT
			Title = "GitHub backup/sync setup";
#else
			InitWinProp("GitHub backup/sync setup", App.Wmain);
#endif
			
			var b = new wpfBuilder(this).WinSize(500);
			b.Columns(-1, 0);
			
			b.R.Add(out KCheckBox cUse, "Use GitHub to backup or sync this workspace");
			b.Add<TextBlock>().Text(new WBLink("Help", _ => HelpUtil.AuHelp("editor/git")), "    ", new WBLink("Folder", _ => run.itSafe(_dir)));
			
			b.R.StartGrid().Hidden();
			var panel = b.Panel;
			
			b.R.StartGrid<GroupBox>("GitHub repository");
			b.R.Add("URL", out TextBox tUrl, _sett.repoUrl)
				.Validation(o => cUse.IsChecked && !_ParseURL(tUrl.Text, out _, out _) ? "URL must be like https://github.com/owner/name" : null);
			
			b.R.Add("Token", out TextBox tToken, _Pat.NE() ? null : "*****")
				.Validation(o => cUse.IsChecked && tToken.Text.NE() ? "Token empty" : null);
			tToken.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
			b.End();
			
			TextBlock tGitStatus = null;
			CancellationTokenSource cts = null;
			b.R.StartGrid<GroupBox>("Git software");
			b.R.AddButton(out var bPrivate, "Install private Git", _InstallGit)
				.Validation(o => _FindGit(out _, out _) ? null : "git.exe not found. Click 'Install private Git'.");
			b.Add(out tGitStatus);
			b.Window.Closed += (_, _) => { cts?.Cancel(); };
			_SetGitStatusText();
			b.End();
			
			b.R.StartGrid<GroupBox>("Optional").Columns(0, -1, 0);
			b.R.Add(out KCheckBox cInclude, "Include .nuget and .interop folders").Span(2);
			_GitignoreInit();
			bool cSyncWasChecked = cInclude.IsChecked;
			b.Add<TextBlock>().Text(new WBLink("Print folder sizes", _ => Task.Run(_PrintFolderSizes)));
			b.R.Add("Script", out TextBox tScript, _sett.script);
#if !SCRIPT
			b.Validation(o => cUse.IsChecked && tScript.Text is var s1 && !s1.NE() && null == App.Model.FindCodeFile(s1) ? "Token empty" : null);
#endif
			b.R.Add("Menu", out TextBox tMenu, _sett.menu).Multiline(50, TextWrapping.NoWrap);
			b.End();
			b.End();
			
			b.R.AddOkCancel();
			b.End();
			
			cUse.CheckChanged += (_, _) => panel.Visibility = cUse.IsChecked ? Visibility.Visible : Visibility.Hidden;
			cUse.IsChecked = _sett.use;
			
			b.OkApply += async e => {
				_sett.use = cUse.IsChecked;
				
				_sett.repoUrl = tUrl.Text;
				var t = tToken.Text;
				if (!t.RxIsMatch(@"^\*+$")) _sett.pat = t.NE() ? null : Convert2.AesEncryptS(_pat = t.Trim('*'), "Ctrl+Shift+Q");
				
				if (_sett.use || cInclude.IsChecked != cSyncWasChecked) _GitignoreApply();
				
				_sett.script = tScript.TextOrNull();
				_sett.menu = tMenu.TextOrNull();
				
				if (_sett.use) {
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
					var gh = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("LA"));
					var rel = await gh.Repository.Release.GetLatest("git-for-windows", "git");
					var url = rel.Assets
						.Where(o => o.Name.Starts("MinGit-", true) && o.Name.Contains("-64-bit"))
						.OrderBy(o => o.Name.Contains("-busybox-") ? 0 : 1) //get the smallest, which is busybox, or any if busybox removed
						.First().BrowserDownloadUrl;
					
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
			
			static void _PrintFolderSizes() {
				print.it("<><lc #C0E0A0>Workspace folder sizes (MB)<>");
				foreach (var f in filesystem.enumDirectories(_dir)) {
					var s = f.Name;
					if (s is ".git" or ".compiled" or ".temp" or ".toolbars") continue;
					if (s == "files") s += " (your scripts are here)";
					print.it($"{filesystem.more.calculateDirectorySize(f.FullPath) / 1048576d:0.#}  {f.Name}");
				}
			}
			
			void _GitignoreInit() {
				var file = _dir + "\\.gitignore";
				if (!filesystem.exists(file)) return;
				var s = filesystem.loadText(file);
				bool nuget = s.RxIsMatch(@"(?m)^\Q/.nuget/*/*\E$");
				bool interop = s.RxIsMatch(@"(?m)^\Q/.interop/\E$");
				if (nuget && interop) return;
				if (!nuget && !interop) cInclude.IsChecked = true;
				else cInclude.IsEnabled = false;
			}
			
			void _GitignoreApply() {
				var file = _dir + "\\.gitignore";
				if (!filesystem.exists(file)) filesystem.saveText(file, """
/.nuget/*/*
/.interop/
/exe/
/dll/

# Don't change these defaults
!/.nuget/*/*.csproj
/.compiled/
/.temp/
/.tmp*/
""");
				if (cInclude.IsChecked == cSyncWasChecked) return;
				var s = filesystem.loadText(file);
				if (cInclude.IsChecked) s = s.RxReplace(@"(?m)^(?:\Q/.nuget/*/*\E|\Q/.interop/\E)\R", "");
				else s = "/.nuget/*/*\r\n/.interop/\r\n" + s;
				filesystem.saveText(file, s);
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
}
