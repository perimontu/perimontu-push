using System.Net;
using System.Threading.RateLimiting;
using CoberPush.Api.Endpoints;
using CoberPush.Api.Options;
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
        AddValidatedOption<ReceiptOptions>(services, config, ReceiptOptions.SECTION);
        AddValidatedOption<PhpApiOptions>(services, config, PhpApiOptions.SECTION);
        AddValidatedOption<FirebaseOptions>(services, config, FirebaseOptions.SECTION);
        services.AddOptions<RateLimitOptions>().Bind(config.GetSection(RateLimitOptions.SECTION)).ValidateDataAnnotations();
        services.AddSingleton<IValidateOptions<ApiOptions>, ApiOptionsValidator>();

        AddServices(services);
        AddPhpClient(services);
        AddRateLimiting(services);
        AddInfrastructureOptions(services);

        return services;
    }

    private static void AddValidatedOption<T>(IServiceCollection services, IConfiguration config, string section)
        where T : class
    {
        services.AddOptions<T>().Bind(config.GetSection(section)).ValidateDataAnnotations().ValidateOnStart();
    }

    private static void AddServices(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp =>
        {
            IpAllowList.TryParse(sp.GetRequiredService<IOptions<ApiOptions>>().Value.AllowedSendIps, out var list, out _);
            return list ?? IpAllowList.Empty;
        });

        services.AddSingleton<IUrlPolicy, UrlPolicy>();
        services.AddSingleton<IReceiptSigner, HmacReceiptSigner>();
        services.AddSingleton<PushMessageFactory>();
        services.AddSingleton<IPushSender, FirebasePushSender>();
        services.AddSingleton<PushRequestValidator>();
        services.AddSingleton<ReadReceiptValidator>();
    }

    private static void AddPhpClient(IServiceCollection services)
    {
        services.AddHttpClient<IPhpReadForwarder, PhpReadForwarder>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PhpApiOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.BearerToken);
        });
    }

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });

        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitOptions>>((limiter, rate) =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(RateLimitPolicies.SEND, ctx => ByIp(ctx, rate.Value.Send));
            limiter.AddPolicy(RateLimitPolicies.RECEIPTS, ctx => ByIp(ctx, rate.Value.Receipts));
        });
    }

    private static RateLimitPartition<string> ByIp(HttpContext context, RateLimitPolicyOptions policy)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = policy.PermitLimit,
            Window = TimeSpan.FromSeconds(policy.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
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
