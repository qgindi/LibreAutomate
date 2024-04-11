using System.Collections;
using DictSS = System.Collections.Generic.Dictionary<string, string>;

class EnvVarUpdater {
	DictSS _r;
	
	//Called at startup.
	public EnvVarUpdater() {
		_r = _GetVars();
		//print.it(_r);
		
		var s2 = Environment.GetEnvironmentVariable("Path");
		if (_r.TryGetValue("Path", out var s1) && s1 != s2) {
			Debug_.PrintIf(
				s2.Trim(';') != s1, //somehow s2 ends with ';' if not admin, but no ';' if admin
				$"PATH env var changed at startup:\n{s1}\n{s2}");
			Environment.SetEnvironmentVariable("Path", s1);
		}
	}
	
	//Called on WM_SETTINGCHANGE("Environment").
	public void WmSettingchange() {
		csvTable csv = new();
		
		var rOld = _r;
		_r = _GetVars();
		var p = _GetVars(EnvironmentVariableTarget.Process);
		
		//for each env var deleted from registry
		foreach (var k in rOld.Keys.Except(_r.Keys, StringComparer.OrdinalIgnoreCase)) {
			if (p.TryGetValue(k, out var s1) && s1 == rOld[k]) {
				Debug_.Print($"Env var deleted: {k}");
				Environment.SetEnvironmentVariable(k, null);
				csv.AddRow(k, null);
			}
		}
		
		//for each env var added or changed in registry
		foreach (var (k, v) in _r.Except(rOld)) {
			if (p.TryGetValue(k, out var pv)) {
				if (pv == v) continue;
				//print.it(rOld.ContainsKey(k));
				if (!rOld.TryGetValue(k, out var old) || pv != old) continue;
			}
			Debug_.Print(pv == null ? $"Env var added: {k} = \"{v}\"" : $"Env var changed: {k} = \"{v}\",        was \"{pv}\"");
			Environment.SetEnvironmentVariable(k, v);
			csv.AddRow(k, v);
		}
		
		//pass to script processes
		if (csv.RowCount > 0) {
			var s = csv.ToString();
			for (var w = wnd.findFast(null, script.c_auxWndClassName, true); !w.Is0; w = wnd.findFast(null, script.c_auxWndClassName, true, w))
				if (!w.IsOfThisProcess) w.SendTimeout(200, out _, Api.WM_SETTEXT, script.c_msg_wmsettext_UpdateEnvVar, s);
		}
	}
	
	DictSS _GetVars(EnvironmentVariableTarget target) {
		var id = Environment.GetEnvironmentVariables(target);
		DictSS r = new(id.Count, StringComparer.OrdinalIgnoreCase);
		foreach (DictionaryEntry e in id) r.TryAdd(e.Key as string, e.Value as string);
		return r;
	}
	
	DictSS _GetVars() {
		//Join registry env vars user + machine.
		//	Join PATH machine+";"+user. Trim ';'.
		//	Ignore other machine vars that exist in user.
		
		var r = _GetVars(EnvironmentVariableTarget.User);
		var m = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
		bool pathOK = false;
		foreach (DictionaryEntry e in m) {
			string k = e.Key as string, v = e.Value as string;
			if (!r.TryAdd(k, v)) {
				if (!pathOK && (pathOK = k.Eqi("Path"))) r["Path"] = (v + (v.Ends(';') ? null : ";") + r["Path"]).Trim(';');
			}
		}
		if (!pathOK && r.TryGetValue("Path", out var s1)) r["Path"] = s1.Trim(';');
		return r;
	}
}
