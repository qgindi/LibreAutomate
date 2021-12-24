// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#pragma once

// Including SDKDDKVer.h defines the highest available Windows platform.
// If you wish to build your application for a previous Windows platform, include WinSDKVer.h and set the _WIN32_WINNT macro to the platform you wish to support before including SDKDDKVer.h.
#include <SDKDDKVer.h>

#define WIN32_LEAN_AND_MEAN
#define _CRT_SECURE_NO_WARNINGS

#include <assert.h>
#include <string>
#include <algorithm>
#include <functional>
#include <initializer_list>

#include <comdef.h>
#include <comdefsp.h> //smart pointers of many interfaces

#include <atlbase.h>
#include <atlcoll.h>
//#include <concrt.h> //critical_section

#include <windows.h>
#include <Sddl.h>
#include <OleAuto.h>
#include <OleAcc.h>
#include <UIAutomation.h>
#include <shellapi.h>
#include <shlobj.h>
#include <shellscalingapi.h>

#define PCRE2_STATIC 1
#define PCRE2_CODE_UNIT_WIDTH 16
#include "..\Libraries\pcre\pcre2.h"



#define null nullptr
#define out
#define ref

using STR = LPCWSTR;

#pragma region enum operators
//http://blog.bitwigglers.org/using-enum-classes-as-type-safe-bitmasks/

template<typename Enum>
struct EnableBitMaskOperators
{
	static const bool enable = false;
};

#define ENABLE_BITMASK_OPERATORS(x) template<> struct EnableBitMaskOperators<x> { static const bool enable = true; }

template<typename Enum> constexpr
std::enable_if_t<EnableBitMaskOperators<Enum>::enable, Enum>
operator |(Enum lhs, Enum rhs)
{
	using underlying = typename std::underlying_type<Enum>::type;
	return static_cast<Enum> (static_cast<underlying>(lhs) | static_cast<underlying>(rhs));
}

template<typename Enum> constexpr
std::enable_if_t<EnableBitMaskOperators<Enum>::enable, Enum>
operator &(Enum lhs, Enum rhs)
{
	using underlying = typename std::underlying_type<Enum>::type;
	return static_cast<Enum> (static_cast<underlying>(lhs) & static_cast<underlying>(rhs));
}

template<typename Enum> constexpr
std::enable_if_t<EnableBitMaskOperators<Enum>::enable, Enum>
operator ^(Enum lhs, Enum rhs)
{
	using underlying = typename std::underlying_type<Enum>::type;
	return static_cast<Enum> (static_cast<underlying>(lhs) ^ static_cast<underlying>(rhs));
}

template<typename Enum>
std::enable_if_t<EnableBitMaskOperators<Enum>::enable, Enum>
& operator |=(Enum& lhs, Enum rhs)
{
	using underlying = typename std::underlying_type<Enum>::type;
	return lhs = static_cast<Enum> (static_cast<underlying>(lhs) | static_cast<underlying>(rhs));
}

template<typename Enum>
std::enable_if_t<EnableBitMaskOperators<Enum>::enable, Enum>
& operator &=(Enum& lhs, Enum rhs)
{
	using underlying = typename std::underlying_type<Enum>::type;
	return lhs = static_cast<Enum> (static_cast<underlying>(lhs) & static_cast<underlying>(rhs));
}

template<typename Enum>
std::enable_if_t<EnableBitMaskOperators<Enum>::enable, Enum>
& operator ^=(Enum& lhs, Enum rhs)
{
	using underlying = typename std::underlying_type<Enum>::type;
	return lhs = static_cast<Enum> (static_cast<underlying>(lhs) ^ static_cast<underlying>(rhs));
}

template<typename Enum> constexpr
std::enable_if_t<EnableBitMaskOperators<Enum>::enable, Enum>
operator ~(Enum rhs)
{
	using underlying = typename std::underlying_type<Enum>::type;
	return static_cast<Enum> (~static_cast<underlying>(rhs));
}

//compiler error: cannot be static
//template<typename Enum>
//operator bool(Enum rhs)
//{
//	using underlying = typename std::underlying_type<Enum>::type;
//	return static_cast<underlying>(rhs) != 0;
//}

template<typename Enum> constexpr
std::enable_if_t<EnableBitMaskOperators<Enum>::enable, bool>
operator !(Enum rhs)
{
	using underlying = typename std::underlying_type<Enum>::type;
	return 0 == static_cast<underlying>(rhs);
}

////Returns true if x has all flags.
//x can be any enum or integer type of size <= 32-bit.
//template<typename E>
//bool F(E x, E flags) {
//	static_assert(sizeof(E) <= 4);
//	return ((int)x&(int)flags) == (int)flags;
//}
//
////Returns true if x has any of flags.
////x can be any enum or integer type of size <= 32-bit.
//template<typename E>
//bool F_ANY(E x, E flags) {
//	static_assert(sizeof(E) <= 4);
//	return (int)x&((int)flags);
//}

#pragma endregion

