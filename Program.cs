using System.Security.Claims;
using KSol.NextCloudMailToFolder.Data;
using KSol.NextCloudMailToFolder.Mail;
using KSol.NextCloudMailToFolder.Models;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KSol.NextCloudMailToFolder;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var config = builder.Configuration;
        WebApplication? app = null;

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(config.GetConnectionString("DefaultConnection")));

        // Add services to the container.
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = "Cookies";
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie("Cookies")
        .AddOAuth(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            IConfigurationSection nextcloudAuthSection =
            config.GetSection("Nextcloud");
            if (nextcloudAuthSection == null)
            {
                throw new Exception("Nextcloud configuration section not found.");
            }
            options.SignInScheme = "Cookies";
            options.ClientId = nextcloudAuthSection["ClientId"]!;
            options.ClientSecret = nextcloudAuthSection["ClientSecret"]!;
            options.CallbackPath = new PathString("/auth/nextcloud");
            options.TokenEndpoint = nextcloudAuthSection["TokenEndpoint"]!;
            options.AuthorizationEndpoint = nextcloudAuthSection["AuthorizationEndpoint"]!;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.Events = new OAuthEvents
            {
                //Adds Cognito id_token to Claims.
                OnCreatingTicket = async (context) =>
                {
                    var expiration = DateTime.UtcNow.AddHours(0.9);
                    context.Identity.AddClaim(new Claim("nextcloud_token", context.AccessToken));
                    context.Identity.AddClaim(new Claim("nextcloud_refresh_token", context.RefreshToken));
                    context.Identity.AddClaim(new Claim("nextcloud_token_expires", expiration.Ticks.ToString()));
                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
                    var userInfoResponse = await client.GetAsync("https://nc.ksol.it/ocs/v1.php/cloud/user?format=json");
                    var json = await userInfoResponse.Content.ReadAsStringAsync();
                    var userInfo = System.Text.Json.JsonSerializer.Deserialize<NextCloudUserInformationResponse>(json);
                    context.Identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userInfo.Ocs.Data.Id));
                    context.Identity.AddClaim(new Claim(ClaimTypes.Email, userInfo.Ocs.Data.Email));
                    context.Identity.AddClaim(new Claim(ClaimTypes.Name, userInfo.Ocs.Data.DisplayName));
                    context.Identity.AddClaim(new Claim(ClaimTypes.GivenName, userInfo.Ocs.Data.DisplayName));
                    using (var scope = app.Services.CreateScope())
                    using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
                    {
                        var user = await dbContext.NextCloudUsers.FirstOrDefaultAsync(u => u.Id == userInfo.Ocs.Data.Id);
                        if (user == null)
                        {
                            user = new NextCloudUser()
                            {
                                Id = userInfo.Ocs.Data.Id,
                                DisplayName = userInfo.Ocs.Data.DisplayName,
                                Email = userInfo.Ocs.Data.Email,
                                Token = context.AccessToken,
                                RefreshToken = context.RefreshToken,
                                TokenExpiration = expiration
                            };
                            dbContext.NextCloudUsers.Add(user);
                            await dbContext.SaveChangesAsync();
                        }
                        else 
                        {
                            user.Token = context.AccessToken;
                            user.RefreshToken = context.RefreshToken;
                            user.TokenExpiration = expiration;
                            dbContext.NextCloudUsers.Update(user);
                            await dbContext.SaveChangesAsync();
                        }
                    }
                }
            };
            options.Validate();
        });

        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        builder.Services.AddHostedService<SmtpServerService>();

        app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();

        app.Run();
    }
}
