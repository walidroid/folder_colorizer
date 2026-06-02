cmake_minimum_required(VERSION 3.20)
project(FolderColorizer VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Windows 10+
add_compile_definitions(
    UNICODE
    _UNICODE
    _WIN32_WINNT=0x0A00
    WINVER=0x0A00
    WIN32_LEAN_AND_MEAN
    NOMINMAX
)

# GUI subsystem — no console window
add_executable(folder_colorizer WIN32
    src/main.cpp
    src/app.rc
)

target_link_libraries(folder_colorizer PRIVATE
    comctl32
    shell32
    shlwapi
    uxtheme
    gdi32
    user32
    advapi32
    ole32
)

# MSVC: use /W3, no console, multi-threaded static CRT
if(MSVC)
    target_compile_options(folder_colorizer PRIVATE /W3 /WX- /MP)
    set_target_properties(folder_colorizer PROPERTIES
        MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>"
        WIN32_EXECUTABLE TRUE
    )
endif()

# Output the EXE directly into build/Release (matches installer.iss expectation)
set_target_properties(folder_colorizer PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}"
)

install(TARGETS folder_colorizer DESTINATION .)
install(DIRECTORY icons/ DESTINATION icons)
