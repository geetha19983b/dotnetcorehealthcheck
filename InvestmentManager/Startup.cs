using System;
using System.Collections.Generic;
using System.Linq;
using InvestmentManager.Core;
using InvestmentManager.DataAccess.EF;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Web;

using System.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
//using HealthChecks.UI.Client;
using Microsoft.EntityFrameworkCore;

namespace InvestmentManager
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env, IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            Configuration = configuration;
            this.loggerFactory = loggerFactory;

            // For NLog                   
            NLog.LogManager.LoadConfiguration("nlog.config");
        }

        public IConfiguration Configuration { get; }

        private readonly ILoggerFactory loggerFactory;


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddMvc(options => options.EnableEndpointRouting = false).SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            services.AddSingleton<IConfiguration>(this.Configuration);

            // Configure the data access layer
             var connectionString = this.Configuration.GetConnectionString("InvestmentDatabase");

           services.RegisterEfDataAccessClasses(connectionString, loggerFactory);

            //services.AddDbContext<InvestmentContext>(options =>
            //   options.UseSqlServer(
            //       Configuration.GetConnectionString("InvestmentDatabase")));


            // For Application Services
            String stockIndexServiceUrl = this.Configuration["StockIndexServiceUrl"];
            services.ConfigureStockIndexServiceHttpClientWithoutProfiler(stockIndexServiceUrl);
            services.ConfigureInvestmentManagerServices(stockIndexServiceUrl);

            // Configure logging
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
                loggingBuilder.AddNLog();
            });

            //services.AddHealthChecks()
            //    .AddCheck("SQL Check", () =>
            //    {
            //        using (var connection = new SqlConnection(connectionString))
            //        {
            //            try
            //            {
            //                connection.Open();
            //                return HealthCheckResult.Healthy();
            //            }
            //            catch (SqlException)
            //            {
            //                return HealthCheckResult.Unhealthy();
            //            }
            //        }
            //    });

            //using 3rd party packages for health check
            //tag ready will run the health check only for health/ready end point
            var securityLogFilePath = this.Configuration["SecurityLogFilePath"];

            services.AddHealthChecks()
                .AddSqlServer(connectionString, failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready" })
                .AddUrlGroup(new Uri($"{stockIndexServiceUrl}/api/StockIndexes"),
                    "Stock Index Api Health Check", HealthStatus.Degraded, tags: new[] { "ready" }, timeout: new TimeSpan(0, 0, 5));
               // .AddFilePathWrite(securityLogFilePath, HealthStatus.Unhealthy, tags: new[] { "ready" });

            //for using health check ui
           // services.AddHealthChecksUI();
        }


        // Configures the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {



            app.UseExceptionHandler("/Home/Error");

            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            app.UseRouting();

            //    app.UseEndpoints(endpoints =>
            //    {
            //       endpoints.MapHealthChecks("/health").RequireHost("*:51500"); 

            //    });

            app.UseEndpoints(endpoints =>
            {

                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions()
                {
                    ResultStatusCodes = {
                        [HealthStatus.Healthy] = StatusCodes.Status200OK,
                        [HealthStatus.Degraded] = StatusCodes.Status500InternalServerError,
                        [HealthStatus.Unhealthy] =StatusCodes.Status503ServiceUnavailable
                    },
                    ResponseWriter = WriteHealthCheckReadyResponse,
                    Predicate = (check) => check.Tags.Contains("ready"), //to filter health check based on the tags defined
                    AllowCachingResponses = false
                });

                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions()
                {
                    Predicate = (check) => !check.Tags.Contains("ready"),
                    ResponseWriter = WriteHealthCheckLiveResponse,
                    AllowCachingResponses = false
                });
                //endpoints.MapHealthChecks("/healthui", new HealthCheckOptions()
                //{
                //    Predicate=_=> true,
                //    ResponseWriter=UIResponseWriter.WriteHealthCheckUIResponse
                //});
            });

          //  app.UseHealthChecksUI();

        }

        private Task WriteHealthCheckLiveResponse(HttpContext httpContext, HealthReport result)
        {
            httpContext.Response.ContentType = "application/json";

            var json = new JObject(
                    new JProperty("OverallStatus", result.Status.ToString()),
                    new JProperty("TotalChecksDuration", result.TotalDuration.TotalSeconds.ToString("0:0.00"))
                );

            return httpContext.Response.WriteAsync(json.ToString(Formatting.Indented));
        }

        private Task WriteHealthCheckReadyResponse(HttpContext httpContext, HealthReport result)
        {
            httpContext.Response.ContentType = "application/json";

            var json = new JObject(
                    new JProperty("OverallStatus", result.Status.ToString()),
                    new JProperty("TotalChecksDuration", result.TotalDuration.TotalSeconds.ToString("0:0.00")),
                    new JProperty("DependencyHealthChecks", new JObject(result.Entries.Select(dicItem =>
                        new JProperty(dicItem.Key, new JObject(
                                new JProperty("Status", dicItem.Value.Status.ToString()),
                                new JProperty("Duration", dicItem.Value.Duration.TotalSeconds.ToString("0:0.00")),
                                new JProperty("Exception", dicItem.Value.Exception?.Message),
                                new JProperty("Data", new JObject(dicItem.Value.Data.Select(dicData =>
                                    new JProperty(dicData.Key, dicData.Value))))
                            ))
                    )))
                );

            return httpContext.Response.WriteAsync(json.ToString(Formatting.Indented));
        }

    }
}

