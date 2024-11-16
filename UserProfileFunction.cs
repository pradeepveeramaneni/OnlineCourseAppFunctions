using LSC.OnlineCourse.Functions.Entities;
using LSC.OnlineCourse.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;


namespace online_course_functions
{
    public class UserProfileFunction
    {
        private readonly ILogger<UserProfileFunction> _logger;
        private readonly IConfiguration configuration;

        public UserProfileFunction(ILogger<UserProfileFunction> logger, IConfiguration configuration)
        {
            _logger = logger;
            this.configuration = configuration;
        }

        [Function("UserProfileFunction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "UpdateUserProfile")]
        HttpRequest req, ExecutionContext context)
        {
            _logger.LogInformation("C# HTTP trigger function processed UpdateUserProfile request.");

            var userProfileResponse = new Profile();

            try
            {
                //Read connection string value from our local settings from project.
                string connectionString = configuration.GetConnectionString("DbContext");

                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("The connection string is null or empty.");
                    throw new InvalidOperationException("The connection string has not been initialized.");
                }

                //_logger.LogInformation($"DbContext: {connectionString}");

                var optionsBuilder = new DbContextOptionsBuilder<OnlineCourseDbContext>();
                optionsBuilder.UseSqlServer(connectionString);

                var learnSmartDbContext = new OnlineCourseDbContext(optionsBuilder.Options);

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                if (string.IsNullOrEmpty(requestBody))
                {
                    return new BadRequestObjectResult("Invalid request body. Please provide a valid Profile. Body cannot be empty");
                }

                //we will parse our request body to this model
                Profile? profile = JsonSerializer.Deserialize<Profile>(requestBody);

                if (profile == null)
                {
                    return new BadRequestObjectResult("Invalid request body. Please provide a valid Profile.");
                }

                string adObjId = profile.AdObjId;

                if (string.IsNullOrEmpty(adObjId))
                {
                    return new BadRequestObjectResult("Please provide AdObjId in the request body.");
                }

                // Check if UserProfile with given AdObjId exists
                var userProfile = await learnSmartDbContext.UserProfiles.Include(d => d.UserRoles).FirstOrDefaultAsync(u => u.AdObjId == adObjId);
                var role = await learnSmartDbContext.Roles.FirstOrDefaultAsync(f => f.RoleName == "Student");

                if (userProfile == null)
                {
                    // If not exists, create a new UserProfile
                    userProfile = new UserProfile
                    {
                        AdObjId = adObjId,
                        DisplayName = profile.DisplayName,
                        FirstName = profile.FirstName,
                        LastName = profile.LastName,
                        Email = profile.Email,
                        UserRoles = new List<UserRole>() {
                            new UserRole() { SmartAppId = 1, RoleId = role.RoleId}
                        }
                    };

                    learnSmartDbContext.UserProfiles.Add(userProfile);
                }
                else
                {
                    // If exists, update the existing UserProfile
                    // You can update other properties here if needed
                    userProfile.DisplayName = profile.DisplayName;
                    userProfile.FirstName = profile.FirstName;
                    userProfile.LastName = profile.LastName;
                    userProfile.Email = profile.Email;
                }

                await learnSmartDbContext.SaveChangesAsync();

                //get user's roles here
                var userRoles = await learnSmartDbContext.UserRoles.Include(i => i.Role)
                    .Where(u => u.UserId == userProfile.UserId).Select(s => s.Role.RoleName).ToListAsync();

                userProfileResponse = new Profile()
                {
                    UserId = userProfile.UserId,
                    AdObjId = userProfile.AdObjId,
                    DisplayName = userProfile.DisplayName,
                    Email = userProfile.Email,
                    FirstName = userProfile.FirstName,
                    LastName = userProfile.LastName,
                    Roles = userRoles == null ? new List<string>() : userRoles
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }


            return new OkObjectResult(userProfileResponse);
        }

    }
}
