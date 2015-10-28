using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.ServiceBus.Notifications;

using System.Security.Cryptography;
using System.Net;
using System.IO;
using System.Runtime.Serialization.Json;

namespace SendToNotificationHub
{
    class Program
    {
        static void Main(string[] args)
        {
            //SendNotificationAsync();
            GetInstallations();
            Console.ReadLine();
        }

        private static async void SendNotificationAsync()
        {
            NotificationHubClient hub = NotificationHubClient
                .CreateClientFromConnectionString("Endpoint"=sb://wesmc-hub-ns.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=Ztd5lKYAovSs+g3gs0M/EkbVRuIVw7ft4BUylZRtcss=", "wesmc-hub");
            var toast = @"<toast><visual><binding template=""ToastText01""><text id=""1"">Hello from a .NET App!</text></binding></visual></toast>";
            await hub.SendWindowsNativeNotificationAsync(toast);
        }


        private static async void GetInstallations()
        {
            string listenConnectionString = "Endpoint"=sb://wesmc-hub-ns.servicebus.windows.net/;SharedAccessKeyName=DefaultListenSharedAccessSignature;SharedAccessKey=vIlbhiaBeH5MYg680giYwevM+XdTbMUaEwzv4B/KZsk=";
            string hubName = "wesmc-hub";
            string hubResource = "installations/bfab4178-4bfd-4672-aba8-81cbafbeca7f?";
            string targetUri = null;
            string apiVersion = "api-version=2015-01";


            string endpoint = null;
            string SasKeyName = null;
            string SasKeyValue = null;
            string SasToken = null;

            //Parse Connectionstring
            char[] separator = {';'};
            string[] parts = listenConnectionString.Split(separator);
            for (int i=0; i<parts.Length; i++)
            {
                if (parts[i].StartsWith("Endpoint"))
                    endpoint = "https" + parts[i].Substring(11);
                if (parts[i].StartsWith("SharedAccessKeyName"))
                    SasKeyName = parts[i].Substring(20);
                if (parts[i].StartsWith("SharedAccessKey"))
                    SasKeyValue = parts[i].Substring(16);
            }


            //=== Generate SaS Security Token for Authentication header ===
            // Determine the targetUri that we will sign
            string uri = endpoint + hubName + "/" + hubResource + apiVersion;
            targetUri = Uri.EscapeDataString(uri.ToLower()).ToLower();
            
            // Add an expiration in seconds to it.
            long expiresOnDate = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            int expiresInMins = 60; // 1 hour
            expiresOnDate += expiresInMins * 60 * 1000; 
            long expires_seconds = expiresOnDate / 1000;
            String toSign = targetUri + "\n" + expires_seconds;

            // Generate a HMAC-SHA256 hash or the uri and expiration using your secret key.
            byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(SasKeyValue);
            HMACSHA256 hmacsha256 = new HMACSHA256(keyBytes);
            byte[] hash = hmacsha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(toSign));

            // Create the token string using the base64
            string signature = Uri.EscapeDataString(Convert.ToBase64String(hash));

            SasToken = "SharedAccessSignature sr=" + targetUri + "&sig=" + signature + "&se=" + expires_seconds + "&skn=" + SasKeyName;


            //=== Execute the request 
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);

            request.Method = "GET";
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", SasToken);


            System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
            //byte[] bytes = encoding.GetBytes(data);
            //request.ContentLength = bytes.Length;
            //using (Stream requestStream = request.GetRequestStream())
            //{
            //    // Send the data.
            //    requestStream.Write(bytes, 0, bytes.Length);
            //}


            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            Console.WriteLine("Content length is {0}", response.ContentLength);
            Console.WriteLine("Content type is {0}", response.ContentType);

            // Get the stream associated with the response.
            Stream receiveStream = response.GetResponseStream();

            // Pipes the stream to a higher level stream reader with the required encoding format. 
            StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);

            Console.WriteLine("Response stream received.");
            Console.WriteLine(readStream.ReadToEnd());
            response.Close();
            readStream.Close();
        }     
    }
}
