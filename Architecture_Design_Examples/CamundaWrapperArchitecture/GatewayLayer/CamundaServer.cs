using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using oc.CamundaWrapper.Core.Models.Dto.CamundaInterface;
using oc.CamundaWrapper.Gateway.Utility;
using oc.Logging;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace oc.CamundaWrapper.Gateway.CamundaServices
{
    public class CamundaServer : ICamundaServer
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<CamundaServer> _logger;
        private readonly IConfiguration _configuration;
        private readonly IApiClient _apiClient;
        private readonly IMemoryCache _cache;
        public CamundaServer(
            IHttpClientFactory clientFactory,
            ILogger<CamundaServer> logger,
            IConfiguration configuration,
            IApiClient apiClient,
            IMemoryCache cache
            )
        {
            _clientFactory = clientFactory;
            _logger = logger;
            _configuration = configuration;
            _apiClient = apiClient;
            _cache = cache;
        }

        public async Task<MessageRes> StartProcessViaMessage(MessageReq messageReq)
        {
            string endpoint = CamundaServicesConst.BaseUrl + CamundaServicesConst.MessageUrl;
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            };
            var requestBody = System.Text.Json.JsonSerializer.Serialize(messageReq);
            var res = await HttpClientHelper.MakeHttpRequest(endpoint, RestSharp.Method.Post, headers, requestBody);
            if (res.ErrorMessage != null)
            {
                _logger.LogError(message: res.ErrorMessage);
                throw new Exception(res.ErrorMessage);
            }
            var deserializedRes = JsonConvert.DeserializeObject<MessageRes>(res.Result!.TrimStart('[').TrimEnd(']'));

            return deserializedRes;
        }

        public async Task<MessageRes> StartProcessViaMessage(string messageJson)
        {
            string endpoint = CamundaServicesConst.BaseUrl + CamundaServicesConst.MessageUrl;
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            };
            // var requestBody = System.Text.Json.JsonSerializer.Serialize(messageReq);
            var res = await HttpClientHelper.MakeHttpRequest(endpoint, RestSharp.Method.Post, headers, messageJson);
            var deserializedRes = JsonConvert.DeserializeObject<MessageRes>(res.Result!.TrimStart('[').TrimEnd(']'));
            if (res.ErrorMessage != null)
            {
                _logger.LogError(message: res.ErrorMessage);
                throw new Exception(res.ErrorMessage);
            }

            return deserializedRes;
        }

        public async Task<GetNextTaskRes> GetNextTask(GetNextTaskReq req)
        {
            int maxRetries = 3;
            int retryCount = 0;

            string endpoint = CamundaServicesConst.BaseUrl + CamundaServicesConst.TaskUrl;
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            };

            var queryParams = new Dictionary<string, string>
            {
                { "processInstanceId", $"{req.ProcessInstanceId}"}
            };

            var queryString = new FormUrlEncodedContent(queryParams).ReadAsStringAsync().Result;
            string requestUrl = $"{endpoint}?{queryString}";

            while (retryCount < maxRetries)
            {
                var res = await HttpClientHelper.MakeHttpRequest(requestUrl, RestSharp.Method.Get, headers);
                var deserializedRes = JsonConvert.DeserializeObject<GetNextTaskRes>(res.Result!.TrimStart('[').TrimEnd(']'), MicrosoftDateFormatSettings);

                if (deserializedRes != null)
                {
                    return deserializedRes;
                }
                else if (res.ErrorMessage != null)
                {
                    _logger.LogError(message: res.ErrorMessage);
                    throw new Exception(res.ErrorMessage);
                }

                retryCount++;
                if (retryCount < maxRetries)
                {
                    _logger.LogWarning($"Retrying GetNextTask request. Retry count: {retryCount}");
                    await Task.Delay(500); // Optional: Add a delay before retrying
                }
            }
            _logger.LogError(message: "GetNextTask failed after maximum retries.");
            throw new Exception("GetNextTask failed after maximum retries.");
        }

        private static JsonSerializerSettings MicrosoftDateFormatSettings =>
        new JsonSerializerSettings
        {
            DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
            DateParseHandling = DateParseHandling.None
        };


        public async Task<ExpandoObject> CompleteTask(CompleteTaskReq req, string taskID)
        {
            string endpoint = CamundaServicesConst.BaseUrl + CamundaServicesConst.TaskUrl + $"/{taskID}" + CamundaServicesConst.CompleteTaskUrl;
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            };
            var requestBody = JsonConvert.SerializeObject(req);
            dynamic res = await HttpClientHelper.MakeHttpRequest(endpoint, RestSharp.Method.Post, headers, requestBody);
            if (res.ErrorMessage != null)
            {
                _logger.LogError(message: res.ErrorMessage);
                throw new Exception(res.ErrorMessage);
            }

            var deserializedRes = JsonConvert.DeserializeObject<ExpandoObject>(
                res.Result,
                MicrosoftDateFormatSettings
            );
            return deserializedRes;
        }



        public async Task<ExpandoObject> GetFormVariables(FormVariablesReq req)
        {
            string endpoint = CamundaServicesConst.BaseUrl + CamundaServicesConst.TaskUrl + $"/{req.TaskId}" + CamundaServicesConst.GetTaskForm;
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            };

            var res = await HttpClientHelper.MakeHttpRequest(endpoint, RestSharp.Method.Get, headers);
            if (res.ErrorMessage != null)
            {
                _logger.LogError(message: res.ErrorMessage);
                throw new Exception(res.ErrorMessage);
            }

            var deserializedRes = JsonConvert.DeserializeObject<ExpandoObject>(res.Result, MicrosoftDateFormatSettings);

            return deserializedRes;
        }

        /// <summary>
        /// Gets all the tasks for the required assignee.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>

        public async Task<dynamic> GetTasksForAssignee(GetTasksForAssigneeReq req)
        {
            string endpoint = CamundaServicesConst.BaseUrl + CamundaServicesConst.TaskUrl + $"?assignee={req.Assignee}";
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            };

            var res = await HttpClientHelper.MakeHttpRequest(endpoint, RestSharp.Method.Get, headers);
            var deserializedRes = JsonConvert.DeserializeObject<List<Dictionary<string, dynamic>>>(res.Result);
            if (res.ErrorMessage != null)
            {
                _logger.LogError(message: res.ErrorMessage);
                throw new Exception(res.ErrorMessage);
            }
            //var finalResult = JsonConvert.DeserializeObject<ExpandoObject>(res.Result);
            return deserializedRes;
        }
    }
}
