using System;
using System.Collections.Specialized;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.SimpleGoogleSignIn
{
    /// <summary>
    /// https://developers.google.com/identity/protocols/oauth2/native-app
    /// </summary>
    public static partial class GoogleAuth
    {
        private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://www.googleapis.com/oauth2/v4/token";
        private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";
        private const string AccessScope = "openid email profile";

        private static string _clientId;
        private static string _clientSecret;
        private static string _redirectUri;
        private static string _state;
        private static string _codeVerifier;
        private static Action<bool, string, UserInfo> _callback;

        #if UNITY_STANDALONE || UNITY_EDITOR

        public static void Auth(string clientId, string clientSecret, Action<bool, string, UserInfo> callback)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _callback = callback;
            _redirectUri = $"http://localhost:{Utils.GetRandomUnusedPort()}/";

            Auth();
            Listen();
        }

        private static void Listen()
        {
            var httpListener = new System.Net.HttpListener();

            httpListener.Prefixes.Add(_redirectUri);
            httpListener.Start();

            var context = System.Threading.SynchronizationContext.Current;
            var asyncResult = httpListener.BeginGetContext(result => context.Send(HandleHttpListenerCallback, result), httpListener);

            // Block the thread when background mode is not supported to serve HTTP response while the application is not in focus.
            if (!Application.runInBackground) asyncResult.AsyncWaitHandle.WaitOne();
        }

        private static void HandleHttpListenerCallback(object state)
        {
            var result = (IAsyncResult) state;
            var httpListener = (System.Net.HttpListener) result.AsyncState;
            var context = httpListener.EndGetContext(result);

            // Send an HTTP response to the browser to notify the user to close the browser.
            var response = context.Response;
            var buffer = System.Text.Encoding.UTF8.GetBytes($"Success! Please close the browser tab and return to {Application.productName}.");

            response.ContentLength64 = buffer.Length;

            var output = response.OutputStream;

            output.Write(buffer, 0, buffer.Length);
            output.Close();
            httpListener.Close();

            HandleAuthResponse(context.Request.QueryString);
        }

        #elif UNITY_WSA || UNITY_ANDROID || UNITY_IOS

        static GoogleAuth()
        {
            Application.deepLinkActivated += deepLink =>
            {
                Debug.Log("Application.deepLinkActivated=" + deepLink);
                HandleAuthResponse(Utils.ParseQueryString(deepLink));
            };
        }

        public static void Auth(string clientId, string protocol, Action<bool, string, UserInfo> callback)
        {
            #if UNITY_EDITOR

            Debug.LogWarning("Deep links don't work inside Editor.");

            #endif

            _clientId = clientId;
            _callback = callback;
            _redirectUri = $"{protocol}:/oauth2callback";
            
            Auth();
        }

        #elif UNITY_WEBGL

        public static void Auth(string clientId, Action<bool, string, UserInfo> callback)
        {
            _clientId = clientId;
            _callback = callback;
            _redirectUri = Application.absoluteURL;

            var accessToken = Utils.ParseQueryString(Application.absoluteURL).Get("access_token");

            if (accessToken == null)
            {
                Application.OpenURL($"{AuthorizationEndpoint}?response_type=token&scope={AccessScope}&redirect_uri={_redirectUri}&client_id={_clientId}");
            }
            else
            {
                Debug.Log($"Access token extracted from Application.absoluteURL: {accessToken}");
                RequestUserInfo(accessToken, callback);
            }
        }

        #endif

        private static void Auth()
        {
            _state = Guid.NewGuid().ToString();
            _codeVerifier = Guid.NewGuid().ToString();

            var codeChallenge = Utils.CreateCodeChallenge(_codeVerifier);
            var authorizationRequest = $"{AuthorizationEndpoint}?response_type=code&scope={Uri.EscapeDataString(AccessScope)}&redirect_uri={Uri.EscapeDataString(_redirectUri)}&client_id={_clientId}&state={_state}&code_challenge={codeChallenge}&code_challenge_method=S256";

            Debug.Log("authorizationRequest=" + authorizationRequest);
            Application.OpenURL(authorizationRequest);
        }

        private static void HandleAuthResponse(NameValueCollection parameters)
        {
            var error = parameters.Get("error");

            if (error != null)
            {
                _callback?.Invoke(false, error, null);
                return;
            }

            var state = parameters.Get("state");
            var code = parameters.Get("code");
            var scope = parameters.Get("scope");

            if (state == null || code == null || scope == null) return;

            if (state == _state)
            {
                PerformCodeExchange(code, _codeVerifier);
            }
            else
            {
                Debug.Log("Unexpected response.");
            }
        }

        private static void PerformCodeExchange(string code, string codeVerifier)
        {
            var form = new WWWForm();

            form.AddField("code", code);
            form.AddField("redirect_uri", _redirectUri);
            form.AddField("client_id", _clientId);
            form.AddField("code_verifier", codeVerifier);

            if (_clientSecret != null) form.AddField("client_secret", _clientSecret);

            form.AddField("scope", AccessScope);
            form.AddField("grant_type", "authorization_code");

            var request = UnityWebRequest.Post(TokenEndpoint, form);

            request.SendWebRequest().completed += _ =>
            {
                if (request.error == null)
                {
                    Debug.Log("CodeExchange=" + request.downloadHandler.text);

                    var exchangeResponse = JsonUtility.FromJson<TokenExchangeResponse>(request.downloadHandler.text);
                    var accessToken = exchangeResponse.access_token;

                    RequestUserInfo(accessToken, _callback);
                }
                else
                {
                    _callback(false, request.error, null);
                }
            };
        }

        internal class TokenExchangeResponse
        {
            public string access_token;
            //public int expires_in;
            //public string refresh_token;
            //public string scope;
            //public string token_type;
        }

        /// <summary>
        /// You can move this function to your backend for more security.
        /// </summary>
        public static void RequestUserInfo(string accessToken, Action<bool, string, UserInfo> callback)
        {
            var request = UnityWebRequest.Get(UserInfoEndpoint);

            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            request.SendWebRequest().completed += _ =>
            {
                if (request.error == null)
                {
                    var userInfo = JsonUtility.FromJson<UserInfo>(request.downloadHandler.text);

                    callback(true, null, userInfo);
                }
                else
                {
                    callback(false, request.error, null);
                }
            };
        }
    }
}