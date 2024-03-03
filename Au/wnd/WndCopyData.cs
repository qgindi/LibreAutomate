namespace Au.More {
	/// <summary>
	/// Send/receive data to/from other process using message <msdn>WM_COPYDATA</msdn>.
	/// </summary>
	/// <remarks>
	/// This struct is <msdn>COPYDATASTRUCT</msdn>.
	/// <note>By default [](xref:uac) blocks messages sent from processes of lower integrity level. Call <see cref="EnableReceivingWM_COPYDATA"/> if need.</note>
	/// </remarks>
	/// <seealso cref="System.IO.MemoryMappedFiles.MemoryMappedFile"/>
	/// <seealso cref="System.IO.Pipes.NamedPipeServerStream"/>
	public unsafe struct WndCopyData {
		//COPYDATASTRUCT fields
		nint _dwData;
		int _cbData;
		byte* _lpData;

		#region receive

		/// <summary>
		/// Initializes this variable from <i>lParam</i> of a received <msdn>WM_COPYDATA</msdn> message.
		/// Then you can call functions of this variable to get data in managed format.
		/// </summary>
		/// <param name="lParam"><i>lParam</i> of a <msdn>WM_COPYDATA</msdn> message received in a window procedure. It is <msdn>COPYDATASTRUCT</msdn> pointer.</param>
		public WndCopyData(nint lParam) {
			var p = (WndCopyData*)lParam;
			_dwData = p->_dwData; _cbData = p->_cbData; _lpData = p->_lpData;
		}

		/// <summary>
		/// Data id. It is <msdn>COPYDATASTRUCT.dwData</msdn>.
		/// </summary>
		public int DataId { get => (int)_dwData; set => _dwData = value; }

		/// <summary>
		/// Unmanaged data pointer. It is <msdn>COPYDATASTRUCT.lpData</msdn>.
		/// </summary>
		public byte* RawData { get => _lpData; set => _lpData = value; }

		/// <summary>
		/// Unmanaged data size. It is <msdn>COPYDATASTRUCT.cbData</msdn>.
		/// </summary>
		public int RawDataSize { get => _cbData; set => _cbData = value; }

		/// <summary>
		/// Gets received data as string.
		/// </summary>
		public string GetString() {
			return new string((char*)_lpData, 0, _cbData / 2);
		}

		/// <summary>
		/// Gets received data as <b>byte[]</b>.
		/// </summary>
		public byte[] GetBytes() {
			var a = new byte[_cbData];
			Marshal.Copy((IntPtr)_lpData, a, 0, a.Length);
			return a;
		}

		/// <summary>
		/// Calls API <msdn>ChangeWindowMessageFilter</msdn>(<b>WM_COPYDATA</b>). Then windows of this process can receive this message from lower [](xref:uac) integrity level processes.
		/// </summary>
		public static void EnableReceivingWM_COPYDATA() {
			Api.ChangeWindowMessageFilter(Api.WM_COPYDATA, 1);
		}

		#endregion

		#region send

		/// <summary>
		/// Sends string or other data to a window of any process. Uses API <msdn>SendMessage</msdn> <msdn>WM_COPYDATA</msdn>.
		/// </summary>
		/// <typeparam name="T">Type of data elements. For example, <b>char</b> for string, <b>byte</b> for <b>byte[]</b>.</typeparam>
		/// <param name="w">The window.</param>
		/// <param name="dataId">Data id. It is <msdn>COPYDATASTRUCT.dwData</msdn>.</param>
		/// <param name="data">Data. For example string or <b>byte[]</b>. String can contain <c>'\0'</c> characters.</param>
		/// <param name="wParam">Can be any value. Optional.</param>
		/// <returns><b>SendMessage</b>'s return value.</returns>
		public static unsafe nint Send<T>(wnd w, int dataId, ReadOnlySpan<T> data, nint wParam = 0) where T : unmanaged {
			fixed (T* p = data) {
				var c = new WndCopyData { _dwData = dataId, _cbData = data.Length * sizeof(T), _lpData = (byte*)p };
				return w.Send(Api.WM_COPYDATA, wParam, &c);
			}
		}

		/// <summary>
		/// Type of <see cref="SendReceive{TSend, TReceive}(wnd, int, ReadOnlySpan{TSend}, ResultReader{TReceive})"/> callback function.
		/// </summary>
		/// <param name="span">Received data buffer. The callback function can convert it to array, string, etc.</param>
		public delegate void ResultReader<TReceive>(ReadOnlySpan<TReceive> span) where TReceive : unmanaged;
		//compiler error if Action<ReadOnlySpan<TReceive>>.
		//could instead use System.Buffers.ReadOnlySpanAction, but then need TState, which is difficult to use for return, and nobody would use, and would not make faster etc.

		static readonly Lazy<IntPtr> s_mutex = new(Api.CreateMutex(null, false, "Au-mutex-WndUtil.Data")); //tested: don't need Api.SECURITY_ATTRIBUTES.ForLowIL

		/// <summary>
		/// Sends string or other data to a window of any process. Uses API <msdn>SendMessage</msdn> <msdn>WM_COPYDATA</msdn>.
		/// Receives string or other data returned by that window with <see cref="Return"/>.
		/// </summary>
		/// <typeparam name="TSend">Type of data elements. For example, <b>char</b> for string, <b>byte</b> for <b>byte[]</b></typeparam>
		/// <typeparam name="TReceive">Type of received data elements. For example, <b>char</b> for string, <b>byte</b> for <b>byte[]</b>.</typeparam>
		/// <param name="w">The window.</param>
		/// <param name="dataId">Data id. It is <msdn>COPYDATASTRUCT.dwData</msdn>.</param>
		/// <param name="send">Data to send. For example string or <b>byte[]</b>. String can contain <c>'\0'</c> characters.</param>
		/// <param name="receive">Callback function that can convert the received data to desired format.</param>
		/// <returns><c>false</c> if failed.</returns>
		public static unsafe bool SendReceive<TSend, TReceive>(wnd w, int dataId, ReadOnlySpan<TSend> send, ResultReader<TReceive> receive) where TSend : unmanaged where TReceive : unmanaged {
			var mutex = s_mutex.Value;
			if (Api.WaitForSingleObject(mutex, -1) is not (0 or Api.WAIT_ABANDONED_0)) return false;
			try {
				int len = (int)Send(w, dataId, send, Api.GetCurrentProcessId());
				if (len == 0) return false;
				var sm = SharedMemory_.ReturnDataPtr;
				if (len > 0) { //shared memory
					if (len <= SharedMemory_.ReturnDataSize) {
						receive(new ReadOnlySpan<TReceive>((TReceive*)sm, len / sizeof(TReceive)));
					} else {
						using var m2 = SharedMemory_.Mapping.CreateOrOpen(new((char*)sm), len);
						receive(new ReadOnlySpan<TReceive>((TReceive*)m2.Mem, len / sizeof(TReceive)));
					}
				} else { //process memory
					var pm = (void*)*(long*)sm;
					receive(new ReadOnlySpan<TReceive>((TReceive*)pm, -len / sizeof(TReceive)));
					bool ok = Api.VirtualFree(pm);
					Debug_.PrintIf(!ok, "VirtualFree");
				}
				return true;
			}
			finally { Api.ReleaseMutex(mutex); }
		}

		/// <summary>
		/// Calls <see cref="SendReceive{TSend, TReceive}(wnd, int, ReadOnlySpan{TSend}, ResultReader{TReceive})"/> and gets the received data as <b>byte[]</b>.
		/// </summary>
		/// <param name="received">The received data.</param>
		/// <inheritdoc cref="SendReceive{TSend, TReceive}(wnd, int, ReadOnlySpan{TSend}, ResultReader{TReceive})"/>
		public static bool SendReceive<TSend>(wnd w, int dataId, ReadOnlySpan<TSend> send, out byte[] received) where TSend : unmanaged {
			byte[] r = null;
			bool R = SendReceive<TSend, byte>(w, dataId, send, span => r = span.ToArray());
			received = r;
			return R;
		}

		/// <summary>
		/// Calls <see cref="SendReceive{TSend, TReceive}(wnd, int, ReadOnlySpan{TSend}, ResultReader{TReceive})"/> and gets the received string.
		/// </summary>
		/// <param name="received">The received data.</param>
		/// <inheritdoc cref="SendReceive{TSend, TReceive}(wnd, int, ReadOnlySpan{TSend}, ResultReader{TReceive})"/>
		public static bool SendReceive<TSend>(wnd w, int dataId, ReadOnlySpan<TSend> send, out string received) where TSend : unmanaged {
			string r = null;
			bool R = SendReceive<TSend, char>(w, dataId, send, span => r = span.ToString());
			received = r;
			return R;
		}

		/// <summary>
		/// Returns data to <see cref="SendReceive"/>.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="length"></param>
		/// <param name="wParam"><i>wParam</i> of the received <b>WM_COPYDATA</b> message. Important, pass unchanged.</param>
		/// <returns>Your window procedure must return this value.</returns>
		public static unsafe int Return(void* data, int length, nint wParam) {
			var sm = SharedMemory_.ReturnDataPtr;

			//use shared memory of this library. Max 1 MB.
			if (length <= SharedMemory_.ReturnDataSize) {
				MemoryUtil.Copy(data, sm, length);
				return length;
			}

			//allocate memory in caller process
			using var pm = new ProcessMemory((int)wParam, length, noException: true);
			if (pm.ProcessHandle != default) { //fails if that process has higher UAC IL. Rare.
				pm.Write(data, length);
				*(long*)sm = (long)pm.Mem;
				pm.MemAllocated = default;
				return -length;
			}

			//allocate new shared memory
			try {
				var smname = "Au-memory-" + Guid.NewGuid().ToString();
				fixed (char* p = smname) MemoryUtil.Copy(p, sm, smname.Length * 2 + 2);
				var m2 = SharedMemory_.Mapping.CreateOrOpen(smname, length);
				MemoryUtil.Copy(data, m2.Mem, length);
				Task.Run(() => { //wait until caller returns and then close the shared memory in this process
					var mutex = s_mutex.Value;
					if (Api.WaitForSingleObject(mutex, -1) is not (0 or Api.WAIT_ABANDONED_0)) { Debug_.Print("WaitForSingleObject"); return; }
					Api.ReleaseMutex(mutex);
					m2.Dispose();
				});
				return length;
			}
			catch { return 0; }

			//speed when size 1 MB and hot CPU:
			//	shared memory: 1000 mcs
			//	process memory: 1500 mcs
			//	shared memory 2: 2500 mcs
		}

		/// <summary>
		/// Returns string or other data to <see cref="SendReceive"/>.
		/// </summary>
		/// <typeparam name="T">Type of data elements. For example, <b>char</b> for string, <b>byte</b> for <b>byte[]</b></typeparam>
		/// <param name="data"></param>
		/// <param name="wParam"><i>wParam</i> of the received <b>WM_COPYDATA</b> message. Important, pass unchanged.</param>
		/// <returns>Your window procedure must return this value.</returns>
		public static unsafe int Return<T>(ReadOnlySpan<T> data, nint wParam) where T : unmanaged {
			fixed (T* f = data) return Return(f, data.Length * sizeof(T), wParam);
		}

		//rejected. Don't need too many not important overloads. Good: in most cases data size is 2 times smaller. Same: speed.
		//[SkipLocalsInit]
		//public static unsafe int ReturnStringUtf8_(RStr data, nint wParam) {
		//	var e = Encoding.UTF8;
		//	using var b = new FastBuffer<byte>(e.GetByteCount(data));
		//	int len = e.GetBytes(data, new Span<byte>(b.p, b.n));
		//	return ReturnData_(b.p, len, wParam);
		//}

		#endregion
	}
}
