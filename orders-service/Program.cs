using Confluent.Kafka;
using Dapper;
using Microsoft.Extensions.Primitives;
using Npgsql;
using OrdersService.Data;
using OrdersService.Kafka;
using OrdersService.Models;
using OrdersService.Services;

DefaultTypeMap.MatchNamesWithUnderscores = true;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

KafkaOptions kafkaOpt = new()
{
    Brokers = builder.Configuration["KAFKA_BROKERS"] ?? "kafka:29092",
    Topic = builder.Configuration["KAFKA_TOPIC"] ?? "orders",
    ConsumerGroup = builder.Configuration["KAFKA_CONSUMER_GROUP"] ?? "orders-service-group"
};
builder.Services.AddSingleton(kafkaOpt);

string cs = Db.GetConnectionString(builder.Configuration);
builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(cs).Build());

builder.Services.AddSingleton<OrderRepository>();
builder.Services.AddSingleton<OutboxRepository>();
builder.Services.AddSingleton<OrderWriteRepository>();
builder.Services.AddSingleton<OrdersAppService>();

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    ProducerConfig config = new()
    {
        BootstrapServers = kafkaOpt.Brokers, Acks = Acks.All, EnableIdempotence = true, MessageSendMaxRetries = 5
    };
    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IConsumer<string, string>>(_ =>
{
    ConsumerConfig config = new()
    {
        BootstrapServers = kafkaOpt.Brokers,
        GroupId = kafkaOpt.ConsumerGroup,
        EnableAutoCommit = false,
        AutoOffsetReset = AutoOffsetReset.Earliest
    };
    return new ConsumerBuilder<string, string>(config).Build();
});

builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<PaymentResultsConsumer>();

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/orders", async (HttpContext ctx, OrdersAppService svc, CreateOrderRequest body, CancellationToken ct) =>
    {
        Guid? userId = ResolveUserId(ctx, body.UserId);
        if (userId is null)
        {
            return Results.BadRequest("user_id is required (X-User-Id header or request body)");
        }

        if (body.Amount <= 0)
        {
            return Results.BadRequest("amount must be > 0");
        }

        try
        {
            OrderDto order = await svc.CreateOrderAsync(userId.Value, body.Amount, body.Description, ct);
            return Results.Created($"/orders/{order.Id}", order);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.BadRequest("amount must be > 0");
        }
    })
    .WithName("CreateOrder")
    .WithOpenApi();

app.MapPost("/orders/create",
        async (HttpContext ctx, OrdersAppService svc, CreateOrderRequest body, CancellationToken ct) =>
        {
            Guid? userId = ResolveUserId(ctx, body.UserId);
            if (userId is null)
            {
                return Results.BadRequest("user_id is required (X-User-Id header or request body)");
            }

            if (body.Amount <= 0)
            {
                return Results.BadRequest("amount must be > 0");
            }

            OrderDto order = await svc.CreateOrderAsync(userId.Value, body.Amount, body.Description, ct);
            return Results.Created($"/orders/{order.Id}", order);
        })
    .WithName("CreateOrderLegacy")
    .WithOpenApi();

app.MapGet("/orders", async (HttpContext ctx, OrdersAppService svc, CancellationToken ct) =>
    {
        Guid? userId = ResolveUserId(ctx, null);
        if (userId is null)
        {
            return Results.BadRequest("X-User-Id header is required");
        }

        IReadOnlyList<OrderDto> list = await svc.ListAsync(userId.Value, ct);
        return Results.Ok(list);
    })
    .WithName("ListOrders")
    .WithOpenApi();

app.MapGet("/orders/{orderId:guid}/status",
        async (HttpContext ctx, OrdersAppService svc, Guid orderId, CancellationToken ct) =>
        {
            Guid? userId = ResolveUserId(ctx, null);
            if (userId is null)
            {
                return Results.BadRequest("X-User-Id header is required");
            }

            OrderDto? order = await svc.GetAsync(userId.Value, orderId, ct);
            return order is null
                ? Results.NotFound("Order not found")
                : Results.Ok(new OrderStatusResponse(order.Id, order.Status));
        })
    .WithName("OrderStatus")
    .WithOpenApi();

app.Run();
return;

static Guid? ResolveUserId(HttpContext ctx, Guid? userIdFromBody)
{
    return ctx.Request.Headers.TryGetValue("X-User-Id", out StringValues headerVals) &&
           Guid.TryParse(headerVals.FirstOrDefault(), out Guid id)
        ? id
        : userIdFromBody;
}