using MediatR;
using Microsoft.AspNetCore.Mvc;
using FluentResults;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

// Genericized imports for demonstration
using Project.Application.CamundaInterface.Command;
using Project.Core.Models.Dto;

namespace Project.Api.Controllers
{
    /// <summary>
    /// Provides API endpoints for interacting with the external Camunda system.
    /// Handles starting processes, completing tasks, and coordinating between the API and MediatR command handlers.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class CamundaInterfaceController : BaseApiController
    {
        /// <summary>
        /// Initializes a new instance of the Camunda interface controller.
        /// </summary>
        /// <param name="mediator">MediatR mediator instance used to dispatch commands.</param>
        public CamundaInterfaceController(IMediator mediator) : base(mediator)
        {
        }

        /// <summary>
        /// Starts a Camunda process via a message trigger.
        /// Sends a command through the MediatR pipeline to the appropriate handler.
        /// </summary>
        /// <param name="command">Command object containing process parameters and metadata.</param>
        /// <returns>Result containing process start information or an error.</returns>
        [HttpPost("StartProcessViaMessage")]
        [ProducesResponseType(typeof(Result<MessageDto>), StatusCodes.Status200OK)]
        [Produces("application/json")]
        public async Task<ActionResult<Result<MessageDto>>> StartProcessViaMessage(CamundaMessageCommand command)
        {
            Console.WriteLine("[INFO] Processing Camunda message request...");

            Result<MessageDto> result = await Mediator.Send(command);

            if (result.IsSuccess)
                return Ok(result);
            else
                return BadRequest(result.ToResult());
        }

        /// <summary>
        /// Completes a Camunda task with the provided variable payload.
        /// This endpoint forwards data to the internal command handler responsible for form normalization and task completion.
        /// </summary>
        /// <param name="command">Command containing task identifier and related form data.</param>
        /// <returns>Result containing the next task context or an error.</returns>
        [HttpPost("CompleteTask")]
        [ProducesResponseType(typeof(Result<TaskCompletionDto>), StatusCodes.Status200OK)]
        [Produces("application/json")]
        public async Task<ActionResult<Result<TaskCompletionDto>>> CompleteTask(CamundaCompleteTaskCommand command)
        {
            Result<TaskCompletionDto> result = await Mediator.Send(command);

            if (result.IsSuccess)
                return Ok(result);
            else
                return BadRequest(result.ToResult());
        }
    }
}
