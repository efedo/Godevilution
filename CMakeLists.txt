cmake_minimum_required(VERSION 3.22)

foreach(_policy CMP0135 CMP0141)
  if(POLICY ${_policy})
    cmake_policy(SET ${_policy} NEW)
    set(CMAKE_POLICY_DEFAULT_${_policy} NEW)
  endif()
endforeach()

# Projects added via `add_subdirectory` or `FetchContent` may have a lower
# `cmake_minimum_required` than we set here. Set policies that we require
# to their new value so that they still apply.
set(CMAKE_POLICY_DEFAULT_CMP0069 NEW)
set(CMAKE_POLICY_DEFAULT_CMP0077 NEW)

if(IS_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/dist")
  message("-- Detected a source distribution with the required FetchContent dependencies and devilutionx.mpq included")
  set(SRC_DIST ON)
  add_subdirectory(dist)
endif()

if(${CMAKE_SOURCE_DIR} STREQUAL ${CMAKE_BINARY_DIR})
  message(WARNING [[In-source build detected, please eg. create a new directory and use `cmake ..`]])
endif()

include(CMakeDependentOption)

list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/CMake")
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/CMake/finders")
include(functions/genex)

# Options required by `VcPkgManifestFeatures`, which must be included before the `project` call.
option(USE_SDL1 "Use SDL1.2 instead of SDL2" OFF)
option(USE_SDL3 "Use SDL3 instead of SDL2" OFF)
option(NONET "Disable network support" OFF)
cmake_dependent_option(PACKET_ENCRYPTION "Encrypt network packets" ON "NOT NONET" OFF)
option(DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT "Build the opt-in C++ client for the C# authoritative server" OFF)
option(DEVILUTIONX_ENABLE_AUTHORITATIVE_CLIENT "Deprecated alias for DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT" OFF)
if(DEVILUTIONX_ENABLE_AUTHORITATIVE_CLIENT)
  set(DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT ON CACHE BOOL "Build the opt-in C++ client for the C# authoritative server" FORCE)
endif()
# The gettext[tools] package takes a very long time to install
if(CMAKE_TOOLCHAIN_FILE MATCHES "vcpkg.cmake$")
  option(USE_GETTEXT_FROM_VCPKG "Add vcpkg dependency for gettext[tools] for compiling translations" OFF)
endif()
option(BUILD_TESTING "Build tests." ON)

# These must be included after the options above but before the `project` call.
include(VcPkgManifestFeatures)

# Set up the `project` before the rest of the options so that:
#
# 1. Properties such as `TARGET_SUPPORTS_SHARED_LIBS` are defined.
# 2. Toolchain file is evaluated, required for `Platforms.cmake`,
#    which can override the options.
file(STRINGS "VERSION" VERSION_STR)
if(NOT "${VERSION_STR}" STREQUAL "")
  string(REGEX MATCH "([0-9]+\\.[0-9]+\\.[0-9]+).*" VERSION_PREFIX ${VERSION_STR})
  set(VERSION_NUM ${CMAKE_MATCH_1})
endif()

if(NOT VERSION_SUFFIX)
  # For versions with a suffix, e.g. "1.5.0-dev", include
  # the build type and the git hash.
  set(VERSION_SUFFIX "-$<CONFIG>")
  if(VERSION_PREFIX MATCHES "-")
    if(NOT GIT_COMMIT_HASH)
      include(functions/git)
      get_git_commit_hash(GIT_COMMIT_HASH)
    endif()
    if(GIT_COMMIT_HASH)
      set(VERSION_SUFFIX "${VERSION_SUFFIX}-${GIT_COMMIT_HASH}")
    endif()
  else()
    # For versions without a suffix, e.g. "1.5.0" rather than "1.5.0-dev",
    # only include the build type if it is a debug build.
    set(VERSION_SUFFIX "$<$<CONFIG:Debug>:$<CONFIG>>")
  endif()
endif()

project(DevilutionX
  VERSION ${VERSION_NUM}
  LANGUAGES C CXX)
set(PROJECT_VERSION_WITH_SUFFIX "${VERSION_PREFIX}${VERSION_SUFFIX}")

# Platform definitions can override options and we want `cmake_dependent_option` to see the effects.
# Note that a few options are still defined before this because they're needed by `VcPkgManifestFeatures.cmake`.
include(Platforms)

# This built-in CMake module adds a BUILD_TESTING option (ON by default).
# Must be included in the top-level `CMakeLists.txt` after calling `project`.
# Because we must include `VcPkgManifestFeatures` before the `project` call,
# we add a BUILD_TESTING option ourselves above as well.
include(CTest)

# Debugging / profiling options
DEBUG_OPTION(ASAN "Enable address sanitizer")
DEBUG_OPTION(UBSAN "Enable undefined behaviour sanitizer")
option(TSAN "Enable thread sanitizer (not compatible with ASAN=ON)" OFF)
DEBUG_OPTION(DEBUG "Enable debug mode in engine")
option(GPERF "Build with GPerfTools profiler" OFF)
cmake_dependent_option(GPERF_HEAP_FIRST_GAME_ITERATION "Save heap profile of the first game iteration" OFF "GPERF" OFF)
option(ENABLE_CODECOVERAGE "Instrument code for code coverage (only enabled with BUILD_TESTING)" OFF)

# Network options
cmake_dependent_option(DISABLE_TCP "Disable TCP multiplayer option" OFF "NOT NONET" ON)
cmake_dependent_option(DISABLE_ZERO_TIER "Disable ZeroTier multiplayer option" OFF "NOT NONET" ON)

if(USE_SDL1 AND USE_SDL3)
  message(FATAL_ERROR "USE_SDL1 and USE_SDL3 cannot be set at the same time")
endif()

if(DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT AND (NONET OR DISABLE_TCP))
  message(FATAL_ERROR "DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT requires TCP network support")
endif()

# Graphics options
if(NOT USE_SDL1)
if(USE_SDL3)
  set(_texture_format_default "SDL_PIXELFORMAT_XRGB8888")
else()
  set(_texture_format_default "SDL_PIXELFORMAT_RGB888")
endif()
  set(DEVILUTIONX_DISPLAY_TEXTURE_FORMAT "${_texture_format_default}" CACHE STRING "Texture format for DevilutionX textures when using the GPU renderer")
  mark_as_advanced(DEVILUTIONX_DISPLAY_TEXTURE_FORMAT)
endif()

if(USE_SDL1)
  # SDL_image in SDL1 does not support PNG, making PCX the only option.
  set(DEVILUTIONX_SCREENSHOT_FORMAT "DEVILUTIONX_SCREENSHOT_FORMAT_PCX")
else()
  set(DEVILUTIONX_SCREENSHOT_FORMAT "DEVILUTIONX_SCREENSHOT_FORMAT_PNG" CACHE STRING "Screenshot format")
  set_property(CACHE DEVILUTIONX_SCREENSHOT_FORMAT PROPERTY STRINGS "DEVILUTIONX_SCREENSHOT_FORMAT_PNG;DEVILUTIONX_SCREENSHOT_FORMAT_PCX")
  mark_as_advanced(DEVILUTIONX_SCREENSHOT_FORMAT)
endif()

# Sound options
option(NOSOUND "Disable sound support" OFF)
option(DEVILUTIONX_RESAMPLER_SPEEX "Build with Speex resampler" ON)
cmake_dependent_option(DEVILUTIONX_RESAMPLER_SDL "Build with SDL resampler" ON "NOT USE_SDL1" OFF)
if(DEVILUTIONX_RESAMPLER_SPEEX)
  list(APPEND _resamplers Speex)
endif()
if(DEVILUTIONX_RESAMPLER_SDL)
  list(APPEND _resamplers SDL)
endif()
list(GET _resamplers 0 _default_resampler)
set(DEVILUTIONX_DEFAULT_RESAMPLER ${_default_resampler} CACHE STRING "Default resampler")
set_property(CACHE DEVILUTIONX_DEFAULT_RESAMPLER PROPERTY STRINGS ${_resamplers})

# Optimization / link options
option(DISABLE_LTO "Disable link-time optimization (by default enabled in release mode)" OFF)
option(PIE "Generate position-independent code" OFF)
cmake_dependent_option(DEVILUTIONX_DISABLE_RTTI "Disable RTTI" ON "NONET" OFF)
cmake_dependent_option(DEVILUTIONX_DISABLE_EXCEPTIONS "Disable exceptions" ON "DISABLE_ZERO_TIER" OFF)
RELEASE_OPTION(DEVILUTIONX_STATIC_CXX_STDLIB "Link C++ standard library statically (if available)")
option(DEVILUTIONX_PROFILE_GENERATE "Build a binary that generates the profile for PGO" OFF)
option(DEVILUTIONX_PROFILE_USE "Build with PGO using the given profile file" OFF)
set(DEVILUTIONX_PROFILE_DIR "" CACHE STRING "Directory where the profile is stored")

include(MoldLinker)

# Memory / performance trade-off options
option(UNPACKED_MPQS "Expect MPQs to be unpacked and the data converted with devilutionx-mpq-tools" OFF)
option(UNPACKED_SAVES "Uses unpacked save files instead of MPQ .sv/.hsv files" OFF)
option(DISABLE_STREAMING_MUSIC "Disable streaming music (to work around broken platform implementations)" OFF)
mark_as_advanced(DISABLE_STREAMING_MUSIC)
option(DISABLE_STREAMING_SOUNDS "Disable streaming sounds (to work around broken platform implementations)" OFF)
mark_as_advanced(DISABLE_STREAMING_SOUNDS)
set(STREAM_ALL_AUDIO_MIN_FILE_SIZE "" CACHE STRING "If set, stream all the audio files larger than this size")
mark_as_advanced(STREAM_ALL_AUDIO_MIN_FILE_SIZE)
option(DEVILUTIONX_PALETTE_TRANSPARENCY_BLACK_16_LUT "Whether to use a lookup table for transparency blending with black. This improves performance of blending transparent black overlays, such as quest dialog background, at the cost of 128 KiB of RAM." ON)
mark_as_advanced(DEVILUTIONX_PALETTE_TRANSPARENCY_BLACK_16_LUT)

# Additional features
option(DISABLE_DEMOMODE "Disable demo mode support" OFF)
option(SCREEN_READER_INTEGRATION "Build with screen reader support" OFF)
mark_as_advanced(SCREEN_READER_INTEGRATION)

# If both UNPACKED_MPQS and UNPACKED_SAVES are enabled, we completely remove MPQ support.
if(UNPACKED_MPQS AND UNPACKED_SAVES)
  set(SUPPORTS_MPQ OFF)
else()
  set(SUPPORTS_MPQ ON)
endif()

# By default, devilutionx.mpq and mods/Hellfire.mpq are built only if smpq is installed and MPQ support is enabled.
if(SUPPORTS_MPQ AND NOT UNPACKED_MPQS)
  if(BUILD_ASSETS_MPQ)
    find_program(SMPQ smpq REQUIRED)
  elseif(NOT DEFINED BUILD_ASSETS_MPQ AND NOT SRC_DIST)
    find_program(SMPQ smpq)
  endif()
  if(SMPQ)
    set(_has_smpq ON)
  else()
    set(_has_smpq OFF)
  endif()
  option(BUILD_ASSETS_MPQ "If true, assets are packaged into devilutionx.mpq and mods/Hellfire to Hellfire.mpq." ${_has_smpq})
else()
  set(BUILD_ASSETS_MPQS OFF)
endif()

# === Option overrides ===
# TSAN is not compatible with ASAN.
if(TSAN)
  set(ASAN OFF)
endif()

if(MSVC AND NOT CMAKE_BUILD_TYPE STREQUAL "Debug" AND NOT DISABLE_LTO)
  # Work around MSVC + CMake bug when LTO is enabled.
  # See https://github.com/diasurgical/devilutionX/issues/3778
  # and https://gitlab.kitware.com/cmake/cmake/-/issues/23035
  set(BUILD_TESTING OFF)
endif()

# Note: `CMAKE_CROSSCOMPILING` is only available after the `project` call.
if(CMAKE_CROSSCOMPILING)
  set(BUILD_TESTING OFF)
endif()

if(DISABLE_DEMOMODE)
  # Testing requires demomode.
  set(BUILD_TESTING OFF)
endif()

if(BUILD_TESTING)
  # When tests are enabled, we build a shared devilutionx_so library, which needs to be PIC to link.
  set(PIE ON)
endif()

# Recalculate the dependent options that are defined before `include(Platforms)`:
if(NONET)
  # PACKET_ENCRYPTION is defined before `Platforms.cmake` is included.
  # This means that if a `Platforms.cmake` sets NONET to OFF, PACKET_ENCRYPTION will not automatically
  # reflect that.
  set(PACKET_ENCRYPTION OFF)
endif()
# === End of option overrides ===

if(PIE)
  set(CMAKE_POSITION_INDEPENDENT_CODE TRUE)
endif()

find_program(CCACHE_PROGRAM ccache)
if(CCACHE_PROGRAM)
  set(CMAKE_C_COMPILER_LAUNCHER "${CCACHE_PROGRAM}")
  set(CMAKE_CXX_COMPILER_LAUNCHER "${CCACHE_PROGRAM}")
endif()

if(DEVILUTIONX_DISABLE_RTTI)
  if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fno-rtti")
  elseif(MSVC)
    string(REGEX REPLACE "/GR" "/GR-" CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS}")
  endif()
endif()

if(DEVILUTIONX_DISABLE_EXCEPTIONS)
  if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fno-exceptions")
  elseif(MSVC)
    string(REGEX REPLACE "/EHsc" "/EHs-c-" CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS}")
  endif()
endif()

# Remove unused symbols in non-debug mode.
# This is useful even with LTO (-84 KiB with MinSizeRel).
#
# PS4 toolchain crashes in `create-fself` when linking with these flags, so we exclude it:
# https://github.com/PacBrew/ps4-openorbis/issues/8
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang" AND NOT PS4)
  add_compile_options("$<$<NOT:$<CONFIG:Debug>>:-ffunction-sections;-fdata-sections>")
  if(APPLE)
    add_link_options("$<$<NOT:$<CONFIG:Debug>>:LINKER:-dead_strip>")
  else()
    add_link_options("$<$<NOT:$<CONFIG:Debug>>:LINKER:--gc-sections,--as-needed>")
  endif()
endif()

if(CMAKE_CXX_COMPILER_ID MATCHES "GNU")
  # -fipa-pta and -fdevirtualize-at-ltrans improve performance.
  add_compile_options("$<$<NOT:$<CONFIG:Debug>>:-fipa-pta;-fdevirtualize-at-ltrans>")
endif()

if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
  add_compile_options("$<$<BOOL:${DEVILUTIONX_PROFILE_GENERATE}>:-fprofile-generate>")
  add_link_options("$<$<BOOL:${DEVILUTIONX_PROFILE_GENERATE}>:-fprofile-generate>")
  add_compile_options("$<$<BOOL:${DEVILUTIONX_PROFILE_USE}>:-fprofile-use>")
  add_link_options("$<$<BOOL:${DEVILUTIONX_PROFILE_USE}>:-fprofile-use>")
  add_compile_options("$<$<BOOL:${DEVILUTIONX_PROFILE_DIR}>:-fprofile-dir=${DEVILUTIONX_PROFILE_DIR};-fprofile-prefix-path=${CMAKE_CURRENT_BINARY_DIR}>")
  add_link_options("$<$<BOOL:${DEVILUTIONX_PROFILE_DIR}>:-fprofile-dir=${DEVILUTIONX_PROFILE_DIR};-fprofile-prefix-path=${CMAKE_CURRENT_BINARY_DIR}>")
endif()

if(CMAKE_CXX_COMPILER_ID MATCHES "MSVC")
  # u8path() function is deprecated but there is no sensible alternative and it might even get un-deprecated.
  add_definitions(-D_SILENCE_CXX20_U8PATH_DEPRECATION_WARNING)
  if(${CMAKE_VERSION} VERSION_GREATER_EQUAL "3.25")
    # This enables Edit & Continue support, see https://learn.microsoft.com/en-us/cpp/build/cmake-projects-in-visual-studio#edit-and-continue-for-cmake-projects
    set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<$<CONFIG:Debug>:EditAndContinue>") # Sets /ZI compiler option, see https://cmake.org/cmake/help/latest/variable/CMAKE_MSVC_DEBUG_INFORMATION_FORMAT.html
    add_link_options("$<$<CONFIG:Debug>:/INCREMENTAL>")
  endif()
endif()

# Not a genexp because CMake doesn't support it
# https://gitlab.kitware.com/cmake/cmake/-/issues/20546
if(NOT DISABLE_LTO)
  # LTO if supported:
  include(CheckIPOSupported)
  check_ipo_supported(RESULT is_ipo_supported OUTPUT lto_error)
  if(is_ipo_supported)
    set(CMAKE_INTERPROCEDURAL_OPTIMIZATION_RELEASE ON)
    set(CMAKE_INTERPROCEDURAL_OPTIMIZATION_RELWITHDEBINFO ON)
    set(CMAKE_INTERPROCEDURAL_OPTIMIZATION_MINSIZEREL ON)
  endif()
endif()

if(GPERF)
  set(DEVILUTIONX_STATIC_CXX_STDLIB OFF)

  if(GPERF_HEAP_FIRST_GAME_ITERATION)
    set(GPERF_HEAP_MAIN ON)
  endif()

  # Compile with information about file and line numbers for everything
  # even in non-Debug build types.
  if(CMAKE_CXX_COMPILER_ID MATCHES "GNU")
    add_compile_options("$<$<NOT:$<CONFIG:Debug>>:-g2>")
  elseif(CMAKE_CXX_COMPILER_ID MATCHES "Clang")
    # Use the more size-efficient `-gmlt` option on clang.
    add_compile_options("$<$<NOT:$<CONFIG:Debug>>:-gmlt>")
  endif()
endif()

set(CMAKE_CXX_STANDARD 20)

# On some platforms, such as DJGPP,
# `-std=c++20` defines `__STRICT_ANSI__` which disables
# all POSIX extensions, so we need to use `-std=gnu++20` instead.
set(CMAKE_CXX_EXTENSIONS ON)

set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON) # for clang-tidy
set(CMAKE_THREAD_PREFER_PTHREAD ON)
set(THREADS_PREFER_PTHREAD_FLAG ON)
if(NOT TARGET_PLATFORM STREQUAL "dos")
  find_package(Threads REQUIRED)
