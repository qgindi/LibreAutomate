/// <summary>
/// Logs started/ended tasks and trigger actions for menu Run > Recent.
/// </summary>
static class RecentTT {
	record _Item {
		public long id;
		public FileNode file;
		public int line;
		public int repeated;
		public long startTime;
		public long endTime;
		public string trigger;
		public bool failed;
	}
	
	static readonly List<_Item> s_a = new();
	
	public static void TaskEvent(bool started, RunningTask t, int exitCode = 0) {
		Api.GetSystemTimeAsFileTime(out long time);
		if (started) _Started(t.taskId, t.f, 0, time, null);
		else _Ended(t.taskId, time, exitCode < 0);
	}
	
	public static void TriggerEvent(PrintServerMessage m) {
		string s = m.Text, z = m.Caller;
		//z format: "id\0sourceFilePath\0line1based"
		//	id identifies that action instance in time (start/end/fail events have same id).
		
		z.ToInt(out long id, 0, out int i); i++;
		
		int j = z.IndexOf('\0', i);
		var fn = App.Model.FindByFilePath(z[i..j], FNFind.CodeFile);
		if (fn == null) return; //deleted item?
		int line = z.ToInt(++j);
		
		if (s[1] == 'S') _Started(id, fn, line, m.TimeUtc, s[3..]);
		else _Ended(id, m.TimeUtc, s[1] == 'F');
	}
	
	static void _Started(long id, FileNode fn, int line, long time, string trigger) {
		//remove oldest ended items
		if (s_a.Count > 250) {
			var t1 = Environment.TickCount64;
			if (t1 - s_shrinkTime > 1000) {
				s_shrinkTime = t1;
				var ar = new List<_Item>(); //running
				int to = s_a.Count / 4;
				for (int i = 0; i < to; i++) if (s_a[i].endTime == 0) ar.Add(s_a[i]);
				s_a.RemoveRange(0, to);
				s_a.InsertRange(0, ar);
			}
		}
		
		//join multiple same items if started/ended frequently, eg an auto-repeated hotkey
		//CONSIDER: join when ends. To avoid joining failed with succeeded and running with ended.
		//CONSIDER: in main menu display only dictinct. Display multiple run instances (start/end time, failed) in submenu or tooltip.
		_Item last = null;
		for (int i = s_a.Count; --i >= 0;) {
			var v = s_a[i];
			if (v.endTime == 0) continue;
			if (time - v.endTime > 10_000_000L * 70) break; //70 s
			if (v.file == fn && v.line == line && v.trigger == trigger && !v.failed) {
				last = v;
				break;
			}
		}
		if (last != null) {
			//print.it("same");
			last.id = id; last.startTime = time; last.endTime = 0; last.repeated++;
		} else {
			s_a.Add(new() { id = id, file = fn, line = line, startTime = time, trigger = trigger });
		}
	}
	static long s_shrinkTime;
	
	static void _Ended(long id, long time, bool failed) {
		for (int i = s_a.Count; --i >= 0;) if (s_a[i].id == id) {
				var k = s_a[i];
				k.endTime = time;
				k.failed = failed;
				break;
			}
	}
	
	public static void Clear() {
		s_a.Clear();
	}
	
	public static void Show() {
		if (s_a.Count == 0) return;
		bool highContrast = System.Windows.SystemParameters.HighContrast;
		var m = new popupMenu();
		for (int i = s_a.Count; --i >= 0;) {
			var v = s_a[i];
			var s = $"{v.trigger ?? v.file.DisplayName}\t{_Time(v.startTime)} - {_Time(v.endTime)}";
			if (v.repeated > 0) s = $"{s} ({v.repeated + 1} times)";
			var k = m.Add(s, v.trigger != null ? Menus.iconTrigger : v.file.IconString);
			k.Tag = v;
			if (highContrast) {
				if (v.failed) k.TextColor = 0xff0000;
			} else {
				if (v.endTime == 0) k.TextColor = 0x0000ff; else if (v.failed) k.TextColor = 0xff0000;
			}
		}
		m.Show();
		if (m.Result?.Tag is _Item r) App.Model.OpenAndGoTo(r.file, r.line - 1);
		
		static string _Time(long t) {
			if (t == 0) return null;
			return DateTime.FromFileTimeUtc(t).ToLocalTime().ToString("dd H:mm:ss");
		}
	}
}
