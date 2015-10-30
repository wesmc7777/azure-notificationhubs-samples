using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

using Microsoft.Azure.NotificationHubs;
using AppBackend.Models;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs.Messaging;
using System.Web;



namespace AppBackend.Controllers
{
    public class InstallationController : ApiController
    {
        private NotificationHubClient hub;

        public class DeviceInstallation
        {
            public string InstallationId { get; set; }
            public string Platform { get; set; }
            public string Handle { get; set; }
            public string[] Tags { get; set; }
        }

        public InstallationController()
        {
            hub = Notifications.Instance.Hub;
        }


        // POST api/installation
        // This creates or updates an installation
        public async Task<HttpResponseMessage> Put(DeviceInstallation deviceUpdate)
        {

            Installation installation = new Installation();
            installation.InstallationId = deviceUpdate.InstallationId;
            installation.PushChannel = deviceUpdate.Handle;
            installation.Tags = deviceUpdate.Tags;

            switch (deviceUpdate.Platform)
            {
                case "mpns":
                    installation.Platform = NotificationPlatform.Mpns;
                    break;
                case "wns":
                    installation.Platform = NotificationPlatform.Wns;
                    break;
                case "apns":
                    installation.Platform = NotificationPlatform.Apns;
                    break;
                case "gcm":
                    installation.Platform = NotificationPlatform.Gcm;
                    break;
                default:
                    throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            //var username = HttpContext.Current.User.Identity.Name;

            // In the backend we can control if a user is allowed to add tags
            //installation.Tags = new List<string>(deviceUpdate.Tags);
            //installation.Tags.Add("username:" + username);

            await hub.CreateOrUpdateInstallationAsync(installation);

            return Request.CreateResponse(HttpStatusCode.OK);
        }


    }
}
