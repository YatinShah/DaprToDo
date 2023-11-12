using Dapr.Client;

using Man.Dapr.Sidekick;
using Man.Dapr.Sidekick.Http;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;

namespace ToDoApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DaprGutController : ControllerBase
    {

        public const string DefaultStoreName = "statestore";
        private readonly IDaprSidecarHost _daprSidecarHost;
        private readonly IDaprSidecarHttpClientFactory _httpClientFactory;
        private readonly ILogger<DaprGutController> _logger;
        private readonly IConfiguration _config;

        public DaprGutController(ILogger<DaprGutController> logger, IConfiguration config,  IDaprSidecarHost daprSidecarHost, IDaprSidecarHttpClientFactory httpClientFactory)
        {
            _daprSidecarHost = daprSidecarHost;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _config = config;
        }
        //copied from https://github.com/man-group/dapr-sidekick-dotnet/blob/main/samples/AspNetCore/ServiceInvocationSample/ServiceInvocationSample/Controllers/WeatherForecastController.cs
        [HttpGet("status")]
        public ActionResult GetStatus() => Ok(new
        {
            process = _daprSidecarHost.GetProcessInfo(),   // Information about the sidecar process such as if it is running
            options = _daprSidecarHost.GetProcessOptions() // The sidecar options if running, including ports and locations
        });

        [HttpGet("WeatherViaSidecar")]
        public async Task<IEnumerable<WeatherForecast>> GetWeatherViaSidecar()
        {
            // Get a Dapr Service Invocation http client for this service's AppId.
            // This will perform a local round-trip call to the sidecar and back
            // to this controller to demonstrate service invocation in action.
            var appId = _daprSidecarHost.GetProcessOptions()?.AppId;
            if (appId == null)
            {
                _logger.LogError($"appId was not found !!");
                // AppId not available, sidecar probably not running
                return null;
            }

            // Create an HttpClient for this service appId
            var httpClient = _httpClientFactory.CreateInvokeHttpClient(appId);

            // Invoke the relative endpoint on target service
            // In this case it will invoke the default Get method on this controller
            var result = await httpClient.GetStringAsync("/api/WeatherForecast");

            // Deserialize and return the result
            return JsonConvert.DeserializeObject<IEnumerable<WeatherForecast>>(result);
        }

        [HttpGet(Name = "GetSecret")]
        public async Task<IActionResult> Get([FromServices] DaprClient daprClient, [FromServices] IConfiguration configuration)
        {
            // Can read secrets by using the client or IConfiguration through DI as well
            var clientSecrets = await daprClient.GetSecretAsync(Program.secretStoreName, "secret");
            var clientSecret = string.Join(",", clientSecrets.Select(d => d.Value));

            var configurationSecret = configuration.GetSection("secret").Value;

            return Ok(new
            {
                SecretFromClient = clientSecret,
                SecretFromConfiguration = configurationSecret
            });
        }
    }
}
