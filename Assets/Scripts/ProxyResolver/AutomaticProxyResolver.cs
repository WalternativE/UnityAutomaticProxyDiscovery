using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Jint;
using UnityEngine;
using Microsoft.Win32;

namespace ProxyResolver
{
    public class AutomaticProxyResolver
    {
        private const string InternetSettingsRegistryKey =
            "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings";

        private const string AutoConfigValueKey = "AutoConfigURL";

        private static readonly Func<string, string> DnsResolve = dnsName =>
        {
            var addresses = Dns.GetHostEntry(dnsName).AddressList;
            return addresses
                .FirstOrDefault(ipAddress => ipAddress.AddressFamily == AddressFamily.InterNetwork)?.ToString();
        };

        // this implementation should also work with multiple network interfaces and return
        // the preferred external endpoint
        private static readonly Func<string> MyIpAddress = () =>
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint?.Address?.ToString();
                }
            }
            catch (Exception)
            {
                return null;
            }
        };

        private static readonly Func<string, bool> IsValidIpAddress = toValidate =>
            IPAddress.TryParse(toValidate, out _);

        private Engine sharedEngine;

        public WebProxy ResolveProxyForTargetUrl(Uri targetUri)
        {
            // first try to find out if there is already a simple system web proxy configured
            var systemProxy = ResolveSystemWebProxy();

            // if there is no obvious web proxy configured try to resolve proxy autoconfig
            // should it be available
            return systemProxy ?? ResolveAutomaticWebProxy(targetUri);
        }

        private static WebProxy ResolveSystemWebProxy()
        {
            var proxy = (WebProxy) WebRequest.DefaultWebProxy;
            return string.IsNullOrWhiteSpace(proxy?.Address?.AbsoluteUri) ? null : proxy;
        }

        private WebProxy ResolveAutomaticWebProxy(Uri targetUri)
        {
            // We currently only support automatic proxy retrieval using DNS WPAD or PAC files
            // on Windows. For other platforms we can still take advantage of the simple system
            // proxy setup.
            if (Application.platform == RuntimePlatform.WindowsPlayer ||
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                return ResolveAutomaticWebProxyOnWindows(targetUri);
            }

            return null;
        }

        private WebProxy ResolveAutomaticWebProxyOnWindows(Uri targetUri)
        {
            if (sharedEngine == null)
            {
                string pacScript;
                using (var client = new HttpClient())
                {
                    // we assume that the autoconfig script is a valid pac file
                    // this should also work for DNS WPAD pointing to a wpad.dat file
                    var autoConfigScriptLocation = RetrievePacScriptLocation();

                    // if we don't get a valid URI back we cannot continue
                    if (!string.IsNullOrWhiteSpace(autoConfigScriptLocation) && Uri.TryCreate(autoConfigScriptLocation,
                        UriKind.Absolute, out var scriptLocation))
                    {
                        var response = client.GetAsync(scriptLocation).Result;

                        // if the HTTP status code does not indicate a success we cannot continue
                        if (!response.IsSuccessStatusCode)
                        {
                            return null;
                        }

                        pacScript = response.Content.ReadAsStringAsync().Result;
                    }
                    else
                    {
                        return null;
                    }
                }

                if (string.IsNullOrWhiteSpace(pacScript))
                {
                    return null;
                }

                sharedEngine = CreateModdedJsEngine(pacScript);
            }

            var proxyStatement = EvaluateProxyForUri(sharedEngine, targetUri);

            var parsedProxyStatement = ParseProxyStatement(proxyStatement);
            switch (parsedProxyStatement)
            {
                case DirectStatement _:
                    return null;
                case ProxyStatement proxy:
                    return proxy.Port.HasValue
                        ? new WebProxy(proxy.Host, proxy.Port.Value)
                        : new WebProxy(proxy.Host);
                default:
                    return null;
            }
        }

        private static ParsedProxyStatement ParseProxyStatement(string proxyStatement)
        {
            if (string.IsNullOrWhiteSpace(proxyStatement))
            {
                return null;
            }

            if (proxyStatement.ToLower().StartsWith("direct"))
            {
                return ParsedProxyStatement.Direct();
            }

            var possibleProxies = proxyStatement.Split(
                new[] { ';' },
                StringSplitOptions.RemoveEmptyEntries);

            // at the current moment we have no way to test if a proxy works and whether we should rather choose
            // the fallback - if we want to go this way we have to check all proxies rather than taking the first one
            var preferredProxy = possibleProxies.FirstOrDefault();

            if (preferredProxy == null)
            {
                return null;
            }

            var proxyLineParts = preferredProxy.Split(' ');

            // example "PROXY 127.0.0.1:8080"
            // a proxy line has exactly two parts: the PROXY keyword and the address part
            if (proxyLineParts.Length != 2)
            {
                return null;
            }

            var addressPart = proxyLineParts[1];
            var proxyAddressParts = addressPart.Split(':');

            string host;
            int? port;
            switch (proxyAddressParts.Length)
            {
                case 2:
                    host = proxyAddressParts[0];
                    port = int.Parse(proxyAddressParts[1]);
                    break;
                case 1:
                    host = proxyAddressParts[0];
                    port = null;
                    break;
                default:
                    return null;
            }

            return ParsedProxyStatement.Proxy(host, port);
        }

        private static string EvaluateProxyForUri(Engine jsEngine, Uri targetUri)
        {
            var proxyStatement =
                jsEngine.Execute($"FindProxyForURL('{targetUri}', '{targetUri.Host}')")
                    .GetCompletionValue();

            return proxyStatement?.ToString();
        }

        private static string RetrievePacScriptLocation()
        {
            var autoConfigScriptLocation = (string) Registry.GetValue(
                InternetSettingsRegistryKey,
                AutoConfigValueKey,
                null
            );

            return string.IsNullOrWhiteSpace(autoConfigScriptLocation) ? null : autoConfigScriptLocation;
        }

        private static Engine CreateModdedJsEngine(string pacScript) =>
            new Engine(cfg => cfg.Strict())
                .SetValue("isValidIpAddress", IsValidIpAddress)
                .SetValue("dnsResolve", DnsResolve)
                .SetValue("myIpAddress", MyIpAddress)
                .Execute(PacUtilities.PacUtilsScript)
                .Execute(pacScript);

        public abstract class ParsedProxyStatement
        {
            public static ParsedProxyStatement Direct() => new DirectStatement();
            public static ParsedProxyStatement Proxy(string host, int? port) => new ProxyStatement(host, port);
        }

        private sealed class DirectStatement : ParsedProxyStatement
        {
        }

        private sealed class ProxyStatement : ParsedProxyStatement
        {
            internal ProxyStatement(string host, int? port)
            {
                Host = host;
                Port = port;
            }

            internal string Host { get; }
            internal int? Port { get; }
        }
    }
}
