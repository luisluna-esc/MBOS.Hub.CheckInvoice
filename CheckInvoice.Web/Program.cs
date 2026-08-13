using System.Net;
using System.Text;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var entries = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToList();

            var hasRootError = entries.Any(entry => string.IsNullOrWhiteSpace(entry.Key));
            var pathErrors = entries.Where(entry => entry.Key.StartsWith("$.")).ToList();

            Message[] messages;
            if (hasRootError)
            {
                messages = [new Message { Type = MessageType.Error, Description = "The request body is invalid or missing." }];
            }
            else if (pathErrors.Count > 0)
            {
                messages = pathErrors
                    .Select(entry => new Message
                    {
                        Type = MessageType.Error,
                        Description = $"The field '{entry.Key.TrimStart('$', '.')}' has an invalid or missing value."
                    })
                    .ToArray();
            }
            else
            {
                messages = entries
                    .Select(entry => new Message
                    {
                        Type = MessageType.Error,
                        Description = $"The field '{entry.Key}' has an invalid or missing value."
                    })
                    .ToArray();
            }

            var response = new ResponsePost
            {
                Id = 0,
                Messages = messages,
                StatusCode = HttpStatusCode.BadRequest
            };

            return new BadRequestObjectResult(response);
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the access token returned by /api/auth/login."
    });
});

builder.Services.AddInfrastructure(builder.Configuration);

var jwtSection = builder.Configuration.GetSection("Jwt");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtSection["Key"] ?? string.Empty)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

const string LocalDevCorsPolicy = "LocalDevCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(LocalDevCorsPolicy, policy =>
    {
        policy.SetIsOriginAllowed(origin => new Uri(origin).Host is "localhost" or "127.0.0.1")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.UseCors(LocalDevCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();