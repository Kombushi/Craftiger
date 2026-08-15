using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Settings live beside the binary, so a run works from any working directory.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.AddBuilderServices(builder.Configuration);

using var host = builder.Build();
return host.Services.GetRequiredService<IBuilderPipeline>().Run();