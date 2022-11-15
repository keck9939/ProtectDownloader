# Unifi Protect Downloader

**Tool to download footage from a local UniFi Protect system**

## Introduction

The Unifi Protect application has no good provision for getting large amounts of video from it. Using the web interface will work for limited downloads, but is not suitable for archiving large amounts of data.
There are several applications that have been written to download the video to mp4 files, the best I've found is here: https://github.com/danielfernau/unifi-protect-video-downloader

However, this project has a lot of dependencies and I found running it as suggested in Docker was cumbersome. In addition, not being a fluent Python programmer, I found it difficult to modify.

I wrote this simple C# application that implements much of the functionality of the above project. It is intended to run under dotnet 6 and as such should run on Windows, Mac or Linux. It has been tested with Windows and WSL 2 (Ubuntu 22.04). It has only two dependencies outside of dotnet itself.

It has a couple of enhancements over the Python version: 
1. It has a command to get the list of available cameras.
2. It allows you to specify the cameras by friendly name rather than ID.

## :vertical_traffic_light: Compatibility
Only tested with Unifi Dream Machine Pro with
UniFi OS v1.12.30
Protect v2.2.6

## :arrow_right: Getting started

To run it, cd to the directory with the .cs and .csproj files and type
`dotnet run`
This will give you help on the command arguments.
To get the list of cameras for example:

`dotnet run -- cameras --user me --password mySecurePassword --ip 192.168.1.1`

Note the ` -- ` between `run` and `cameras` which separates the dotnet options from the command line being fed to the application.

`dotnet run -- --help`

gives a pretty complete idea on how to use the program:

```shell
Description:
  Protect Downloader

Usage:
  ProtectDownloader [command] [options]

Options:
  --user <user> (REQUIRED)  User name
  --pass <pass> (REQUIRED)  Password
  --ip <ip> (REQUIRED)      Protect server address
  --version                 Show version information
  -?, -h, --help            Show help and usage information

Commands:
  download  Download video
  cameras   Get list of cameras
```
although "Usage" assumes it has been compiled and is being run as an executable.
