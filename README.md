# Automatic Proxy Discovery

This project showcases the troubles with more complex proxy configurations in Unity.
This will most likely never affect you **except** when you do anything with heavy
enterprise customers (think: Automotive, Pharma, Medical, etc.). In these cases not
being able to handle complex proxy setups will kill all your networking efforts 🙈

I focus mainly on two different networking components:
- The standard `UnityWebRequest`
- The .NET `HttpClient`

`UnityWebRequest` picks up a simple system proxy automatically but lacks features for
dynamic configuration (changes at runtime through code) and more intricate SSL
configuration.

> **Note**: This bug has finally been fixed in Unity 2022.1.X! Checkout https://issuetracker.unity3d.com/issues/unitywebrequest-does-not-respect-auto-proxy-settings for more information

`HttpClient` doesn't do anyting automatically but ooffers extensive APIs for dynamic
configuration, dirty SSL hacks, etc.

I will show how these differences affect the networking behavior in three different scenarios given the small application I built which allows for dynamic script based
proxy configuration:
- No proxy
- Standard simple system proxy
- Scripted configuration using a `proxy.pac` file

## What does the App do?

The given application consists of one screen that allows you to download Chuck
Norris quotes. You can select if you use `UnityWebRequest` (WWW) or the .NET
`HttpClient` to do this.

## Prerequisites

- Unity
- A Windows PC
- Telerik Fiddler

## No Proxy

In cases where no proxy server is configured for the system (and no proxy is present
on the network) everything works fine 🎉🎉🎉

![no proxy](images/no_proxy.gif)

## System Proxy

In cases where you set a standard system proxy both `UnityWebRequest` as well as the
`HttpClient` are able to pick up the correct proxy server with the code provided
in this application.

![system proxy](images/standard_system_proxy.gif)

## Scripted Proxy Configuration

The case that breaks everyting built on `UnityWebRequest` is scripted proxy
configuration. As stated above: this is really common in enterprise setups
and a real blocker if you want to deploy - say - a VR/AR/MR training or assistance
experience built with Unity. You might manage to convince the IT department to treat
you with special care but in my experience you don't want to be the 'special little
snowflake'. If your application is hated by a corporate IT department from the get-go
it will most likely never scale up.

This repo ships with a very simple PAC file that basically points to the local
Fiddler proxy for every host you want to call.


![scripted system proxy](images/scripted_system_proxy.gif)