using Hermes.Application.ApiService;
using Hermes.Application.ApiService.Interface;
using Hermes.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class ApiController(IApiService apiService) : ControllerBase
    {
        [HttpPost("add/user")]
        public async Task<ActionResult> PostAddUser([FromBody] User request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.Name))
                return BadRequest("Name is required.");

            // Hier rufst du jetzt deinen Service in der Application-Schicht auf
            // var result = await _userService.RegisterUserAsync(request, cancellationToken);

            return Ok(new { Message = "User received", UserEmail = request.Email });
        }


    }
}
