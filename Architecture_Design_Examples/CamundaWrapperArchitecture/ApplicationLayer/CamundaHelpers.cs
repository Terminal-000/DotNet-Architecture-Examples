using FluentResults;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// Core model and service imports (genericized)
using Project.Core.Models.Base;
using Project.Core.Models.Dto;
using Project.Core.Models.Utility;
using Project.Gateway.ExternalServices;

namespace Project.Application.InternalCommands
{
    /// <summary>
    /// Provides internal command implementations for interacting with an external Camunda engine.
    /// Wraps API calls, data transformation, and helper utilities for structured communication.
    /// </summary>
    public class CamundaHelpers
    {
        /// <summary>
        /// Requests and retrieves an authentication token from the Camunda server.
        /// Demonstrates how a message-based process is triggered to return a token variable.
        /// </summary>
        /// <param name="server">Injected instance of the Camunda server interface.</param>
        /// <returns>Authentication token as a string.</returns>
        public async Task<string> GetCamundaToken(ICamundaServer server)
        {
            var result = new Result<MessageDto>();

            // Build message request payload
            var request = new MessageReq
            {
                ResultEnabled = true,
                VariablesInResultEnabled = true,
                BusinessKey = "1",
                MessageName = "getToken",
                ProcessVariables = new ExpandoObject()
            };

            // Example placeholder variables for demonstration
            request.ProcessVariables.username = new VariableModel { value = "user_value", type = "String" };
            request.ProcessVariables.grant_type = new VariableModel { value = "client_credentials", type = "String" };
            request.ProcessVariables.scope = new VariableModel { value = "requested_permissions", type = "String" };
            request.ProcessVariables.client_id = new VariableModel { value = "demo_client", type = "String" };
            request.ProcessVariables.auth_code = new VariableModel { value = "example_authorization_code", type = "String" };

            // Start the process and wait for token response
            var output = await server.StartProcessViaMessage(request);

            // Extract the token variable from returned output
            var tokenSection = output.Variables.FirstOrDefault(v => v.Key == "token");
            var token = tokenSection.Value.Value;

            Console.WriteLine($"[INFO] Token retrieved successfully: {token}");

            return token;
        }

        /// <summary>
        /// Retrieves the next task in the Camunda process (placeholder for demonstration).
        /// </summary>
        /// <param name="server">Camunda server interface.</param>
        /// <returns>Task identifier as string (if applicable).</returns>
        public async Task<string> GetNextTask(ICamundaServer server)
        {
            string result = "";
            return result;
        }

        /// <summary>
        /// Retrieves the form variables for a given Camunda task.
        /// Converts a dynamic response into a strongly typed dictionary for further processing.
        /// </summary>
        /// <param name="server">Camunda server interface.</param>
        /// <param name="taskId">Unique identifier of the Camunda task.</param>
        /// <returns>Dictionary of form variables.</returns>
        public async Task<Dictionary<string, VariableResponseModel>> GetFormVariables(ICamundaServer server, string taskId)
        {
            // Request form variables from server
            dynamic response = await server.GetFormVariables(new FormVariablesReq { TaskId = taskId });

            // Serialize and deserialize to create a dictionary structure
            string json = JsonConvert.SerializeObject(response);
            var formVariables = JsonConvert.DeserializeObject<Dictionary<string, VariableResponseModel>>(json);

            return formVariables!;
        }

        /// <summary>
        /// Updates the JSON representation of a Camunda form.
        /// Cleans property naming inconsistencies and restructures component hierarchy.
        /// </summary>
        /// <param name="server">Camunda server interface.</param>
        /// <param name="taskId">Task identifier.</param>
        /// <param name="currentFormDict">Current form variable dictionary.</param>
        /// <returns>Normalized and processed JSON string.</returns>
        public async Task<string> UpdateFormJson(
            ICamundaServer server,
            string taskId,
            Dictionary<string, VariableResponseModel> currentFormDict)
        {
            string result = "";

            if (currentFormDict == null || !currentFormDict.ContainsKey("viewJson"))
                return result;

            // Normalize property naming for consistent JSON structure
            string modifiedJson = Regex.Replace(
                currentFormDict["viewJson"].value,
                @"""(\w+)Properties""\s*:",
                @"""Properties"":"
            );

            // Deserialize into strongly typed structure
            FrontendSpecificViewModel? frontendComponents =
                JsonConvert.DeserializeObject<FrontendSpecificViewModel>(modifiedJson);

            // Optionally update component values if needed
            // UpdateComponentValues(frontendComponents, currentFormDict);

            result = JsonConvert.SerializeObject(frontendComponents);

            // Create a hierarchical (nested) representation of the form
            var nestedOutput = CreateNestedJson(result);

            // Rename properties based on their component type
            var fixedJson = RenamePropertiesByType(nestedOutput);

            return fixedJson;
        }

