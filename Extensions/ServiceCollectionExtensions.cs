using System.Net;
using CoberPush.Api.Endpoints;
using CoberPush.Api.Options;
using CoberPush.Api.Projects;
using CoberPush.Api.Security;
using CoberPush.Api.Services;
using CoberPush.Api.Validation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra opciones (validadas al arrancar), servicios, rate limiting y proxies de confianza.
    /// Todo lo que depende de la configuración se resuelve de forma diferida (IOptions) para respetar los overrides.
    /// </summary>
    public static IServiceCollection AddCoberPush(this IServiceCollection services, IConfiguration config)
    {
        AddValidatedOption<ApiOptions>(services, config, ApiOptions.SECTION);
        AddValidatedOption<PushOptions>(services, config, PushOptions.SECTION);
        AddProjectsOption(services, config);

        AddServices(services);
        AddReadForwarderClient(services);
        AddRateLimiting(services);
        AddInfrastructureOptions(services);

        return services;
    }

    private static void AddValidatedOption<T>(IServiceCollection services, IConfiguration config, string section)
        where T : class
    {
        services.AddOptions<T>().Bind(config.GetSection(section)).ValidateDataAnnotations().ValidateOnStart();
    }

    /// <summary>Enlaza el array <c>Projects</c> y lo valida por completo al arrancar (ver <see cref="ProjectsOptionsValidator"/>).</summary>
    private static void AddProjectsOption(IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<ProjectsOptions>()
            .Configure(options => config.GetSection(ProjectsOptions.SECTION).Bind(options.Items))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ProjectsOptions>, ProjectsOptionsValidator>();
        services.AddSingleton<IValidateOptions<ApiOptions>, ApiOptionsValidator>();
    }

    private static void AddServices(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IProjectRegistry, ProjectRegistry>();
        services.AddSingleton<ApiKeyAuthenticator>();

        services.AddSingleton<IUrlPolicy, UrlPolicy>();
        services.AddSingleton<IReceiptSigner, HmacReceiptSigner>();
        services.AddSingleton<PushMessageFactory>();
        services.AddSingleton<IFirebaseMessagingProvider, FirebaseMessagingProvider>();
        services.AddSingleton<IPushSender, FirebasePushSender>();
        services.AddSingleton<IProjectHealthChecker, ProjectHealthChecker>();
        services.AddSingleton<PushRequestValidator>();
        services.AddSingleton<RecipientResolver>();
        services.AddSingleton<ReadReceiptValidator>();
    }

    /// <summary>El timeout lo fija cada proyecto (<c>UrlApi.TimeoutSeconds</c>) dentro del forwarder.</summary>
    private static void AddReadForwarderClient(IServiceCollection services)
    {
        services.AddHttpClient<IReadForwarder, ReadForwarder>(client => client.Timeout = Timeout.InfiniteTimeSpan);
    }

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });

        services.AddOptions<RateLimiterOptions>().Configure<IOptions<ApiOptions>>((limiter, api) =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(RateLimitPolicies.SEND, ctx => RateLimitPartitioner.ByKey(ctx, RateLimitPolicies.SEND, api.Value));
            limiter.AddPolicy(RateLimitPolicies.HEALTH, ctx => RateLimitPartitioner.ByKey(ctx, RateLimitPolicies.HEALTH, api.Value));
            limiter.AddPolicy(RateLimitPolicies.RECEIPTS, ctx => RateLimitPartitioner.ByProjectIp(ctx, api.Value));
        });
    }

    /// <summary>Proxies de confianza (IP real) y límite global de cuerpo (<c>Push:MaxBodyBytes</c>).</summary>
    private static void AddInfrastructureOptions(IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ApiOptions>>((forwarded, api) =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwarded.ForwardLimit = 1;
            forwarded.KnownProxies.Clear();
            forwarded.KnownIPNetworks.Clear();

            foreach (var proxy in api.Value.KnownProxies)
            {
                forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });

        services.AddOptions<KestrelServerOptions>().Configure<IOptions<PushOptions>>((kestrel, push) =>
        {
            kestrel.Limits.MaxRequestBodySize = push.Value.MaxBodyBytes;
        });
    }
}
