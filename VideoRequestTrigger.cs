using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;
using LSC.OnlineCourse.Functions.Email;
using Microsoft.Extensions.Configuration;
using LSC.OnlineCourse.Functions.Entities;
using LSC.OnlineCourse.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace online_course_functions
{
    public class VideoRequestTrigger
    {
        private readonly ILogger _logger;
        private readonly IEmailNotification _emailNotification;
        private readonly IConfiguration configuration;

        public VideoRequestTrigger(ILoggerFactory loggerFactory, IEmailNotification emailNotification,
            IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<VideoRequestTrigger>();
            _emailNotification = emailNotification;
            this.configuration = configuration;
        }

        // Visit https://aka.ms/sqltrigger to learn how to use this trigger binding
        [Function("VideoRequestTrigger")]
        public async Task RunAsync(
            [SqlTrigger("[dbo].[VideoRequest]", "DbContext")] IReadOnlyList<SqlChange<VideoRequest>> videoRequests,
            FunctionContext context)
        {
            var logger = context.GetLogger("VideoRequestTrigger");
            _logger.LogInformation("C# HTTP trigger with SQL Output Binding function processed a request.");

            var onlineCourseDbContext = GetDbContext();

            foreach (SqlChange<VideoRequest> change in videoRequests)
            {
                VideoRequest videoRequest = change.Item;

                var userInfo = await onlineCourseDbContext.UserProfiles.FirstOrDefaultAsync(f => f.UserId == videoRequest.UserId);
                var userFullName = $"{userInfo.LastName},{userInfo.FirstName}";

                await _emailNotification.SendVideoRequestConfirmation(videoRequest, userFullName, userInfo.Email);
                logger.LogInformation($"Change operation: {change.Operation}");
            }
        }

        [Function("SendVideoRequestAckEmailToUser")]
        public async Task<IActionResult> SendVideoRequestAckEmailToUser([HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "SendVideoRequestAckEmailToUser")]
        HttpRequest req, ExecutionContext context)
        {
            _logger.LogInformation("C# HTTP trigger function processed SendVideoRequestAckEmailToUser request.");
            VideoRequestModel model = new VideoRequestModel();

            try
            {
                var onlineCourseDbContext = GetDbContext();
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                if (string.IsNullOrEmpty(requestBody))
                {
                    return new BadRequestObjectResult("Invalid request body. Please provide a valid Profile. Body cannot be empty");
                }

                //we will parse our request body to this model
                model = JsonSerializer.Deserialize<VideoRequestModel>(requestBody);

                if (model == null || model.VideoRequestId < 1)
                {
                    return new BadRequestObjectResult("Invalid request body. Please provide a valid videoRequest.");
                }

                var videoRequest = await onlineCourseDbContext.VideoRequests.Include(i => i.User).FirstOrDefaultAsync(f => f.VideoRequestId == model.VideoRequestId);
                var userFullName = $"{videoRequest.User.LastName},{videoRequest.User.FirstName}";

                if (videoRequest != null)
                {
                    await _emailNotification.SendVideoRequestConfirmation(videoRequest, userFullName, videoRequest.User.Email);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }


            return new OkObjectResult(model);
        }

        private OnlineCourseDbContext GetDbContext()
        {
            //Read connection string value from our local settings from project.
            string connectionString = configuration.GetConnectionString("DbContext");

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("The connection string is null or empty.");
                throw new InvalidOperationException("The connection string has not been initialized.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<OnlineCourseDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            var onlineCourseDbContext = new OnlineCourseDbContext(optionsBuilder.Options);
            return onlineCourseDbContext;
        }
    }
}

