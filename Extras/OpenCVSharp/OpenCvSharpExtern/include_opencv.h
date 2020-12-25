#ifndef _INCLUDE_OPENCV_H_
#define _INCLUDE_OPENCV_H_

//#define ENABLED_CONTRIB
//#undef ENABLED_CONTRIB

#ifndef CV_EXPORTS
# if (defined _WIN32 || defined WINCE || defined __CYGWIN__)
#   define CV_EXPORTS __declspec(dllexport)
# elif defined __GNUC__ && __GNUC__ >= 4 && defined(__APPLE__)
#   define CV_EXPORTS __attribute__ ((visibility ("default")))
# endif
#endif

#ifndef CV_EXPORTS
# define CV_EXPORTS
#endif

#ifdef _MSC_VER
#define NOMINMAX
#define _CRT_SECURE_NO_WARNINGS
#pragma warning(push)
#pragma warning(disable: 4251)
#pragma warning(disable: 4996)
#endif

#define OPENCV_TRAITS_ENABLE_DEPRECATED

#include <opencv2/opencv.hpp>

// MP! Added: To correctly support imShow under WinRT.
#ifdef _WINRT_DLL

#endif
#include <opencv2/imgproc/imgproc_c.h>

#ifndef _WINRT_DLL

#endif

// opencv_contrib


#include <vector>
#include <algorithm>
#include <iterator>
#include <sstream>
#include <iterator>
#include <fstream>
#include <iostream>
#include <cstdio>
#include <cstring>
#include <cstdlib>
#ifdef _MSC_VER
#pragma warning(pop)
#endif

// Additional types
#include "my_types.h"

// Additional functions
#include "my_functions.h"

#endif