endif()

# Dependencies must be included after Platforms.
include(Dependencies)

add_subdirectory(legacy-native)

set(BIN_TARGET devilutionx)
if(NINTENDO_3DS)
  set(BIN_TARGET ${BIN_TARGET}.elf)
elseif(TARGET_PLATFORM STREQUAL "dos")
  set(BIN_TARGET devx)
endif()

add_executable(${BIN_TARGET}
  WIN32
  MACOSX_BUNDLE
  legacy-native/application/main.cpp)

if(CMAKE_STRIP AND NOT DEVILUTIONX_DISABLE_STRIP)
  add_custom_command(
    TARGET ${BIN_TARGET} POST_BUILD
    COMMAND $<$<OR:$<CONFIG:Release>,$<CONFIG:MinSizeRel>>:${CMAKE_STRIP}>
    ARGS $<TARGET_FILE:${BIN_TARGET}>)
endif()

if(TARGET_PLATFORM STREQUAL "dos")
    # Allow multiple definitions for math stubs in DJGPP
    set_target_properties(${BIN_TARGET} PROPERTIES
        LINK_FLAGS "-Wl,--allow-multiple-definition -static"
    )
    target_link_libraries(${BIN_TARGET} PRIVATE m)
endif()

target_link_dependencies(${BIN_TARGET} PRIVATE libdevilutionx)

if(GPERF)
  target_link_libraries(${BIN_TARGET} PUBLIC ${GPERFTOOLS_LIBRARIES})
endif()

# Must be included after `BIN_TARGET` and `libdevilutionx` are defined.
include(Assets)
include(Mods)

if(EMSCRIPTEN)
  target_link_options(${BIN_TARGET} PRIVATE --preload-file assets)
endif()

if(NOT USE_SDL1)
  target_link_libraries(${BIN_TARGET} PUBLIC ${SDL2_MAIN})
endif()

if(BUILD_TESTING)
  include(Tests)
endif()

include(functions/set_relative_file_macro)
set_relative_file_macro(${BIN_TARGET})

find_library(COREFOUNDATION_LIBRARY CoreFoundation)
if(COREFOUNDATION_LIBRARY)
  target_link_libraries(libdevilutionx PUBLIC "${COREFOUNDATION_LIBRARY}")
  target_compile_definitions(libdevilutionx PRIVATE USE_COREFOUNDATION)
endif()

resolve_target_link_dependencies()
