using InvestmentManager.Health_Checks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class FilePathHealthCheckBuilderExtensions
    {
        public static IHealthChecksBuilder AddFilePathWrite(this IHealthChecksBuilder builder, string filePath,
            HealthStatus failureStatus, IEnumerable<string> tags = default)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }


            return builder.Add(new HealthCheckRegistration(
                "File Path Health Check",
                new FilePathWriteHealthCheck(filePath), 
                failureStatus,
                tags));
        }
    }
}
