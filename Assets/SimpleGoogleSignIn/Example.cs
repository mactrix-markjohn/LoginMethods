using UnityEngine;
using UnityEngine.UI;

namespace Assets.SimpleGoogleSignIn
{
    public class Example : MonoBehaviour
    {
        public Text Log;
        public Text Output;

        public void Start()
        {
            Application.logMessageReceived += LogMessageReceived;

            #if UNITY_WEBGL

            if (Application.absoluteURL.Contains("access_token="))
            {
                SignIn();
            }

            #endif
        }

        private void LogMessageReceived(string condition, string stacktrace, LogType type)
        {
            Log.text += condition + "\n";
        }

        public void SignIn()
        {
            // TODO: Don't use default client IDs that come with the asset in production, they are for test purposes only and can be disabled/blocked.

            #if UNITY_STANDALONE || UNITY_EDITOR

            GoogleAuth.Auth("275731233438-v1anl61611mmer6ohqes9a310mkc2di8.apps.googleusercontent.com", "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", AuthCallback);
            
            #elif UNITY_WSA || UNITY_ANDROID || UNITY_IOS

            GoogleAuth.Auth("275731233438-nv59vdj6ornprhtppnm8qle2jt3ebjol.apps.googleusercontent.com", "google.auth", AuthCallback);

            #elif UNITY_WEBGL

            GoogleAuth.Auth("275731233438-3hvi982vbhpcpkjihioa4d3gum074lds.apps.googleusercontent.com275731233438-3hvi982vbhpcpkjihioa4d3gum074lds.apps.googleusercontent.com", AuthCallback);

            #endif
        }

        private void AuthCallback(bool success, string error, UserInfo userInfo)
        {
            if (success)
            {
                Output.text = $"Hello, {userInfo.name}!";
            }
            else
            {
                Output.text = error;
            }
        }

        public void Navigate(string url)
        {
            Application.OpenURL(url);
        }
    }
}