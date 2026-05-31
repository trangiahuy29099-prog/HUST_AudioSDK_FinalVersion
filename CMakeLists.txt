cmake_minimum_required(VERSION 3.16)

project(HUSTAudioSDK LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(HUSTAudioSDK SHARED
    src/AudioSDK_C_API.cpp
    src/AudioEngine.cpp
    src/AudioListener.cpp
    src/AudioSource.cpp
    src/AudioEnvironment.cpp
)

target_include_directories(HUSTAudioSDK PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

find_package(OpenAL REQUIRED)

target_link_libraries(HUSTAudioSDK PRIVATE
    OpenAL::OpenAL
)

set_target_properties(HUSTAudioSDK PROPERTIES
    OUTPUT_NAME "HUSTAudioSDK"
)
