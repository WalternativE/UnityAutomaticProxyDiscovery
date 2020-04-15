function FindProxyForURL(url, host) {
    // easiest possible PAC file
    // connects to standard port of local Fiddler Proxy
    return "PROXY 127.0.0.1:8888";
}