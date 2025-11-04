using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using oc.Common;
using EC.Application.Helper;

namespace MinioProvider.Usage
{
    public static class DependencyInjection
    {
        public static IServiceCollection InjectMinioServices(this IServiceCollection services)
        {
            services.AddTransient<IMinioHelper, MinioHelper>();
            return services;
        }
    }
}
