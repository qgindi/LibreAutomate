namespace Au.More
{
	/// <summary>
	/// Executes actions in same UI thread as ctor.
	/// Use <see cref="OfThisThread"/>. Cannot create more instances.
	/// </summary>
	class PostToThisThread_
	{
		readonly wnd _w;
		readonly Queue<Action> _q = new();

		PostToThisThread_() {
			_w = WndUtil.CreateWindowDWP_(messageOnly: true, t_wp = _WndProc);
		}

		public static PostToThisThread_ OfThisThread => t_default ??= new();
		[ThreadStatic] static PostToThisThread_ t_default;
		[ThreadStatic] static WNDPROC t_wp;

		public void Post(Action a) {
			bool post;
			lock (this) {
				post = _q.Count == 0;
				_q.Enqueue(a);
			}
			if (post) _w.Post(Api.WM_USER);
		}

		nint _WndProc(wnd w, int message, nint wParam, nint lParam) {
			switch (message) {
			case Api.WM_USER:
				//print.it(_q.Count);
				object o;
				lock (this) {
					o = _q.Count switch { 0 => null, 1 => _q.Dequeue(), _ => _q.ToArray() };
					_q.Clear();
				}
				switch (o) {
				case Action a:
					a();
					break;
				case Action[] a:
					foreach (var f in a) f();
					break;
				}
				return default;
			}

			return Api.DefWindowProc(w, message, wParam, lParam);
		}
	}
}
