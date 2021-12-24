namespace Au.Compiler
{
	static partial class Compiler
	{
		unsafe class _Resources
		{
			class _Res
			{
				public byte[] data;
				public ushort resType, resId;
				//don't need to support language and string type/name

				public _Res(ushort resType, ushort resId, byte[] data) {
					this.data = data;
					this.resType = resType;
					this.resId = resId;
				}
			}

			List<_Res> _a = new();

			public void AddVersion(string asmFile) {
				var hModule = Api.LoadLibraryEx(asmFile, default, Api.LOAD_LIBRARY_AS_DATAFILE);
				if (hModule == default) _Throw();
				try { _a.Add(new _Res(16, 1, _LoadNativeResource(hModule, 16, 1))); } //RT_VERSION
				finally { Api.FreeLibrary(hModule); }
			}

			public void AddManifest(string manifestFile) {
				_a.Add(new _Res(24, 1, File.ReadAllBytes(manifestFile))); //RT_MANIFEST
			}

			public void AddIcon(string iconFile, ref ICONCONTEXT ic)
				=> AddIcon(File.ReadAllBytes(iconFile), ref ic);

			public void AddIcon(byte[] iconBytes, ref ICONCONTEXT ic) {
				//info:
				//An icon file begins with NEWHEADER followed by multiple ICONDIRENTRY for each icon.
				//In resource instead of ICONDIRENTRY is RESDIR, where DWORD dwImageOffset ir replaced with WORD id.
				//Note that sizeof(ICONDIRENTRY) is 16 and sizeof(RESDIR) is 14.
				//In icon file then follow multiple ICONIMAGE (or png). Their file offsets are ICONDIRENTRY.dwImageOffset.
				//In resources instead there are multiple RT_ICON resources that are referenced
				//	by RESDIR.wIconCursorId. An RT_ICON resource contains ICONIMAGE or png.

				if (ic.groupId == 0) ic.groupId = Api.IDI_APPLICATION;

				var m = new MemoryStream();
				fixed (byte* mem = iconBytes) {
					if (iconBytes.Length <= sizeof(Api.NEWHEADER)) _Throw();
					var ph = (Api.NEWHEADER*)mem;
					var pi = (Api.ICONDIRENTRY*)(ph + 1);
					int n = ph->wResCount;
					if (iconBytes.Length <= sizeof(Api.NEWHEADER) + n * sizeof(Api.ICONDIRENTRY)) _Throw();
					m.Write(new ReadOnlySpan<byte>(ph, sizeof(Api.NEWHEADER)));

					for (int i = 0; i < n; i++) {
						Api.ICONDIRENTRY* ide = pi + i;
						int offset = ide->dwImageOffset, size = ide->dwBytesInRes;
						if (offset + size > iconBytes.Length) _Throw();
						ushort id = (ushort)(++ic.iconId);
						ide->dwImageOffset = id; //RESDIR.wIconCursorId
						m.Write(new ReadOnlySpan<byte>(ide, sizeof(Api.ICONDIRENTRY) - 2)); //ICONDIRENTRY to RESDIR

						_a.Add(new _Res(3, id, new ReadOnlySpan<byte>(mem + offset, size).ToArray())); //RT_ICON
					}
				}

				_a.Add(new _Res(14, (ushort)ic.groupId++, m.ToArray())); //RT_GROUP_ICON
			}

			public struct ICONCONTEXT { public int groupId, iconId; }

			//rejected. Rarely used. If need, users can add later, with tools like ResourceHacker. Here makes more difficult because need to support string type/name, language, etc.
			//public void AddRes(string resFile)
			//{

			//}

			static void _Throw() {
				throw new AuException("*add resources");
			}

			static byte[] _LoadNativeResource(IntPtr hModule, ushort resType, ushort resId) {
				if (!_LoadNativeResource(hModule, resType, resId, out var data, out var size)) _Throw();
				return new ReadOnlySpan<byte>(data, size).ToArray();
			}

			static bool _LoadNativeResource(IntPtr hModule, ushort resType, ushort resId, out byte* ptr, out int size) {
				ptr = null; size = 0;
				var hRes = Api.FindResource(hModule, resId, resType); if (hRes == default) return false;
				var hGlob = LoadResource(hModule, hRes); if (hGlob == default) return false;
				ptr = LockResource(hGlob);
				size = SizeofResource(hModule, hRes);
				return ptr != null && size > 0;
				//info: don't need to free or unlock. It is documented.
			}

			public void WriteAll(string exeFile, byte[] exeData, bool bit32, bool console) {
				fixed (byte* pBase = exeData) {
					IMAGE_NT_HEADERS64* nth = ImageNtHeader(pBase); if (nth == null) _Throw();
					var oh = &nth->OptionalHeader;
					uint fileAlignment = oh->FileAlignment;
					uint resRva = oh->SizeOfImage;

					if (fileAlignment != 512
						|| (uint)exeData.Length % fileAlignment != 0
						|| exeData.Length < 2048
						|| oh->DataDirectory_Resource.VirtualAddress != 0 //must not contain resources
						) _Throw();

					var m = new MemoryStream();
					m.Position = exeData.Length; //reserve for updated exeData
					if (!_WriteResources(m, resRva)) _Throw();
					uint resSize = (uint)(m.Length - exeData.Length), resSizeAligned = Math2.AlignUp(resSize, fileAlignment);
					m.SetLength((uint)m.Length + resSizeAligned);

					//The exe template does not contain resources, therefore .rsrc section does not exist and ImageDirectoryEntryToDataEx fails.
					//Could add a placeholder resource, but then it is not the last section...
					//But we can simply append new section header here. There is empty space, because sections are aligned at 0x400.
					var ish = (IMAGE_SECTION_HEADER*)((byte*)oh + nth->FileHeader.SizeOfOptionalHeader + nth->FileHeader.NumberOfSections * sizeof(IMAGE_SECTION_HEADER));
					long ishOffset = (byte*)ish - pBase; if (ishOffset <= 512 || ishOffset > 1024 - sizeof(IMAGE_SECTION_HEADER)) _Throw();

					//write IMAGE_SECTION_HEADER of .rsrc section
					for (int i = 0; i < 5; i++) ish->Name[i] = (byte)".rsrc"[i];
					ish->VirtualSize = resSize;
					ish->VirtualAddress = resRva;
					ish->SizeOfRawData = Math2.AlignUp(resSize, fileAlignment);
					ish->PointerToRawData = (uint)exeData.Length;
					ish->Characteristics = 0x40000040;

					uint resSize2 = Math2.AlignUp(resSize, oh->SectionAlignment);
					if (!bit32) {
						//in IMAGE_OPTIONAL_HEADER write RVA/size of .rsrc section
						oh->DataDirectory_Resource.VirtualAddress = resRva;
						oh->DataDirectory_Resource.Size = resSize;

						//update some other fields
						oh->SizeOfInitializedData += resSizeAligned;
						oh->SizeOfImage += resSize2;
						if (console) oh->Subsystem = 3; //IMAGE_SUBSYSTEM_WINDOWS_CUI
						oh->CheckSum = 0;
					} else {
						var oh32 = (IMAGE_OPTIONAL_HEADER32*)oh;
						oh32->DataDirectory_Resource.VirtualAddress = resRva;
						oh32->DataDirectory_Resource.Size = resSize;
						oh32->SizeOfInitializedData += resSizeAligned;
						oh32->SizeOfImage += resSize2;
						if (console) oh32->Subsystem = 3;
						oh32->CheckSum = 0;
					}
					nth->FileHeader.NumberOfSections++;
					nth->FileHeader.TimeDateStamp = 0;

					//write updated exeData to stream
					m.Position = 0;
					m.Write(exeData);
					m.Position = 0;

					using var fs = new FileStream(exeFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
					m.CopyTo(fs);
				}
			}

			bool _WriteResources(Stream m, uint resRva) {
				//calc number of resource types and ids (sum of unique ids of all types)
				//We can have max 1 version, 1 manifest and multiple RT_ICONGROUP (each + its multiple RT_ICON).

				_a.Sort((v1, v2) => {
					int r = v1.resType - v2.resType;
					if (r == 0) r = v1.resId - v2.resId;
					return r;
				});

				int nTypes = 0, nIds = 0, prevType = -1, prevId = -1;
				foreach (var r in _a) {
					if (r.resType != prevType) { prevType = r.resType; nTypes++; prevId = -1; }
					if (r.resId != prevId) { prevId = r.resId; nIds++; }
				}

				//calc offsets and size of all directories
				int sizeResDir = sizeof(IMAGE_RESOURCE_DIRECTORY);
				int sizeDirEntry = sizeof(IMAGE_RESOURCE_DIRECTORY_ENTRY);
				int baseNameIdDir = sizeResDir + nTypes * sizeDirEntry;
				int baseLangDir = baseNameIdDir + sizeResDir * nTypes + nIds * sizeDirEntry;
				int baseDataDir = baseLangDir + sizeResDir * nIds + sizeDirEntry * _a.Count;
				int baseResData = baseDataDir + _a.Count * sizeof(IMAGE_RESOURCE_DATA_ENTRY);
				int offsData = baseResData; //temp

				//create and write directories
				var aDir = new byte[baseResData];
				fixed (byte* pDir = aDir) {
					var dT = (IMAGE_RESOURCE_DIRECTORY*)pDir;
					var eT = (IMAGE_RESOURCE_DIRECTORY_ENTRY*)(dT + 1);
					IMAGE_RESOURCE_DIRECTORY* dN = null, dL = null;
					IMAGE_RESOURCE_DIRECTORY_ENTRY* eN = null, eL = null;
					prevType = -1; prevId = -1;
					foreach (var r in _a) {
						if (r.resType != prevType) {
							prevType = r.resType;
							prevId = -1;

							//write resource type entry
							dT->NumberOfIdEntries++;
							eT->Id = r.resType;
							eT->OffsetToData = baseNameIdDir | unchecked((int)0x80000000);
							eT++;

							//write directory of resource ids
							dN = (IMAGE_RESOURCE_DIRECTORY*)(pDir + baseNameIdDir);
							baseNameIdDir += sizeResDir;
							eN = (IMAGE_RESOURCE_DIRECTORY_ENTRY*)(dN + 1);
						}

						if (r.resId != prevId) {
							prevId = r.resId;

							//write resource id entry
							dN->NumberOfIdEntries++;
							eN->Id = r.resId;
							eN->OffsetToData = baseLangDir | unchecked((int)0x80000000);
							eN++;
							baseNameIdDir += sizeDirEntry;

							//write directory of languages
							dL = (IMAGE_RESOURCE_DIRECTORY*)(pDir + baseLangDir);
							baseLangDir += sizeResDir;
							eL = (IMAGE_RESOURCE_DIRECTORY_ENTRY*)(dL + 1);
						}

						//write language entry
						dL->NumberOfIdEntries++;
						eL->Id = 0;
						eL->OffsetToData = baseDataDir;
						eL++;
						baseLangDir += sizeDirEntry;

						//write data entry
						var de = (IMAGE_RESOURCE_DATA_ENTRY*)(pDir + baseDataDir);
						baseDataDir += sizeof(IMAGE_RESOURCE_DATA_ENTRY);
						de->Size = (uint)r.data.Length;
						de->OffsetToData = resRva + (uint)offsData; //RVA
						offsData += Math2.AlignUp(r.data.Length, 4);
					}
				}
				m.Write(aDir);

				//write resource data
				foreach (var r in _a) {
					m.Write(r.data);
					for (int k = Math2.AlignUp(r.data.Length, 4) - r.data.Length; k > 0; k--) m.WriteByte(0); //4-align
				}

				return true;
			}

			[DllImport("kernel32.dll")]
			internal static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

			[DllImport("kernel32.dll")]
			internal static extern byte* LockResource(IntPtr hResData);

			[DllImport("kernel32.dll")]
			internal static extern int SizeofResource(IntPtr hModule, IntPtr hResInfo);

			[DllImport("dbghelp.dll")]
			internal static extern IMAGE_NT_HEADERS64* ImageNtHeader(byte* Base);

#pragma warning disable 649 //field never assigned

			internal struct IMAGE_SECTION_HEADER
			{
				public fixed byte Name[8];
				public uint VirtualSize;
				public uint VirtualAddress;
				public uint SizeOfRawData;
				public uint PointerToRawData;
				public uint PointerToRelocations;
				public uint PointerToLinenumbers;
				public ushort NumberOfRelocations;
				public ushort NumberOfLinenumbers;
				public uint Characteristics;
			}

			internal struct IMAGE_DATA_DIRECTORY
			{
				public uint VirtualAddress;
				public uint Size;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 4)]
			internal struct IMAGE_OPTIONAL_HEADER32
			{
				public ushort Magic;
				public byte MajorLinkerVersion;
				public byte MinorLinkerVersion;
				public uint SizeOfCode;
				public uint SizeOfInitializedData;
				public uint SizeOfUninitializedData;
				public uint AddressOfEntryPoint;
				public uint BaseOfCode;
				public uint BaseOfData;
				public uint ImageBase;
				public uint SectionAlignment;
				public uint FileAlignment;
				public ushort MajorOperatingSystemVersion;
				public ushort MinorOperatingSystemVersion;
				public ushort MajorImageVersion;
				public ushort MinorImageVersion;
				public ushort MajorSubsystemVersion;
				public ushort MinorSubsystemVersion;
				public uint Win32VersionValue;
				public uint SizeOfImage;
				public uint SizeOfHeaders;
				public uint CheckSum;
				public ushort Subsystem;
				public ushort DllCharacteristics;
				public uint SizeOfStackReserve;
				public uint SizeOfStackCommit;
				public uint SizeOfHeapReserve;
				public uint SizeOfHeapCommit;
				public uint LoaderFlags;
				public uint NumberOfRvaAndSizes;
				IMAGE_DATA_DIRECTORY _d0, _d1;
				public IMAGE_DATA_DIRECTORY DataDirectory_Resource;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 4)]
			internal struct IMAGE_OPTIONAL_HEADER64
			{
				public ushort Magic;
				public byte MajorLinkerVersion;
				public byte MinorLinkerVersion;
				public uint SizeOfCode;
				public uint SizeOfInitializedData;
				public uint SizeOfUninitializedData;
				public uint AddressOfEntryPoint;
				public uint BaseOfCode;
				public ulong ImageBase;
				public uint SectionAlignment;
				public uint FileAlignment;
				public ushort MajorOperatingSystemVersion;
				public ushort MinorOperatingSystemVersion;
				public ushort MajorImageVersion;
				public ushort MinorImageVersion;
				public ushort MajorSubsystemVersion;
				public ushort MinorSubsystemVersion;
				public uint Win32VersionValue;
				public uint SizeOfImage;
				public uint SizeOfHeaders;
				public uint CheckSum;
				public ushort Subsystem;
				public ushort DllCharacteristics;
				public ulong SizeOfStackReserve;
				public ulong SizeOfStackCommit;
				public ulong SizeOfHeapReserve;
				public ulong SizeOfHeapCommit;
				public uint LoaderFlags;
				public uint NumberOfRvaAndSizes;
				IMAGE_DATA_DIRECTORY _d0, _d1;
				public IMAGE_DATA_DIRECTORY DataDirectory_Resource;
			}

			internal struct IMAGE_FILE_HEADER
			{
				public ushort Machine;
				public ushort NumberOfSections;
				public uint TimeDateStamp;
				public uint PointerToSymbolTable;
				public uint NumberOfSymbols;
				public ushort SizeOfOptionalHeader;
				public ushort Characteristics;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 4)]
			internal struct IMAGE_NT_HEADERS64
			{
				public uint Signature;
				public IMAGE_FILE_HEADER FileHeader;
				public IMAGE_OPTIONAL_HEADER64 OptionalHeader;
			}

			internal struct IMAGE_RESOURCE_DIRECTORY
			{
				public uint Characteristics;
				public uint TimeDateStamp;
				public ushort MajorVersion;
				public ushort MinorVersion;
				public ushort NumberOfNamedEntries;
				public ushort NumberOfIdEntries;
			}

			internal struct IMAGE_RESOURCE_DIRECTORY_ENTRY
			{
				//simplified, does not support name as string
				public ushort Id;
				public int OffsetToData;
			}

			internal struct IMAGE_RESOURCE_DATA_ENTRY
			{
				public uint OffsetToData;
				public uint Size;
				public uint CodePage;
				public uint Reserved;
			}

			//Icon resource entry (part of RT_GROUP_ICON)
			//[StructLayout(LayoutKind.Sequential, Pack = 2)]
			//struct RESDIR
			//{
			//	public ICONRESDIR ird;
			//	public ushort wPlanes;
			//	public ushort wBitCount;
			//	public uint dwBytesInRes;
			//	public ushort wIconCursorId;
			//};

			//Icon image in file. RT_ICON resource is the same.
			//[StructLayout(LayoutKind.Sequential, Pack = 2)]
			//struct ICONIMAGE
			//{
			//	public BITMAPINFOHEADER icHeader;
			//	public RGBQUAD icColors[1];
			//	public byte icXOR[1];
			//	public byte icAND[1];
			//};
		}
	}
}