        /// <summary>
        /// Recursively updates component values from form variables (optional helper).
        /// </summary>
        void UpdateComponentValues(dynamic component, Dictionary<string, VariableResponseModel> currentFormDict)
        {
            if (component.Type == "labelDict")
            {
                string fieldName = component.Properties["submitRequiredFields"]["fieldName"]?.ToString() ?? "";

                if (currentFormDict.ContainsKey(fieldName) && currentFormDict[fieldName].value != null)
                {
                    component.Properties["submitRequiredFields"]["value"] = currentFormDict[fieldName].value.ToString();
                }
            }

            if (component.Properties != null && component.Properties.ContainsKey("items"))
            {
                foreach (var item in component.Properties["items"])
                {
                    UpdateComponentValues(item, currentFormDict);
                }
            }
        }

        /// <summary>
        /// Converts a flat component list into a nested JSON hierarchy based on parent-child relationships.
        /// </summary>
        /// <param name="json">Flat JSON input string.</param>
        /// <returns>Nested hierarchical JSON string.</returns>
        string CreateNestedJson(string json)
        {
            var root = JsonConvert.DeserializeObject<FrontendSpecificViewModel>(json);

            // Map each component by its unique identifier
            var componentsMap = new Dictionary<string, Component>();

            foreach (var componentData in root!.componentsList)
            {
                var component = new Component
                {
                    Type = componentData.Type,
                    ComponentId = componentData.ComponentId,
                    ParentComponentId = componentData.ParentComponentId,
                    Properties = componentData.Properties
                };
                componentsMap[component.ComponentId] = component;
            }

            // Identify root-level components
            var reorganizedComponents = new List<Component>();

            foreach (var component in root.componentsList)
            {
                if (component.ParentComponentId == null)
                {
                    reorganizedComponents.Add(componentsMap[component.ComponentId]);
                }
                else
                {
                    var parentComponent = componentsMap[component.ParentComponentId];
                    parentComponent.Items.Add(componentsMap[component.ComponentId]);
                }
            }

            // Serialize reorganized structure
            string outputJson = JsonConvert.SerializeObject(new
            {
                processName = root.ProcessName,
                processLabel = root.ProcessLabel,
                formName = root.FormName,
                formLabel = root.FormLabel,
                componentsList = reorganizedComponents
            }, Formatting.None);

            Console.WriteLine(outputJson);
            return outputJson;
        }

        /// <summary>
        /// Recursively renames the 'Properties' object in each component to match the component type.
        /// Example: a component of type 'input' will have 'inputProperties' instead of 'Properties'.
        /// </summary>
        /// <param name="inputJson">JSON string representing components.</param>
        /// <returns>JSON string with renamed properties.</returns>
        public string RenamePropertiesByType(string inputJson)
        {
            var root = JObject.Parse(inputJson);

            void ProcessComponents(JArray components)
            {
                foreach (var component in components.Children<JObject>())
                {
                    // Rename 'Properties' based on component type
                    if (component.ContainsKey("Properties") && component["Properties"]?.Type != JTokenType.Null)
                    {
                        string type = component["type"]?.ToString();
                        if (!string.IsNullOrEmpty(type))
                        {
                            var propValue = component["Properties"];
                            component.Remove("Properties");
                            component[$"{type}Properties"] = propValue;
                        }
                    }

                    // Recursively process child components
                    if (component.ContainsKey("items") && component["items"] is JArray childItems)
                    {
                        ProcessComponents(childItems);
                    }
                }
            }

            // Begin processing at root level
            var componentsList = root["componentsList"] as JArray;
            if (componentsList != null)
            {
                ProcessComponents(componentsList);
            }

            // Return compact (minified) JSON
            return root.ToString(Formatting.None);
        }
    }
}
