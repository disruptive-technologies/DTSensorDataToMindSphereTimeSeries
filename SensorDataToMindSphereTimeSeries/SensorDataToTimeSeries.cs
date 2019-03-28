using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SensorDataToMindSphereTimeSeries.Models;

namespace SensorDataToMindSphereTimeSeries
{
    public static class SensorDataToTimeSeries
    {


        [FunctionName("SensorDataToTimeSeries")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {

            /*
             * Use MindSphere Service Credentials to get access token.
             * tenant: You MindSphere Tenant name
             */
            var userName = Environment.GetEnvironmentVariable("service_credentials_username");
            var password = Environment.GetEnvironmentVariable("secret_key");
            var tenantName = Environment.GetEnvironmentVariable("tenantName");
            var accessToken = await GetMindSphereAccessToken(userName, password, tenantName);

            /*
             * Read and deserialize HTTP content into a Device object.
             */
            var content = req.Content.ReadAsStringAsync().Result;
            var device = JsonConvert.DeserializeObject<Device>(content);

            var sensorId = GetSensorIdByDevice(device);
            var sensorValue = GetSensorValueByDeviceType(device);

            /*
             *Creating a time series object
             */
            var timeSeriesObject = new List<object>
            {
                new
                {
                    _time = device.Event.Timestamp,
                    value = sensorValue
                }
            };

            /*
             * In this example, we are using the sensor id as the aspect name.
             * If you choose to create an Aspect in MindSphere with a different name,
             * you can for example set up a DB where you can find which AssetId and aspectName the sensorId belongs to.
             */
            var assetId = "";
            var aspectName = sensorId;
            var baseUrl = "https://gateway.eu1.mindsphere.io/api";
            var url = $"{baseUrl}/iottimeseries/v3/timeseries/{assetId}/{aspectName}";

            var request = GetRequest(url, HttpMethod.Put, accessToken);
            request.Content = new StringContent(
                JsonConvert.SerializeObject(timeSeriesObject),
                Encoding.UTF8, "application/json");

            var response = await new HttpClient().SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
               return req.CreateResponse(HttpStatusCode.OK);
            }
            
               return req.CreateResponse(HttpStatusCode.BadRequest, "Something went wrong, sensor data was not pushed to MindSphere time series");
        }


        private static string GetSensorIdByDevice(Device device)
        {
            return device.Event.TargetName.Split('/').LastOrDefault();
        }

        private static string GetSensorValueByDeviceType(Device device)
        {

            if (device.Event.EventType.Contains("temperature"))
            {
                return device.Event.Data.Temperature.Value;
            }

            if (device.Event.EventType.Contains("objectPresent"))
            {
                return device.Event.Data.ObjectPresent.State.Equals("PRESENT").ToString();
            }

            return null;
        }

        public static HttpRequestMessage GetRequest(string baseUrl, HttpMethod method, string accessToekn)
        {
            if (!string.IsNullOrEmpty(accessToekn))
            {
                var request = new HttpRequestMessage(method, baseUrl);
                request.Headers.Add("Authorization", "Bearer " + accessToekn);
                return request;
            }

            return null;
        }

        public static async Task<string> GetMindSphereAccessToken(string username, string password, string tenant)
        {
            var oAuthUrl = $"https://{tenant}.piam.eu1.mindsphere.io/oauth/token";
            var request = new HttpRequestMessage(HttpMethod.Post, oAuthUrl);

            var basicToken = Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);

            var contentPairs = new List<KeyValuePair<string, string>>();
            contentPairs.Add(new KeyValuePair<string, string>("grant_type", "client_credentials"));

            var content = new FormUrlEncodedContent(contentPairs);
            request.Content = content;

            var response = await new HttpClient().SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                var json = JsonConvert.DeserializeObject<JObject>(result);
                var accessToken = json["access_token"].Value<string>();
                return accessToken;

            }

            return null;

        }
    }
}
