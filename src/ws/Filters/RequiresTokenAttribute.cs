using Microsoft.AspNetCore.Mvc.Filters;

namespace Unlimitedinf.Tom.WebSocket.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    internal sealed class RequiresTokenAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => true;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            Options options = serviceProvider.GetRequiredService<Options>();
            ILogger<RequiresTokenFilter> logger = serviceProvider.GetRequiredService<ILogger<RequiresTokenFilter>>();

            return new RequiresTokenFilter(options, logger);
        }
    }
}
