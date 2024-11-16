using LSC.OnlineCourse.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

namespace online_course_functions
{
    public class SignUpValidationFunction
    {
        private readonly IConfiguration configuration;

        public HttpClient HttpClient { get; }
        public ILogger<SignUpValidationFunction> Logger { get; }
        public SignUpValidationFunction(HttpClient httpClient, IConfiguration configuration, ILogger<SignUpValidationFunction> logger)
        {
            HttpClient = httpClient;
            Logger = logger;
            this.configuration = configuration;
        }

        [Function("SignUpValidationFunction")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            // Allowed domains
            //string[] allowedDomain = { "gmail.com", "facebook.com" };

            //Check HTTP basic authorization
            if (!Authorize(req))
            {
                Logger.LogWarning("HTTP basic authentication validation failed.");
                return (ActionResult)new UnauthorizedResult();
            }

            // Get the request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            // If input data is null, show block page
            if (data == null)
            {
                return (ActionResult)new OkObjectResult(new ResponseContent("ShowBlockPage", "There was a problem with your request."));
            }

            // Print out the request body
            Logger.LogInformation("Request body: " + requestBody);

            // Get the current user language 
            string language = (data.ui_locales == null || data.ui_locales.ToString() == "") ? "default" : data.ui_locales.ToString();
            Logger.LogInformation($"Current language: {language}");

            // If email claim not found, show block page. Email is required and sent by default.
            if (data.email == null || data.email.ToString() == "" || data.email.ToString().Contains("@") == false)
            {
                return (ActionResult)new OkObjectResult(new ResponseContent("ShowBlockPage", "Email name is mandatory."));
            }

            // Get domain of email address
            string domain = data.email.ToString().Split("@")[1];

            // Check the domain in the allowed list
            //if (!allowedDomain.Contains(domain.ToLower()))
            //{
            //    return (ActionResult)new OkObjectResult(new ResponseContent("ShowBlockPage", $"You must have an account from '{string.Join(", ", allowedDomain)}' to register as an external user for Contoso."));
            //}

            // If displayName claim doesn't exist, or it is too short, show validation error message. So, user can fix the input data.
            if (data.displayName == null || data.displayName.ToString().Length < 5)
            {
                return (ActionResult)new BadRequestObjectResult(new ResponseContent("ValidationError", "Please provide a Display Name with at least five characters."));
            }

            var profile = new Profile()
            {
                AdObjId = data.objectId,
                DisplayName = data.displayName.ToString(),
                Email = data.email.ToString(),
                FirstName = data.givenName,
                LastName = data.surname
                //FirstName = string.IsNullOrEmpty(data.firstName) ? (data.givenName ?? "") : data.firstName,
                //LastName = string.IsNullOrEmpty(data.lastName) ? (data.surname ?? "") : data.lastName
            };

            Logger.LogInformation(JsonConvert.SerializeObject(profile));

            // Call the UpdateUserProfile function
            var userProfileResponse = await CallUpdateUserProfileFunction(profile);


            var responseToReturn = new ResponseContent()
            {
                //jobTitle = "This value return by the API Connector",// this jobTitle is in built attribute, you ca change that value as well
                // You can also return custom claims using extension properties.
                //extension_EmployeeName = data.displayName,
                //extension_EmployeeRole = role,
                //extension_userRoles = userRoles,
                extension_userRoles = string.Join(',', userProfileResponse.Roles),
                extension_userId = userProfileResponse.UserId.ToString(),
            };

            Logger.LogInformation(JsonConvert.SerializeObject(responseToReturn));


            // Input validation passed successfully, return `Allow` response.
            // TO DO: Configure the claims you want to return
            return (ActionResult)new OkObjectResult(responseToReturn);
        }

        private bool Authorize(HttpRequest req)
        {
            try
            {
                // Get the environment's credentials 
                Logger.LogInformation("BASIC_AUTH_USERNAME : " + configuration.GetValue<string>("BASIC_AUTH_USERNAME"));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

            string username = configuration.GetValue<string>("BASIC_AUTH_USERNAME");
            string password = configuration.GetValue<string>("BASIC_AUTH_PASSWORD");

            Logger.LogInformation($"{username} - {password}");
            // Returns authorized if the username is empty or not exists.
            if (string.IsNullOrEmpty(username))
            {
                Logger.LogInformation("HTTP basic authentication is not set.");
                return true;
            }

            // Check if the HTTP Authorization header exist
            if (!req.Headers.ContainsKey("Authorization"))
            {
                Logger.LogWarning("Missing HTTP basic authentication header.");
                return false;
            }

            // Read the authorization header
            var auth = req.Headers["Authorization"].ToString();

            // Ensure the type of the authorization header id `Basic`
            if (!auth.StartsWith("Basic "))
            {
                Logger.LogWarning("HTTP basic authentication header must start with 'Basic '.");
                return false;
            }

            // Get the the HTTP basinc authorization credentials
            var cred = Encoding.UTF8.GetString(Convert.FromBase64String(auth.Substring(6))).Split(':');

            // Evaluate the credentials and return the result
            return (cred[0] == username && cred[1] == password);
        }

        private async Task<Profile> CallUpdateUserProfileFunction(Profile profile)
        {
            try
            {
                // Adjust the URL based on your Azure Functions app and function name
                var updateProfileURL = configuration.GetValue<string>("UpdateProfileURL");

                // Serialize the profile to JSON
                var jsonProfile = JsonConvert.SerializeObject(profile);

                // Create the HTTP content
                var content = new StringContent(jsonProfile, Encoding.UTF8, "application/json");

                // Call the UpdateUserProfile function
                var response = await HttpClient.PostAsync(updateProfileURL, content);

                if (response.IsSuccessStatusCode)
                {
                    // Parse and return the response
                    var responseBody = await response.Content.ReadAsStringAsync();
                    Logger.LogInformation("Processed userprofile: " + responseBody);
                    return JsonConvert.DeserializeObject<Profile>(responseBody);
                }

                // Handle error cases
                Logger.LogError($"UpdateUserProfile function failed with status code {response.StatusCode}");
                return new Profile();


            }
            catch (Exception ex)
            {
                Logger.LogError($"UpdateUserProfile function failed with exception {ex.ToString()}");
                return new Profile();
            }
        }
    }
}
