using JobAgent.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JobAgent.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CompanyController : ControllerBase
    {
        private readonly RegonSettings _settings;

        public CompanyController(IOptions<RegonSettings> settings)
        {
            _settings = settings.Value;
        }

        [HttpGet("test-connection")]
        public IActionResult TestConnection()
        {
            if (string.IsNullOrEmpty(_settings.ProductionKey))
            {
                return BadRequest(new { error = "Configuration missing: ProductionKey is empty." });
            }

            return Ok(new { 
                status = "Connected", 
                message = "Backend successfully read the API Key.",
                preview = _settings.ProductionKey.Substring(0, 4) + "****"
            });
        }
    }
}