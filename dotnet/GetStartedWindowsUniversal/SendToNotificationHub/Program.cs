using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Azure.NotificationHubs;
using System.Security.Cryptography;
using System.Net;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Xml;

namespace SendToNotificationHub
{

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
            byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(SasKeyValue);
            HMACSHA256 hmacsha256 = new HMACSHA256(keyBytes);
            byte[] hash = hmacsha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(toSign));

            // Create the token string using the base64
            string signature = Uri.EscapeDataString(Convert.ToBase64String(hash));

            return "SharedAccessSignature sr=" + targetUri + "&sig=" + signature + "&se=" + expires_seconds + "&skn=" + SasKeyName;
        }

    }


    class Program
    {
        static void Main(string[] args)
        {
            string listenConnectionString = "Endpoint=sb://wesmc-hub-ns.servicebus.windows.net/;SharedAccessKeyName=DefaultListenSharedAccessSignature;SharedAccessKey=vIlbhiaBeH5MYg680giYwevM+XdTbMUaEwzv4B/KZsk=";
            string fullConnectionString = "Endpoint=sb://wesmc-hub-ns.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=Ztd5lKYAovSs+g3gs0M/EkbVRuIVw7ft4BUylZRtcss=";
            string hubName = "wesmc-hub";

            GetInstallations(hubName, "installations/bfab4178-4bfd-4672-aba8-81cbafbeca7f?", fullConnectionString);

            System.Threading.Thread.Sleep(10000);

            DeleteInstallation(hubName, "installations/bfab4178-4bfd-4672-aba8-81cbafbeca7f?", fullConnectionString);

            SendNotificationREST(hubName, fullConnectionString);
            
            //SendNotificationAsync();

            GetInstallations(hubName, null, fullConnectionString);

            //GetPlatformNotificationServiceFeedback(hubName, fullConnectionString);

            Console.ReadLine();
        }

        private static async void SendNotificationAsync()
        {
            string fullSharedConnectionString = "Endpoint=sb://wesmc-hub-ns.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=Ztd5lKYAovSs+g3gs0M/EkbVRuIVw7ft4BUylZRtcss=";
            string listenConnectionString = "Endpoint=sb://wesmc-hub-ns.servicebus.windows.net/;SharedAccessKeyName=DefaultListenSharedAccessSignature;SharedAccessKey=vIlbhiaBeH5MYg680giYwevM+XdTbMUaEwzv4B/KZsk=";

            NotificationHubClient hub = NotificationHubClient
                .CreateClientFromConnectionString(fullSharedConnectionString, "wesmc-hub");
            var toast = @"<toast><visual><binding template=""ToastText01""><text id=""1"">Hello from a .NET App!</text></binding></visual></toast>";
            NotificationOutcome outcome = await hub.SendWindowsNativeNotificationAsync(toast);
            Console.WriteLine("\nNotification Id : " + outcome.NotificationId.ToString());
            Console.WriteLine("Tracking Id : " + outcome.TrackingId.ToString());

            Console.WriteLine("Waiting 5 min before requesting message telemetry...\n\n");
            System.Threading.Thread.Sleep(300000);

            GetNotificationTelemtry(outcome.NotificationId, "wesmc-hub", fullSharedConnectionString);
        }

        private static async Task<string> SendNotificationREST(string hubname, string connectionString)
        {
            ConnectionStringUtility connectionSaSUtil = new ConnectionStringUtility(connectionString);
            string location = null;

            string hubResource = "messages/?";
            string apiVersion = "api-version=2015-04";
            string notificationId = "";

            //=== Generate SaS Security Token for Authentication header ===
            // Determine the targetUri that we will sign
            string uri = connectionSaSUtil.Endpoint + hubname + "/" + hubResource + apiVersion;
            string SasToken = connectionSaSUtil.getSaSToken( uri, 60);

            //APNS
            //WebHeaderCollection headers = new WebHeaderCollection();
            //headers.Add("ServiceBusNotification-Format", "apple");

            //string body = "{\"aps\":{\"alert\":\"Notification Hub Test REST Notification\"}}";


            //HttpWebResponse response = await ExecuteREST("POST", uri, SasToken, headers, body);

            //GCM toast
            //WebHeaderCollection headers = new WebHeaderCollection();
            //headers.Add("X-WNS-Type", "wns/toast");
            //headers.Add("ServiceBusNotification-Format", "windows");

            //string body = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>" +
            //                "<toast>" +
            //                    "<visual>" +
            //                        "<binding template=\"ToastText01\">" +
            //                            "<text id=\"1\">" +
            //                                "Test WNS from REST!" +
            //                            "</text>" +
            //                        "</binding>" +
            //                    "</visual>" +
            //                "</toast>";


            //HttpWebResponse response = await ExecuteREST("POST", uri, SasToken, headers, body, "application/xml");


            //WNS toast
            WebHeaderCollection headersWNS = new WebHeaderCollection();
            headersWNS.Add("X-WNS-Type", "wns/toast");
            headersWNS.Add("ServiceBusNotification-Format", "windows");

            string bodyWNS = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>" +
                            "<toast>" +
                                "<visual>" +
                                    "<binding template=\"ToastText01\">" +
                                        "<text id=\"1\">" +
                                            "Test WNS from REST!" +
                                        "</text>" +
                                    "</binding>" +
                                "</visual>" +
                            "</toast>";


            HttpWebResponse response = await ExecuteREST("POST", uri, SasToken, headersWNS, bodyWNS, "application/xml");            

            Console.WriteLine("Content length is {0}", response.ContentLength);
            Console.WriteLine("Content type is {0}", response.ContentType);
            char[] seps1 = { '?' };
            char[] seps2 = { '/' };

            location = response.Headers.Get("Location");
            string[] locationUrl = location.Split(seps1);
            string[] locationParts = locationUrl[0].Split(seps2);

            notificationId = locationParts[locationParts.Length - 1];
            Console.WriteLine("Notification Id : " + notificationId + "\n");

            int wait = 1;
            Console.WriteLine("Waiting {0} min before trying to get telemetry...\n\n", wait.ToString());
            System.Threading.Thread.Sleep(wait * 60 * 1000);
            

            response = await GetNotificationTelemtry(notificationId, hubname, connectionString);
                                    
            // Get the stream associated with the response.
            Stream receiveStream = response.GetResponseStream();

            // Pipes the stream to a higher level stream reader with the required encoding format. 
            StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);

            Console.WriteLine("");

            DisplayResponseBody(response);

            readStream.Close();
            receiveStream.Close();

            return location;
        }

        private static async Task<HttpWebResponse> GetNotificationTelemtry(string id, string hubname, string connectionString)
        {
            string hubResource = "messages/" + id + "?";
            string apiVersion = "api-version=2015-04";
            ConnectionStringUtility connectionSasUtil = new ConnectionStringUtility(connectionString);

            //=== Generate SaS Security Token for Authentication header ===
            // Determine the targetUri that we will sign
            string uri = connectionSasUtil.Endpoint + hubname + "/" + hubResource + apiVersion;
            string SasToken = connectionSasUtil.getSaSToken(uri, 60);

            return await ExecuteREST("GET", uri, SasToken);
        }


        private static async Task<HttpWebResponse> GetInstallations(string hubName, string hubResource, string connectionString)
        {
            HttpWebResponse response  = null;
            ConnectionStringUtility connectionSasUtil = new ConnectionStringUtility(connectionString);

            if (hubResource == null)
                hubResource = "installations?";

            string apiVersion = "api-version=2015-04";

            //=== Generate SaS Security Token for Authentication header ===
            // Determine the targetUri that we will sign
            string uri = connectionSasUtil.Endpoint + hubName + "/" + hubResource + apiVersion;
            string SasToken = connectionSasUtil.getSaSToken(uri, 60);

            response = await ExecuteREST("GET", uri, SasToken);

            // BUG: Forcing the content type here because the response content type has been
            // application/xml even though the response body is application/json
            DisplayResponseBody(response, "application/json");

            return response;

        }

        private static async Task<HttpWebResponse> DeleteInstallation(string hubName, string hubResource, string connectionString)
        {
            HttpWebResponse response = null;
            ConnectionStringUtility connectionSasUtil = new ConnectionStringUtility(connectionString);

            //            string hubResource =  "installations/bfab4178-4bfd-4672-aba8-81cbafbeca7f?";
            string apiVersion = "api-version=2015-04";

            //=== Generate SaS Security Token for Authentication header ===
            // Determine the targetUri that we will sign
            string uri = connectionSasUtil.Endpoint + hubName + "/" + hubResource + apiVersion;
            string SasToken = connectionSasUtil.getSaSToken(uri, 60);

            response = await ExecuteREST("DELETE", uri, SasToken);

            DisplayResponseBody(response);

            return response;
        }

        private static async Task<HttpWebResponse> GetPlatformNotificationServiceFeedback(string hubName, string connectionString)
        {
            HttpWebResponse response = null;
            ConnectionStringUtility connectionSasUtil = new ConnectionStringUtility(connectionString);

            string hubResource =  "feedbackcontainer?";
            string apiVersion = "api-version=2015-04";

            //=== Generate SaS Security Token for Authentication header ===
            // Determine the targetUri that we will sign
            string uri = connectionSasUtil.Endpoint + hubName + "/" + hubResource + apiVersion;
            string SasToken = connectionSasUtil.getSaSToken(uri, 60);

            response = await ExecuteREST("GET", uri, SasToken);

            // Get the stream associated with the response.
            Stream receiveStream = response.GetResponseStream();

            // Pipes the stream to a higher level stream reader with the required encoding format. 
            StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);
            Console.WriteLine("");
            string containerUri = readStream.ReadToEnd();
            string listcontainerUri = containerUri + "&restype=container&comp=list";

            readStream.Close();
            receiveStream.Close();
            Console.WriteLine(containerUri);


            response = await ExecuteREST("GET", listcontainerUri, null);
            //DisplayResponseBody(response);

            // Get Blob name
            Stream receiveStreamContainer = null;
            StreamReader readStreamContainer = null;

            if (response.ContentType.Contains("application/xml"))
            {
                // Get the stream associated with the response.
                receiveStreamContainer = response.GetResponseStream();

                // Pipes the stream to a higher level stream reader with the required encoding format. 
                readStreamContainer = new StreamReader(receiveStreamContainer, Encoding.UTF8);

                if (readStreamContainer != null)
                {
                    XmlDocument xml = new XmlDocument();
                    xml.LoadXml(readStreamContainer.ReadToEnd());
                    readStreamContainer.Close();
                    receiveStreamContainer.Close();

                    StringBuilder sb = new StringBuilder();
                    XmlWriterSettings settings = new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "  ",
                        NewLineChars = "\r\n",
                        NewLineHandling = NewLineHandling.Replace
                    };

                    using (XmlWriter writer = XmlWriter.Create(sb, settings))
                    {
                        xml.Save(writer);
                    }

                    Console.WriteLine(sb.ToString() + "\n\n");


                    XmlNodeList list = xml.GetElementsByTagName("Blob");

                    string[] parts = null;
                    char[] seps = {'?'};
                    string blobURL = null;

                    foreach(XmlNode node in list)
                    {
                        Console.WriteLine("Get Blob named : " + node["Name"].InnerText);
                        parts = containerUri.Split(seps);
                        blobURL = parts[0] + "/" + node["Name"].InnerText + "?" + parts[1];
                        Console.WriteLine("Blob URL : " + blobURL);
                        response = await ExecuteREST("GET", blobURL, null);
                        DisplayResponseBody(response);
                    }


                }

            }

            return response;

        }

        private static void DisplayResponseBody(HttpWebResponse response, string forcedType = null)
        {
            if (response == null)
                return;

            string contentType = response.ContentType;
            if (forcedType != null)
                contentType = forcedType;

            // Get the stream associated with the response.
            Stream receiveStream = response.GetResponseStream();

            // Pipes the stream to a higher level stream reader with the required encoding format. 
            StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);

            Console.WriteLine("");

            if (receiveStream == null)
                return;


            
            if (contentType.Contains("application/octet-stream"))
            {
                string xmlFiles = readStream.ReadToEnd();
                string[] sseps = { "<?xml " };
                string[] docs = xmlFiles.Split(sseps, StringSplitOptions.RemoveEmptyEntries);

                StringBuilder sb = null;
                XmlDocument xml = null;
                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = "\r\n",
                    NewLineHandling = NewLineHandling.Replace
                };

                foreach (string doc in docs)
                {
                    xml = new XmlDocument();
                    xml.LoadXml(sseps[0] + doc);
                    sb = new StringBuilder();

                    using (XmlWriter writer = XmlWriter.Create(sb, settings))
                    {
                        xml.Save(writer);
                    }

                    Console.WriteLine(sb.ToString() + "\n");
                }
            }

            if (contentType.Contains("application/xml"))
            {
                XmlDocument xml = new XmlDocument();
                xml.LoadXml(readStream.ReadToEnd());

                StringBuilder sb = new StringBuilder();
                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = "\r\n",
                    NewLineHandling = NewLineHandling.Replace
                };

                using (XmlWriter writer = XmlWriter.Create(sb, settings))
                {
                    xml.Save(writer);
                }

                Console.WriteLine(sb.ToString());
            }

            if (contentType.Contains("application/json"))
            {
                Console.WriteLine(JsonHelper.FormatJson(readStream.ReadToEnd()));
            }

            readStream.Close();
            receiveStream.Close();
        }


        private static async Task<HttpWebResponse> ExecuteREST(string httpMethod, string uri, string sasToken, WebHeaderCollection headers = null, string body = null, string contentType = "application/json")
        {
            //=== Execute the request 
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);
            HttpWebResponse response = null;

            request.Method = httpMethod;
            request.ContentType = contentType;
            request.ContentLength = 0;
            
            if (sasToken != null)
                request.Headers.Add("Authorization", sasToken);

            if (headers != null)
            {
                request.Headers.Add(headers);
            }

            if (body != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(body);

                try
                {
                    request.ContentLength = bytes.Length;
                    Stream requestStream = request.GetRequestStream();
                    requestStream.Write(bytes, 0, bytes.Length);
                    requestStream.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            try
            {
                response = (HttpWebResponse)await request.GetResponseAsync();
            }
            catch (WebException we)
            {
                if (we.Response != null)
                {
                    response = (HttpWebResponse)we.Response;
                }
                else
                    Console.WriteLine(we.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return response;
        }

    }


    class JsonHelper
    {
        private const string INDENT_STRING = "  ";
        public static string FormatJson(string str)
        {
            var indent = 0;
            var quoted = false;
            var sb = new StringBuilder();
            for (var i = 0; i < str.Length; i++)
            {
                var ch = str[i];
                switch (ch)
                {
                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            Enumerable.Range(0, ++indent).ForEach(item => sb.Append(INDENT_STRING));
                        }
                        break;
                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            sb.AppendLine();
                            Enumerable.Range(0, --indent).ForEach(item => sb.Append(INDENT_STRING));
                        }
                        sb.Append(ch);
                        break;
                    case '"':
                        sb.Append(ch);
                        bool escaped = false;
                        var index = i;
                        while (index > 0 && str[--index] == '\\')
                            escaped = !escaped;
                        if (!escaped)
                            quoted = !quoted;
                        break;
                    case ',':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            Enumerable.Range(0, indent).ForEach(item => sb.Append(INDENT_STRING));
                        }
                        break;
                    case ':':
                        sb.Append(ch);
                        if (!quoted)
                            sb.Append(" ");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }

    static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> ie, Action<T> action)
        {
            foreach (var i in ie)
            {
                action(i);
            }
        }
    }
}
