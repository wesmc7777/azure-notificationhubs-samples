using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

using Windows.Networking.PushNotifications;
using Microsoft.WindowsAzure.Messaging;
using Windows.UI.Popups;


using Windows.Storage;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Linq;

using Windows.Security.Cryptography.Core;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;


// The Blank Application template is documented at http://go.microsoft.com/fwlink/?LinkId=234227

namespace GetStartedWindowsUniversal
{


    struct DeviceInstallation
    {
        public string installationId { get; set; }
        public string platform { get; set; }
        public string pushChannel { get; set; }
        public string[] tags { get; set; }
    }


    class ConnectionStringUtility
    {
        public string Endpoint { get; private set; }
        public string SasKeyName { get; private set; }
        public string SasKeyValue { get; private set; }

        public ConnectionStringUtility(string connectionString)
        {
            //Parse Connectionstring
            char[] separator = { ';' };
            string[] parts = connectionString.Split(separator);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("Endpoint"))
                    Endpoint = "https" + parts[i].Substring(11);
                if (parts[i].StartsWith("SharedAccessKeyName"))
                    SasKeyName = parts[i].Substring(20);
                if (parts[i].StartsWith("SharedAccessKey"))
                    SasKeyValue = parts[i].Substring(16);
            }
        }

        public string getSaSToken(string uri, int minUntilExpire)
        {
            string targetUri = Uri.EscapeDataString(uri.ToLower()).ToLower();

            // Add an expiration in seconds to it.
            long expiresOnDate = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            expiresOnDate += minUntilExpire * 60 * 1000;
            long expires_seconds = expiresOnDate / 1000;
            String toSign = targetUri + "\n" + expires_seconds;

            // Generate a HMAC-SHA256 hash or the uri and expiration using your secret key.
            MacAlgorithmProvider macAlgorithmProvider = MacAlgorithmProvider.OpenAlgorithm(MacAlgorithmNames.HmacSha256);
            BinaryStringEncoding encoding = BinaryStringEncoding.Utf8;
            var messageBuffer = CryptographicBuffer.ConvertStringToBinary(toSign, encoding);
            IBuffer keyBuffer = CryptographicBuffer.ConvertStringToBinary(SasKeyValue, encoding);
            CryptographicKey hmacKey = macAlgorithmProvider.CreateKey(keyBuffer);
            IBuffer signedMessage = CryptographicEngine.Sign(hmacKey, messageBuffer);
 
            string signature = Uri.EscapeDataString(CryptographicBuffer.EncodeToBase64String(signedMessage));

            return "SharedAccessSignature sr=" + targetUri + "&sig=" + signature + "&se=" + expires_seconds + "&skn=" + SasKeyName;
        }

    }


    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App : Application
    {
#if WINDOWS_PHONE_APP
        private TransitionCollection transitions;
#endif

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.OnSuspending;
        }

        private async void InitNotificationsAsync()
        {
            var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
            
            // Registration Id 
            //var hub = new NotificationHub("<hub name>", "<connection string with listen access>");
            //var result = await hub.RegisterNativeAsync(channel.Uri);

            // Displays the registration ID so you know it was successful
            //if (result.RegistrationId != null)
            //{
            //    var dialog = new MessageDialog("Registration successful: " + result.RegistrationId);
            //    dialog.Commands.Add(new UICommand("OK"));
            //    await dialog.ShowAsync();
            //}

            // Use Installation 
            string installationId = null;
            var settings = ApplicationData.Current.LocalSettings.Values;

            // If we have not stored a installation id in application data, create and store as application data.
            if (!settings.ContainsKey("__NHInstallationId"))
            {
                installationId = Guid.NewGuid().ToString();
                settings.Add("__NHInstallationId", installationId);
            }

            installationId = (string)settings["__NHInstallationId"];

            var deviceInstallation = new DeviceInstallation
            {
                installationId = installationId,
                platform = "wns",
                pushChannel = channel.Uri,
                //tags = tags.ToArray<string>()
            };

            var statusCode = await CreateOrUpdateInstallationAsync(deviceInstallation, "wesmc-hub", "Endpoint=sb://wesmc-hub-ns.servicebus.windows.net/;SharedAccessKeyName=DefaultListenSharedAccessSignature;SharedAccessKey=vIlbhiaBeH5MYg680giYwevM+XdTbMUaEwzv4B/KZsk=");

            if (statusCode != HttpStatusCode.Accepted)
            {
                var dialog = new MessageDialog(statusCode.ToString(), "Registration failed. Installation Id : " + installationId);
                dialog.Commands.Add(new UICommand("OK"));
                await dialog.ShowAsync();
            }
            else
            {
                var dialog = new MessageDialog("Registration successful using installation Id : " + installationId);
                dialog.Commands.Add(new UICommand("OK"));
                await dialog.ShowAsync();
            }
        }


        private async Task<HttpStatusCode> CreateOrUpdateInstallationAsync(DeviceInstallation deviceInstallation, string hubName, string listenConnectionString)
        {
            if (deviceInstallation.installationId == null)
                return HttpStatusCode.BadRequest;

            // Parse connection string
            ConnectionStringUtility connectionSaSUtil = new ConnectionStringUtility(listenConnectionString);
            string hubResource = "installations/" + deviceInstallation.installationId + "?";
            string apiVersion = "api-version=2015-04";

            // Determine the targetUri that we will sign
            string uri = connectionSaSUtil.Endpoint + hubName + "/" + hubResource + apiVersion;

            //=== Generate SaS Security Token for Authorization header ===
            string SasToken = connectionSaSUtil.getSaSToken(uri, 60);

            using (var httpClient = new HttpClient())
            {
                string json = JsonConvert.SerializeObject(deviceInstallation);

                httpClient.DefaultRequestHeaders.Add("Authorization", SasToken);

                var response = await httpClient.PutAsync(uri, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
                return response.StatusCode;
            }
        }


        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            InitNotificationsAsync();

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                // TODO: change this value to a cache size that is appropriate for your application
                rootFrame.CacheSize = 1;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
#if WINDOWS_PHONE_APP
                // Removes the turnstile navigation for startup.
                if (rootFrame.ContentTransitions != null)
                {
                    this.transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        this.transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += this.RootFrame_FirstNavigated;
#endif

                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                if (!rootFrame.Navigate(typeof(MainPage), e.Arguments))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            // Ensure the current window is active
            Window.Current.Activate();
        }

#if WINDOWS_PHONE_APP
        /// <summary>
        /// Restores the content transitions after the app has launched.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the navigation event.</param>
        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }
#endif

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            // TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}