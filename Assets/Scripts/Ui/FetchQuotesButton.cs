using System;
using System.Collections;
using System.Net;
using System.Net.Http;
using ProxyResolver;
using Ui.Transfer;
using UnityEngine;
using UnityEngine.Networking;

namespace Ui
{
    public class FetchQuotesButton : MonoBehaviour
    {
        private const string QuoteApiEndpoint = "https://api.chucknorris.io/jokes/random";

        public delegate void OnQuoteFetchedHandler(string quote);

        public event OnQuoteFetchedHandler OnQuoteFetched;

        [HideInInspector] public bool shouldUseUnityWebRequest = true;

        public void FetchQuote()
        {
            if (shouldUseUnityWebRequest)
            {
                Debug.Log("Using Unity Web Request to catch a quote.");
                StartCoroutine(FetchQuoteWithUnityWebRequest());
            }
            else
            {
                Debug.Log("Using HttpClient to catch a quote.");
                FetchQuoteWithHttpClient();
            }
        }

        private IEnumerator FetchQuoteWithUnityWebRequest()
        {
            // In the case of a Unity Web Request Proxies come preconfigured
            // if the engine fails to get the correct proxy (because it is
            // configured using WPAD or a PAC script) we can't configure it ourselves :(
            var www = UnityWebRequest.Get(QuoteApiEndpoint);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.LogError($"Request failed: {www.error}");
            }
            else
            {
                HandleFetchedTextBody(www.downloadHandler?.text);
            }
        }

        private async void FetchQuoteWithHttpClient()
        {
            // Depending on the computers proxy setup we want to get the correctly
            // configured HttpClient for our web request
            using (var httpClient = GetConfiguredHttpClient(QuoteApiEndpoint))
            {
                var response = await httpClient.GetAsync(QuoteApiEndpoint);
                if (response.IsSuccessStatusCode)
                {
                    var text = await response.Content.ReadAsStringAsync();
                    HandleFetchedTextBody(text);
                }
            }
        }

        private HttpClient GetConfiguredHttpClient(string endpoint)
        {
            var proxyResolver = new AutomaticProxyResolver();
            var preferredProxy = proxyResolver.ResolveProxyForTargetUrl(new Uri(endpoint));

            if (preferredProxy != null)
            {
                var httpClientHandler = new HttpClientHandler { Proxy = preferredProxy };

                // if we use a HTTPS proxy we have to assume that clients use custom certificates
                // there is no obvious elegant way to access system certificates so even if they establish
                // trust on the system level Unity will never know and fail
                // as we don't want that we have to circumvent server certificate validation
                ServicePointManager.ServerCertificateValidationCallback =
                    (message, cert, chain, errors) => true;

                return new HttpClient(httpClientHandler, true);
            }

            return new HttpClient();
        }

        private void HandleFetchedTextBody(string fetchedText)
        {
            var quote = JsonUtility.FromJson<Quote>(fetchedText);
            if (quote?.value != null)
            {
                OnQuoteFetched?.Invoke(quote.value);
            }
            else
            {
                Debug.LogWarning("Web request didn't yield a valid return value.");
            }
        }
    }
}