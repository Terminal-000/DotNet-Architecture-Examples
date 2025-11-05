using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// Genericized imports for abstraction (renamed for documentation clarity)
using Project.Application.CamundaInterface.Command;
using Project.Application.InternalCommands;
using Project.Core.Helpers;
using Project.Core.Models.Base;
using Project.Core.Models.Dto;
using Project.Core.Models.Utility;
using Project.Gateway.ExternalServices;
using Project.Logging;

namespace Project.Application.CamundaInterface.CommandHandler
{
    /// <summary>
    /// Handles task completion commands for the external Camunda system.
    /// Orchestrates form variable preparation, task submission, and fetching the next task context.
    /// </summary>
    public class CamundaCompleteTaskCommandHandler : IRequestHandler<CamundaCompleteTaskCommand, Result<TaskCompletionDto>>
    {
        private readonly ICamundaServer _CamundaServer;
        private readonly ICamundaInternalCommands _CamundaInternalCommands;
        private readonly IRsaService _rsaService;
        private readonly IAesService _aesService;
        private readonly IHttpContextAccessor _context;
        private readonly Microsoft.Extensions.Logging.ILogger<CamundaCompleteTaskCommandHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the task completion handler.
        /// </summary>
        public CamundaCompleteTaskCommandHandler(
            ICamundaServer CamundaServer,
            ICamundaInternalCommands CamundaInternalCommands,
            IRsaService rsaService,
            IAesService aesService,
            IHttpContextAccessor context,
            Microsoft.Extensions.Logging.ILogger<CamundaCompleteTaskCommandHandler> logger)
        {
            _CamundaServer = CamundaServer;
            _CamundaInternalCommands = CamundaInternalCommands;
            _rsaService = rsaService;
            _aesService = aesService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Handles Camunda task completion, submits required form data,
        /// and retrieves the next available task from the Camunda engine.
        /// </summary>
        /// <param name="request">Command containing task and variable data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result containing information about the next task or failure details.</returns>
        public async Task<Result<TaskCompletionDto>> Handle(CamundaCompleteTaskCommand request, CancellationToken cancellationToken)
        {
            // Retrieve user info from request context
            LogUserInfoModel userInfo = (LogUserInfoModel)_context.HttpContext!.Items["UserInfo"]!;

            var result = new Result<TaskCompletionDto>();

            // Build completion request
            var completeTaskRequest = new CompleteTaskReq
            {
                WithVariablesInReturn = true,
                Variables = new ExpandoObject()
            };

            // Normalize incoming JSON variable structure
            string normalizedJson = Regex.Replace(
                request.Variables,
                @"""(\w+)Properties""\s*:",
                @"""Properties"":"
            );

            // Deserialize normalized JSON into structured view model
            FrontendSpecificViewModel deserializedView = JsonConvert.DeserializeObject<FrontendSpecificViewModel>(normalizedJson)!;

            // Identify required components that need to be submitted
            List<Component> requiredComponents = new List<Component>();

            foreach (Component component in deserializedView.componentsList)
            {
                if (component.Properties == null)
                    continue;

                // Process components with button groups
                if (component.Properties.ContainsKey("buttons"))
                {
                    string selectedOption = string.Empty;

                    // Extract selected radio/option value
                    foreach (var field in component.Properties["submitRequiredFields"])
                    {
                        selectedOption = (string)field.FirstOrDefault()!["value"]!;
                    }

                    // Identify matching button by ID
                    var buttons = component.Properties["buttons"];
                    var selectedButton = buttons.FirstOrDefault(x => (string)x["id"]! == selectedOption);

                    if (selectedButton != null)
                    {
                        foreach (dynamic buttonItem in selectedButton["items"]!)
                        {
                            if (buttonItem.Properties != null && buttonItem.Properties.ContainsKey("submitRequiredFields"))
                            {
                                var jObject = (JObject)buttonItem;
                                requiredComponents.Add(jObject.ToObject<Component>()!);
                            }
                        }
                    }
                }

                // Add components with directly defined required fields
                if (component.Properties.ContainsKey("submitRequiredFields"))
                {
                    requiredComponents.Add(component);
                }
            }

            // Prepare variable dictionary for task completion
            var variableDictionary = (IDictionary<string, object>)completeTaskRequest.Variables;

            // Fetch existing form variables for context
            var currentFormVars = await _CamundaInternalCommands.GetFormVariables(_CamundaServer, request.TaskId);

            // Populate required fields into the variable dictionary
            foreach (var component in requiredComponents)
            {
                foreach (var field in component.Properties["submitRequiredFields"])
                {
                    if ((bool)field.FirstOrDefault()!["isRequired"]! == true)
                    {
                        var value = field.FirstOrDefault()!["value"]!;
                        var key = field.Path;
                        variableDictionary[$"{key}"] = new Value { value = value };
                    }
                }
            }

            // Example static placeholders for non-sensitive environment metadata
            variableDictionary["clientIdentifier"] = new Value { value = "example-client-id" };
            variableDictionary["clientAddress"] = new Value { value = "127.0.0.1" };

            // Attach variables and finalize request
            completeTaskRequest.Variables = variableDictionary;
            completeTaskRequest.WithVariablesInReturn = true;

            // Submit task completion to Camunda server
            dynamic mainOutput = await _CamundaServer.CompleteTask(completeTaskRequest, request.TaskId);

            // Retrieve the next task after completion
            var nextTask = await _CamundaServer.GetNextTask(new GetNextTaskReq
            {
                ProcessInstanceId = request.ProcessInstanceId
            });

            if (nextTask != null)
            {
                // Retrieve and update next form structure
                var nextFormVars = await _CamundaInternalCommands.GetFormVariables(_CamundaServer, nextTask.id);
                string updatedJson = await _CamundaInternalCommands.UpdateFormJson(_CamundaServer, nextTask.id, nextFormVars);

                Console.WriteLine($"{updatedJson}");

                return new TaskCompletionDto
                {
                    NextTaskId = nextTask.id,
                    NextTaskName = nextTask.name,
                    viewJson = updatedJson
                };
            }
            else
            {
                return Result.Fail<TaskCompletionDto>(new FluentResults.Error("Failed to fetch the next form data."));
            }
        }
    }
}
