#include "stdafx.h"
#include "cpp.h"

//_________________________________________________________________

namespace jab
{

//From jni.h

typedef long jint;
typedef __int64 jlong;
typedef signed char jbyte;
typedef unsigned char   jboolean;
typedef unsigned short  jchar;
typedef short           jshort;
typedef float           jfloat;
typedef double          jdouble;
typedef int            jsize;

//_________________________________________________________________

//From AccessBridgePackages.h

typedef __int64 JObject;

#define MAX_BUFFER_SIZE   10240
#define MAX_STRING_SIZE   1024
#define SHORT_STRING_SIZE   256

// object types
typedef JObject AccessibleContext;
typedef JObject AccessibleText;
typedef JObject AccessibleValue;
typedef JObject AccessibleSelection;


/**
 ******************************************************
 *  optional AccessibleContext interfaces
 *
 * This version of the bridge reuses the accessibleValue
 * field in the AccessibleContextInfo struct to represent
 * additional optional interfaces that are supported by
 * the Java AccessibleContext.  This is backwardly compatable
 * because the old accessibleValue was set to the BOOL
 * value TRUE (i.e., 1) if the AccessibleValue interface is
 * supported.
 ******************************************************
 */

#define cAccessibleValueInterface (jlong) 1             // 1 << 1 (TRUE)
//#define cAccessibleActionInterface (jlong) 2            // 1 << 2
//#define cAccessibleComponentInterface (jlong) 4         // 1 << 3
//#define cAccessibleSelectionInterface (jlong) 8         // 1 << 4
//#define cAccessibleTableInterface (jlong) 16            // 1 << 5
//#define cAccessibleTextInterface (jlong) 32             // 1 << 6
//#define cAccessibleHypertextInterface (jlong) 64        // 1 << 7


typedef struct AccessibleContextInfoTag {
	wchar_t name[MAX_STRING_SIZE];          // the AccessibleName of the object
	wchar_t description[MAX_STRING_SIZE];   // the AccessibleDescription of the object

	wchar_t role[SHORT_STRING_SIZE];        // localized AccesibleRole string
	wchar_t role_en_US[SHORT_STRING_SIZE];  // AccesibleRole string in the en_US locale
	wchar_t states[SHORT_STRING_SIZE];      // localized AccesibleStateSet string (comma separated)
	wchar_t states_en_US[SHORT_STRING_SIZE]; // AccesibleStateSet string in the en_US locale (comma separated)

	int indexInParent;                     // index of object in parent
	int childrenCount;                     // # of children, if any

	int x;                                 // screen coords in pixels
	int y;                                 // "
	int width;                             // pixel width of object
	int height;                            // pixel height of object

	BOOL accessibleComponent;               // flags for various additional
	BOOL accessibleAction;                  //  Java Accessibility interfaces
	BOOL accessibleSelection;               //  FALSE if this object doesn't
	BOOL accessibleText;                    //  implement the additional interface
											//  in question

	BOOL accessibleInterfaces;              // bitfield containing additional interface flags

} AccessibleContextInfo;

// AccessibleText packages
typedef struct AccessibleTextInfoTag {
	int charCount;                 // # of characters in this text object
	int caretIndex;                // index of caret
	int indexAtPoint;              // index at the passsed in point
} AccessibleTextInfo;

#define MAX_ACTION_INFO 256
#define MAX_ACTIONS_TO_DO 32

// an action assocated with a component
typedef struct AccessibleActionInfoTag {
	wchar_t name[SHORT_STRING_SIZE];        // action name
} AccessibleActionInfo;

// all of the actions associated with a component
typedef struct AccessibleActionsTag {
	int actionsCount;              // number of actions
	AccessibleActionInfo actionInfo[MAX_ACTION_INFO];       // the action information
} AccessibleActions;

// list of AccessibleActions to do
typedef struct AccessibleActionsToDoTag {
	int actionsCount;                              // number of actions to do
	AccessibleActionInfo actions[MAX_ACTIONS_TO_DO];// the accessible actions to do
} AccessibleActionsToDo;

#define MAX_VISIBLE_CHILDREN 256

// visible children information
typedef struct VisibleChildenInfoTag {
	int returnedChildrenCount; // number of children returned
	AccessibleContext children[MAX_VISIBLE_CHILDREN]; // the visible children
} VisibleChildrenInfo;


//_________________________________________________________________

//From AccessBridgeCalls.h

//Loads Java Access Bridge (JAB) dll and functions. Then you can call the functions.
struct _JApi
{

	void(*Windows_run) ();

	//BOOL (*isJavaWindow) (HWND window); //very slow, and need to load dll etc

	void(*releaseJavaObject) (long vmID, JObject object);

	BOOL(*isSameObject) (long vmID, JObject obj1, JObject obj2);

	BOOL(*getAccessibleContextFromHWND) (HWND window, long *vmID, AccessibleContext *ac);

	HWND(*getHWNDFromAccessibleContext) (long vmID, AccessibleContext ac);

