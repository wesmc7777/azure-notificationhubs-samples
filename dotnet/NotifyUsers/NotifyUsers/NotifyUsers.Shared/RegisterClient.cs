using System;
using System.Collections.Generic;
using System.Text;


using Windows.Storage;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Linq;

namespace NotifyUsers
{
    class RegisterClient
    {
        private string POST_URL;
        private string BACKENDENDPOINT;

        private class DeviceRegistration
        {
            public string Platform { get; set; }
            public string Handle { get; set; }
            public string[] Tags { get; set; }
        }
        
        private class DeviceInstallation
        {
            public string InstallationId { get; set; }
            public string Platform { get; set; }
            public string Handle { get; set; }
            public string[] Tags { get; set; }
        }



        public RegisterClient(string backendEndpoint)
        {
            BACKENDENDPOINT = backendEndpoint;
            POST_URL = BACKENDENDPOINT + "/api/register";
        }
        

        public async Task RegisterInstallationAsync(string handle, IEnumerable<string> tags)
        {
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
                InstallationId = installationId,
                Platform = "wns",
                Handle = handle,
                Tags = tags.ToArray<string>()
            };

            var statusCode = await CreateOrUpdateInstallationAsync(deviceInstallation);

            if (statusCode != HttpStatusCode.Accepted)
            {
                // log or throw
            }
        }


        private async Task<HttpStatusCode> CreateOrUpdateInstallationAsync(DeviceInstallation deviceInstallation)
        {
            string uri = BACKENDENDPOINT + "/api/installation";

            using (var httpClient = new HttpClient())
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", (string)settings["AuthenticationToken"]);

                string json = JsonConvert.SerializeObject(deviceInstallation);
                var response = await httpClient.PutAsync(uri, new StringContent(json, Encoding.UTF8, "application/json"));
                return response.StatusCode;
            }
        }


        public async Task RegisterAsync(string handle, IEnumerable<string> tags)
        {
            var regId = await RetrieveRegistrationIdOrRequestNewOneAsync();

            var deviceRegistration = new DeviceRegistration
            {
                Platform = "wns",
                Handle = handle,
                Tags = tags.ToArray<string>()
            };

            var statusCode = await UpdateRegistrationAsync(regId, deviceRegistration);

            if (statusCode == HttpStatusCode.Gone)
            {
                // regId is expired, deleting from local storage & recreating
                var settings = ApplicationData.Current.LocalSettings.Values;
                settings.Remove("__NHRegistrationId");
                regId = await RetrieveRegistrationIdOrRequestNewOneAsync();
                statusCode = await UpdateRegistrationAsync(regId, deviceRegistration);
            }

            if (statusCode != HttpStatusCode.Accepted)
            {
                // log or throw
            }
        }


        private async Task<HttpStatusCode> UpdateRegistrationAsync(string regId, DeviceRegistration deviceRegistration)
        {
            using (var httpClient = new HttpClient())
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", (string)settings["AuthenticationToken"]);

                var putUri = POST_URL + "/" + regId;

                string json = JsonConvert.SerializeObject(deviceRegistration);
                var response = await httpClient.PutAsync(putUri, new StringContent(json, Encoding.UTF8, "application/json"));
                return response.StatusCode;
            }
        }

        private async Task<string> RetrieveRegistrationIdOrRequestNewOneAsync()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;

            // If we have not stored a registration id in application data, retrieve one
            // and store in application data.
            if (!settings.ContainsKey("__NHRegistrationId"))
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", (string)settings["AuthenticationToken"]);

                    var response = await httpClient.PostAsync(POST_URL, new StringContent(""));
                    if (response.IsSuccessStatusCode)
                    {
                        string regId = await response.Content.ReadAsStringAsync();
                        regId = regId.Substring(1, regId.Length - 2);
                        settings.Add("__NHRegistrationId", regId);
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
            }
            return (string)settings["__NHRegistrationId"];

        }
    }
}
