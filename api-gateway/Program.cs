using Microsoft.Extensions.Primitives;
using System.Net;
using System.Net.Http.Headers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string paymentsBase = builder.Configuration["PAYMENTS_SERVICE_URL"] ?? "http://payments-service:8080";
string ordersBase = builder.Configuration["ORDERS_SERVICE_URL"] ?? "http://orders-service:8080";

builder.Services.AddHttpClient("payments", c =>
{
    c.BaseAddress = new Uri(paymentsBase);
});

builder.Services.AddHttpClient("orders", c =>
{
    c.BaseAddress = new Uri(ordersBase);
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapMethods("/accounts/create", ["POST"], (HttpContext ctx, IHttpClientFactory f, CancellationToken ct) =>
    Proxy(ctx, f.CreateClient("payments"), "/accounts/create", ct));

app.MapMethods("/accounts/top_up", ["POST"], (HttpContext ctx, IHttpClientFactory f, CancellationToken ct) =>
    Proxy(ctx, f.CreateClient("payments"), "/accounts/top_up", ct));

app.MapMethods("/accounts/{accountId:guid}/balance", ["GET"],
    (HttpContext ctx, IHttpClientFactory f, Guid accountId, CancellationToken ct) =>
        Proxy(ctx, f.CreateClient("payments"), $"/accounts/{accountId}/balance", ct));

app.MapMethods("/accounts/balance", ["GET"], (HttpContext ctx, IHttpClientFactory f, CancellationToken ct) =>
    Proxy(ctx, f.CreateClient("payments"), "/accounts/balance", ct));

app.MapMethods("/orders/create", ["POST"], (HttpContext ctx, IHttpClientFactory f, CancellationToken ct) =>
    Proxy(ctx, f.CreateClient("orders"), "/orders/create", ct));

app.MapMethods("/orders", ["GET"], (HttpContext ctx, IHttpClientFactory f, CancellationToken ct) =>
    Proxy(ctx, f.CreateClient("orders"), "/orders", ct));

app.MapMethods("/orders/{orderId:guid}/status", ["GET"],
    (HttpContext ctx, IHttpClientFactory f, Guid orderId, CancellationToken ct) =>
        Proxy(ctx, f.CreateClient("orders"), $"/orders/{orderId}/status", ct));

app.Run();
return;

static async Task<IResult> Proxy(HttpContext ctx, HttpClient client, string downstreamPath, CancellationToken ct)
{
    HttpRequest req = ctx.Request;

    using HttpRequestMessage msg = new();
    msg.Method = new HttpMethod(req.Method);
    msg.RequestUri = new Uri(client.BaseAddress!, downstreamPath);

    foreach (KeyValuePair<string, StringValues> header in req.Headers)
    {
        if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (msg.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
        {
            continue;
        }

        msg.Content ??= new StreamContent(req.Body);
        msg.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    if (!HttpMethods.IsGet(req.Method) && !HttpMethods.IsHead(req.Method))
    {
        msg.Content ??= new StreamContent(req.Body);
        if (req.ContentType is not null)
        {
            msg.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(req.ContentType);
        }
    }

    HttpResponseMessage resp;
    try
    {
        resp = await client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
    }
    catch (HttpRequestException e)
    {
        return Results.Problem(title: "Downstream service unavailable", detail: e.Message,
            statusCode: (int)HttpStatusCode.BadGateway);
    }

    ctx.Response.StatusCode = (int)resp.StatusCode;
    foreach (KeyValuePair<string, IEnumerable<string>> header in resp.Headers)
    {
        ctx.Response.Headers[header.Key] = header.Value.ToArray();
    }

    foreach (KeyValuePair<string, IEnumerable<string>> header in resp.Content.Headers)
    {
        ctx.Response.Headers[header.Key] = header.Value.ToArray();
    }

    ctx.Response.Headers.Remove("transfer-encoding");

    await using Stream respStream = await resp.Content.ReadAsStreamAsync(ct);
    await respStream.CopyToAsync(ctx.Response.Body, ct);

    return Results.Empty;
}