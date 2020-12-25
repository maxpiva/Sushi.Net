function BuildForWindows($platform, $vcpkgPath, $runMsbuild) {

    #$ErrorActionPreference = "Stop"

    $buildDirectory = "build_win_${platform}"
    mkdir $buildDirectory -Force -ErrorAction Stop | Out-Null
    cd $buildDirectory
    pwd

    if ($platform -eq "x64") {
        $msbuildPlatform = "x64"
        $msmfFlag = "ON"
    } else {
        $msbuildPlatform = "Win32"
        $msmfFlag = "OFF" # opencv_videoio430.lib(cap_msmf.obj) : error LNK2001: unresolved external symbol _MFVideoFormat_H263 
    }

    cmake -G "Visual Studio 16 2019" `
          -A $msbuildPlatform `
          -D CMAKE_BUILD_TYPE=Release `
          -D CMAKE_INSTALL_PREFIX=install `
          -D INSTALL_C_EXAMPLES=OFF `
          -D INSTALL_PYTHON_EXAMPLES=OFF `
          -D BUILD_ANDROID_PROJECTS=OFF `
          -D BUILD_ANDROID_EXAMPLES=OFF `
          -D BUILD_DOCS=OFF `
          -D BUILD_WITH_DEBUG_INFO=OFF `
          -D BUILD_EXAMPLES=OFF `
          -D BUILD_TESTS=OFF `
          -D BUILD_PERF_TESTS=OFF `
          -D BUILD_JAVA=OFF `
          -D BUILD_opencv_apps=OFF `
          -D BUILD_opencv_datasets=OFF `
          -D BUILD_opencv_gapi=OFF `
          -D BUILD_opencv_java_bindings_generator=OFF `
          -D BUILD_opencv_js=OFF `
          -D BUILD_opencv_python2=OFF `
          -D BUILD_opencv_python3=OFF `
          -D BUILD_opencv_python_bindings_generator=OFF `
          -D BUILD_opencv_python_tests=OFF `
          -D BUILD_opencv_ts=OFF `
          -D BUILD_opencv_world=OFF `
	  -D BUILD_opencv_calib3d=OFF `
	  -D BUILD_opencv_dnn=OFF `
	  -D BUILD_opencv_features2d=OFF `
	  -D BUILD_opencv_flann=OFF `
	  -D BUILD_opencv_highgui=OFF `
	  -D BUILD_opencv_imgcodecs=OFF `
	  -D BUILD_opencv_ml=OFF `
	  -D BUILD_opencv_objc=OFF `
	  -D BUILD_opencv_objdetect=OFF `
	  -D BUILD_opencv_photo=OFF `
	  -D BUILD_opencv_stitching=OFF `
	  -D BUILD_opencv_ts=OFF `
	  -D BUILD_opencv_video=OFF `
	  -D BUILD_opencv_videoio=OFF `
	  -D BUILD_PROTOBUF=OFF `
	  -D BUILD_ZLIB=OFF `
	  -D BUILD_ITT=OFF `
	  -D BUILD_IPP=OFF `
	  -D BUILD_IPP_IW=OFF `
          -D WITH_MSMF=OFF `
          -D WITH_MSMF_DXVA=OFF `
          -D WITH_QT=OFF `
          -D WITH_TESSERACT=OFF `
          -D WITH_OPENCL=OFF `
          -D WITH_GSTREAMER=OFF `
          -D WITH_DSHOW=OFF `
          -D WITH_1394=OFF `
          -D WITH_WIN32UI=OFF `
          -D WITH_PNG=OFF `
          -D WITH_JPEG=OFF `
          -D WITH_TIFF=OFF `
          -D WITH_WEBP=OFF `
          -D WITH_OPENJPEG=OFF `
          -D WITH_JASPER=OFF `
          -D WITH_OPENEXR=OFF `
          -D WITH_CLIP=OFF `
          -D WITH_DIRECTX=OFF `
          -D WITH_VA=OFF `
          -D WITH_LAPACK=OFF `
          -D WITH_QUIRC=OFF `
          -D WITH_ADE=OFF `
          -D WITH_PROTOBUF=OFF `
	  -D ENABLE_CXX11=1 `
          -D OPENCV_ENABLE_NONFREE=OFF `
          -D WITH_FFMPEG=OFF `
          -D BUILD_SHARED_LIBS=OFF ../opencv 

    if ($runMsbuild) {
        # Developer Powershell for VS 2019 
        # Path: C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -noe -c "&{Import-Module """C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\Tools\Microsoft.VisualStudio.DevShell.dll"""; Enter-VsDevShell cebe9bd5}"
        # WorkDir: C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\

        msbuild INSTALL.vcxproj /t:build /p:configuration=Release /p:platform=$msbuildPlatform -maxcpucount
        ls
    }

    cd ..
}


# Entry point
If ((Resolve-Path -Path $MyInvocation.InvocationName).ProviderPath -eq $MyInvocation.MyCommand.Path) {

  ##### Change here #####
  $vcpkgPath = "C:\Tools\vcpkg"
  $platform = "x64"
  #$platform = "x86"

  
  BuildForWindows $platform $vcpkgPath $TRUE
}