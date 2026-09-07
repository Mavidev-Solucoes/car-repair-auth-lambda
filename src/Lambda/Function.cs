using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using CarRepair.Auth.Application.Contracts;
using CarRepair.Auth.Application.Exceptions;
using CarRepair.Auth.Application.Interfaces;
using CarRepair.Auth.Application.Services;
using CarRepair.Auth.Application.Validation;
using CarRepair.Auth.Infrastructure;
using CarRepair.Auth.Infrastructure.Configuration;
using CarRepair.Auth.Lambda.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CarRepair.Auth.Lambda;

public sealed class Function
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceProvider _serviceProvider;

    public Function()
        : this(BuildServiceProvider())
    {
    }

    internal Function(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var correlationId = ResolveCorrelationId(request);

        using var scope = _serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Function>>();
        using var loggingScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["AwsRequestId"] = context.AwsRequestId
        });

        try
        {
            logger.LogInformation("Processing customer authentication request");

            var payload = DeserializeRequest(request.Body);
            var service = scope.ServiceProvider.GetRequiredService<IAuthenticateCustomerService>();
            var response = await service.ExecuteAsync(new AuthenticateCustomerCommand(payload.Cpf), context.CancellationToken);

            logger.LogInformation("Authentication token generated for customer {CustomerId}", response.CustomerId);
            return CreateResponse(HttpStatusCode.OK, response, correlationId);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Invalid request payload");
            return CreateResponse(HttpStatusCode.BadRequest, new
            {
                message = "Request body must be a valid JSON payload.",
                correlationId
            }, correlationId);
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(exception, "Validation failed while authenticating customer");
            return CreateResponse(HttpStatusCode.BadRequest, new
            {
                message = "Validation failed.",
                correlationId,
                errors = exception.Errors.Select(error => new { error.PropertyName, error.ErrorMessage })
            }, correlationId);
        }
        catch (CustomerNotFoundException exception)
        {
            logger.LogWarning(exception, "Customer not found");
            return CreateResponse(HttpStatusCode.NotFound, new
            {
                message = exception.Message,
                correlationId
            }, correlationId);
        }
        catch (CustomerInactiveException exception)
        {
            logger.LogWarning(exception, "Customer inactive");
            return CreateResponse(HttpStatusCode.Forbidden, new
            {
                message = exception.Message,
                correlationId
            }, correlationId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled error while authenticating customer");
            return CreateResponse(HttpStatusCode.InternalServerError, new
            {
                message = "An unexpected error occurred.",
                correlationId
            }, correlationId);
        }
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "O";
            });
        });

        services.AddScoped<IAuthenticateCustomerService, AuthenticateCustomerService>();
        services.AddScoped<IValidator<AuthenticateCustomerCommand>, AuthenticateCustomerCommandValidator>();
        services.AddInfrastructure(configuration);

        var secretKey = configuration["Jwt:SecretKey"];
        if (!string.IsNullOrWhiteSpace(secretKey))
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                        ValidateIssuer = true,
                        ValidIssuer = configuration["Jwt:Issuer"] ?? "car-repair-auth",
                        ValidateAudience = true,
                        ValidAudience = configuration["Jwt:Audience"] ?? "car-repair-shop",
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });
        }

        return services.BuildServiceProvider();
    }

    private static AuthenticateRequest DeserializeRequest(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new JsonException("Request body is empty.");
        }

        var request = JsonSerializer.Deserialize<AuthenticateRequest>(body, JsonSerializerOptions);
        if (request is null)
        {
            throw new JsonException("Request body is invalid.");
        }

        return request;
    }

    private static string ResolveCorrelationId(APIGatewayProxyRequest request)
    {
        if (request.Headers is not null)
        {
            foreach (var header in request.Headers)
            {
                if (string.Equals(header.Key, "x-correlation-id", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(header.Key, "correlation-id", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(header.Value))
                    {
                        return header.Value;
                    }
                }
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static APIGatewayProxyResponse CreateResponse(HttpStatusCode statusCode, object payload, string correlationId)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = (int)statusCode,
            Body = JsonSerializer.Serialize(payload, JsonSerializerOptions),
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["X-Correlation-Id"] = correlationId
            }
        };
    }
}