	BOOL(*getAccessibleContextAt) (long vmID, AccessibleContext acParent, jint x, jint y, AccessibleContext *ac);

	BOOL(*getAccessibleContextWithFocus) (HWND window, long *vmID, AccessibleContext *ac);

	BOOL(*getAccessibleContextInfo) (long vmID, AccessibleContext ac, AccessibleContextInfo *info);

	AccessibleContext(*getAccessibleChildFromContext) (long vmID, AccessibleContext ac, jint i);

	AccessibleContext(*getAccessibleParentFromContext) (long vmID, AccessibleContext ac);

	BOOL(*getAccessibleActions)(long vmID, AccessibleContext accessibleContext, AccessibleActions *actions);

	BOOL(*doAccessibleActions)(long vmID, AccessibleContext accessibleContext, AccessibleActionsToDo *actionsToDo, jint *failure);

	BOOL(*getAccessibleTextInfo) (long vmID, AccessibleText at, AccessibleTextInfo *textInfo, jint x, jint y);

	BOOL(*getAccessibleTextRange) (long vmID, AccessibleText at, jint start, jint end, wchar_t *text, short len);

	BOOL(*getCurrentAccessibleValueFromContext) (long vmID, AccessibleValue av, wchar_t *value, short len);

	void(*addAccessibleSelectionFromContext) (long vmID, AccessibleSelection as, int i);

	void(*clearAccessibleSelectionFromContext) (long vmID, AccessibleSelection as);

	void(*removeAccessibleSelectionFromContext) (long vmID, AccessibleSelection as, int i);

	BOOL(*setTextContents) (const long vmID, const AccessibleContext ac, const wchar_t *text);

	BOOL(*getVirtualAccessibleName) (const long vmID, const AccessibleContext accessibleContext, wchar_t *name, int len);

	BOOL(*requestFocus) (const long vmID, const AccessibleContext accessibleContext);

	//AccessibleContext(*getTopLevelObject) (const long vmID, const AccessibleContext ac);

	//AccessibleContext(*getParentWithRole) (const long vmID, const AccessibleContext ac, const wchar_t* role);

	//AccessibleContext(*getParentWithRoleElseRoot) (const long vmID, const AccessibleContext ac, const wchar_t* role);

	//AccessibleContext(*getActiveDescendent) (const long vmID, const AccessibleContext ac); //does not work

	//int (*getVisibleChildrenCount) (const long vmID, const AccessibleContext accessibleContext);

	//BOOL(*getVisibleChildren) (const long vmID, const AccessibleContext accessibleContext, const int startIndex, VisibleChildrenInfo* children); //slow

private:
	HMODULE _hmodule;
	bool _isLoaded;
public:

	_JApi() noexcept { ZEROTHIS; }

	~_JApi()
	{
		_isLoaded = false;
		if(_hmodule) {
			FreeLibrary(_hmodule);
			_hmodule = 0;
		}
	}

	bool IsLoaded() { return _isLoaded; }

	void Load()
	{
		if(_isLoaded) return;
		if(!IsOS64Bit()) return; //don't support 32-bit OS. Then JObject etc is 32-bit. Too much work for the dying platform.
		STR dllName = L"WindowsAccessBridge-"
#ifdef _WIN64
			L"64.dll";
#else
			L"32.dll";
#endif
		HMODULE hm = LoadLibraryW(dllName);
		if(hm == 0) {
			PRINTF(L"failed LoadLibrary: %s", dllName);
			return;
		}
		_hmodule = hm;
#define JF(f) *(FARPROC*)&f=GetProcAddress(hm, #f)
		JF(Windows_run);
		JF(releaseJavaObject);
		JF(isSameObject);
		JF(getAccessibleContextFromHWND);
		JF(getHWNDFromAccessibleContext);
		JF(getAccessibleContextAt);
		JF(getAccessibleContextWithFocus);
		JF(getAccessibleContextInfo);
		JF(getAccessibleChildFromContext);
		JF(getAccessibleParentFromContext);
		JF(getAccessibleActions);
		JF(doAccessibleActions);
		JF(getAccessibleTextInfo);
		JF(getAccessibleTextRange);
		JF(getCurrentAccessibleValueFromContext);
		JF(addAccessibleSelectionFromContext);
		JF(clearAccessibleSelectionFromContext);
		JF(removeAccessibleSelectionFromContext);
		JF(setTextContents);
		JF(getVirtualAccessibleName);
		JF(requestFocus);
		//JF(getTopLevelObject);
		//JF(getParentWithRole);
		//JF(getParentWithRoleElseRoot);
		//JF(getActiveDescendent);
		//JF(getVisibleChildrenCount);
		//JF(getVisibleChildren);

		for(FARPROC* p = (FARPROC*)&Windows_run, *pe = (FARPROC*)&_hmodule; p < pe; p++) if(*p == null) {
			PRINTS(L"Some JAB functions not found");
			return;
		}

		_isLoaded = true;
	}

};

} //namespace jab
