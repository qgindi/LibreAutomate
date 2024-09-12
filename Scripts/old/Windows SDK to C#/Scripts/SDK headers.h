//we'll remove everything at the start of the intermediate file (added by clang) until this
#define AU

//#define TEST_PREPROCESSING
#ifdef TEST_PREPROCESSING

/*
*/

#endif


//#define __cplusplus 201103L //clang defines this
#define _MSC_VER 1900 //VS2015
#define _MSC_FULL_VER 190023026
#define _MSC_EXTENSIONS 1

#define _UNICODE 1
#define UNICODE 1
#define _WCHAR_T_DEFINED 1
#define _MT 1
#define _DLL 1
#define _WIN32 1 //defined for 32 and 64

#ifdef USE64BIT
#define _WIN64 1
#define _M_AMD64 100
#define _M_X64 100
#else
#define _M_IX86 600
#endif
#define _INTEGRAL_MAX_BITS 64 //need if 32

#define WINVER 0x0A00 //Win10
#define _WIN32_WINNT 0x0A00
#define _WIN32_IE 0x0B00 //IE11

//add __int128 struct, because C# does not have a matching type
struct __int128 { float a, b, c, d; };
struct __int128d { float a, b, c, d; };
struct __int128i { float a, b, c, d; };

//prevent adding XML interfaces, but need some for parameters
#define __msxml_h__
#define __msxml2_h__
#define __msxml6_h__
typedef IntPtr IXMLElement;
typedef IntPtr IXMLDOMDocument;

//prevent adding D3D9 interfaces
#define _D3D9_H_
typedef IntPtr IDirect3DDevice9;

//add some that are in headers that we remove
#define size_t LPARAM
#define intptr_t LPARAM
#define uintptr_t LPARAM
#define time_t LPARAM
typedef void* va_list;
typedef struct _iobuf { char *_ptr; int _cnt; char *_base; int _flag; int _file; int _charbuf; int _bufsiz; char *_tmpfname; } FILE;

#define WIN32_LEAN_AND_MEAN //don't include come rarely used headers in windows.h
#define USE_COM_CONTEXT_DEF //IContext etc

#include <windows.h>

#include <regstr.h> //registry strings
#include <ole2.h> //OLE. Includes objbase.h, oleauto.h, urlmon.h, ...
#include <commctrl.h> //common controls
#include <shlobj.h> //shell
#include <shellapi.h> //shell
#include <shlwapi.h> //misc string functions etc
#include <olectl.h> //some OLE API
#include <oleacc.h> //MSAA accessible objects
#include <UIAutomation.h> //UI Automation
#include <mmsystem.h> //multimedia etc
#include <tlhelp32.h> //process info etc
#include <psapi.h> //process info etc
#include <wtsapi32.h> //user sessions etc
#include <winperf.h> //something used with PDH
#include <pdh.h> //performance counters //future: test System.Diagnostics.PerformanceCounter
#include <uxtheme.h> //visual styles
#include <vsstyle.h> //visual styles
#include <dwmapi.h> //DWM
#include <htmlhelp.h> //HTML help
#include <winioctl.h> //file IO low-level
#include <aclapi.h> //security //future: test System.Security.AccessControl etc
#include <sddl.h> //security strings
#include <taskschd.h> //Task Scheduler
#include <ShellScalingAPI.h> //high DPI
#include <appmodel.h> //Windows Store apps
#include <winternl.h> //Windows semi-internal API, eg NtX, RtlX
#include <iepmapi.h> //IE protected mode
#include <userenv.h> //user profiles, CreateEnvironmentBlock
#include <netlistmgr.h> //network list
#include <winnetwk.h> //networking
#include <propkey.h> //property key GUIDs
#include <cderr.h> //commdlg errors
#include <commdlg.h> //common dialogs
#include <dde.h> //DDE
#include <ddeml.h> //DDE
#define _WINSOCK_DEPRECATED_NO_WARNINGS
#include <winsock2.h> //sockets (TCP etc) //future: test System.Net.Sockets
#include <nspapi.h> //MS extensions for sockets
#include <richedit.h> //rich edit control
#include <usp10.h> //text drawing low-level
#include <dbt.h> //const/struct for WM_DEVICECHANGE
#include <ktmw32.h> //transactions
#define _MSI_NO_CRYPTO //don't include cryptography
#include <msi.h> //installer. MsiGetShortcutTarget, MsiGetComponentPath.
#include <commoncontrols.h> //IImageList
#include <powrprof.h> //power profiles, SetSuspendState
#define _LMAUDIT_ //don't include lmaudit.h, has name conflicts
#include <lm.h> //LAN management, NetServerEnum
#include <Wlanapi.h> //WiFi
#include <adhoc.h> //WiFi
#include <Pla.h> //performance logs and alerts
#include <werapi.h> //error reporting
#include <physicalmonitorenumerationapi.h>
#include <propvarutil.h>

//core audio API
#include <Audioclient.h>
#include <Audiopolicy.h>
#include <Mmdeviceapi.h>
#include <Devicetopology.h>
#include <Endpointvolume.h>

#if 0
//temporary. Used in the library, but don't need to include in the public database.
//#include <sapi.h> //note: will be several errors when converting. Edit the preprocessed files in several places.
////#include <sphelper.h> //C++ code, impossible to convert

//these are little tested and probably not useful
#include <iphlpapi.h> //IP helper, eg list network adapters. Use System.Net.NetworkInformation namespace.
#include <icmpapi.h> //ICMP, eg to implement a Ping(). Use System.Net.NetworkInformation namespace.
#include <GL/gl.h> //openGL
#include <GL/glu.h> //openGL util

//these are never tested but seem interesting
#include <RestartManager.h> //restart manager
#include <Clfsw32.h> //common log file system. .NET has something similar in System.Diagnostics.
#include <Clfsmgmtw32.h> //common log file management. .NET has something similar in System.Diagnostics.
#include <WinEvt.h> //event log. .NET has something similar in System.Diagnostics.
#include <Winsxs.h> //several interfaces for side-by-side assembly management

//these probably should not be added
#include <shldisp.h> //shell IDispatch interfaces
#include <scrnsave.h> //screen saver
#include <dbghelp.h> //debug help library
#include <imagehlp.h> //many the same as dbghelp.h. Includes wintrust.h -> wincrypt.h which is not welcome.
#include <setupapi.h> //obsolete
#include <mapi.h> //only for MS Outlook. Can instead use Outlook COM.
#include <snmp.h> //can instead download a SNMP .NET library
#include Sspi.h //authentication. Includes Security.h. Use .NET classes.
//these would be included if no WIN32_LEAN_AND_MEAN
#include <winefs.h> //encrypted file system. .NET can encrypt/decrypt a file, but maybe this also can be useful.
#include <winspool.h> //printer API. Big. Use System.Drawing.Printing namespace. See also System.Printing namespace.
#include <wincrypt.h> //cryptography. Large. Use System.Security.Cryptography namespace.
#include <Esent.h> //Windows native database API. Use ESENT.NET instead; tested, slooow, better use SQLite.
#include <wininet.h> //internet. We can instead use System.Net.Http namespace and System.Net.FtpWebRequest classes, but maybe this still can be useful.
#include <uiribbon.h>
//#include <mscoree.h> //unmanaged .NET API. Somehow this file disappeared.
#endif
