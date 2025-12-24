using Confluent.Kafka;
using Dapper;
using Microsoft.Extensions.Primitives;
using Npgsql;
using PaymentsService.Data;
using PaymentsService.Kafka;
using PaymentsService.Models;
using PaymentsService.Services;

DefaultTypeMap.MatchNamesWithUnderscores = true;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

KafkaOptions kafkaOpt = new()
{
    Brokers = builder.Configuration["KAFKA_BROKERS"] ?? "kafka:29092",
    Topic = builder.Configuration["KAFKA_TOPIC"] ?? "orders",
    ConsumerGroup = builder.Configuration["KAFKA_CONSUMER_GROUP"] ?? "payments-service-group"
};
builder.Services.AddSingleton(kafkaOpt);

string cs = Db.GetConnectionString(builder.Configuration);
NpgsqlDataSourceBuilder dataSourceBuilder = new(cs);
builder.Services.AddSingleton(dataSourceBuilder.Build());

builder.Services.AddSingleton<AccountRepository>();
builder.Services.AddSingleton<InboxRepository>();
builder.Services.AddSingleton<ChargeRepository>();
builder.Services.AddSingleton<OutboxRepository>();

builder.Services.AddSingleton<AccountsService>();
builder.Services.AddSingleton<PaymentProcessor>();

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
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnablePartitionEof = false
    };
    return new ConsumerBuilder<string, string>(config).Build();
});

builder.Services.AddHostedService<PaymentRequestsConsumer>();
builder.Services.AddHostedService<OutboxDispatcher>();

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/accounts",
        async (HttpContext ctx, AccountsService svc, CreateAccountRequest? body, CancellationToken ct) =>
        {
            Guid? userId = ResolveUserId(ctx, body?.UserId);
            if (userId is null)
            {
                return Results.BadRequest("user_id is required (X-User-Id header or request body)");
            }

            (Guid accountId, decimal balance, bool created) = await svc.CreateOrGetAsync(userId.Value, ct);
            CreateAccountResponse resp = new(accountId, userId.Value, balance);
            return created ? Results.Created($"/accounts/{accountId}", resp) : Results.Ok(resp);
        })
    .WithName("CreateAccount")
    .WithOpenApi();

app.MapPost("/accounts/create",
        async (HttpContext ctx, AccountsService svc, CreateAccountRequest? body, CancellationToken ct) =>
        {
            Guid? userId = ResolveUserId(ctx, body?.UserId);
            if (userId is null)
            {
                return Results.BadRequest("user_id is required (X-User-Id header or request body)");
            }

            (Guid accountId, decimal balance, bool created) = await svc.CreateOrGetAsync(userId.Value, ct);
            CreateAccountResponse resp = new(accountId, userId.Value, balance);
            return created ? Results.Created($"/accounts/{accountId}", resp) : Results.Ok(resp);
        })
    .WithName("CreateAccountLegacy")
    .WithOpenApi();

app.MapPost("/accounts/top_up", async (HttpContext ctx, AccountsService svc, TopUpRequest body, CancellationToken ct) =>
    {
        Guid? userId = ResolveUserId(ctx, null);
        if (userId is null)
        {
            return Results.BadRequest("X-User-Id header is required");
        }

        if (body.Amount <= 0)
        {
            return Results.BadRequest("amount must be > 0");
        }

        Guid accountId;
        if (body.AccountId is { } accId && accId != Guid.Empty)
        {
            accountId = accId;
        }
        else
        {
            (Guid accountId, Guid userId, decimal balance)? acc = await svc.GetByUserIdAsync(userId.Value, ct);
            if (acc is null)
            {
                return Results.NotFound("Account not found");
            }

            accountId = acc.Value.accountId;
        }

        (Guid accountId, Guid userId, decimal balance)? accRow = await svc.GetByAccountIdAsync(accountId, ct);
        if (accRow is null)
        {
            return Results.NotFound("Account not found");
        }

        if (accRow.Value.userId != userId.Value)
        {
            return Results.Forbid();
        }

        decimal newBalance;
        try
        {
            decimal? res = await svc.TopUpAsync(accountId, userId.Value, body.Amount, ct);
            if (res is null)
            {
                return Results.NotFound("Account not found");
            }

            newBalance = res.Value;
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.BadRequest("amount must be > 0");
        }

        return Results.Ok(new BalanceResponse(accountId, userId.Value, newBalance));
    })
    .WithName("TopUp")
    .WithOpenApi();

app.MapGet("/accounts/{accountId:guid}/balance",
        async (HttpContext ctx, AccountsService svc, Guid accountId, CancellationToken ct) =>
        {
            Guid? userId = ResolveUserId(ctx, null);
            if (userId is null)
            {
                return Results.BadRequest("X-User-Id header is required");
            }

            (Guid accountId, Guid userId, decimal balance)? acc = await svc.GetByAccountIdAsync(accountId, ct);
            if (acc is null)
            {
                return Results.NotFound("Account not found");
            }

            return acc.Value.userId != userId.Value
                ? Results.Forbid()
                : Results.Ok(new BalanceResponse(acc.Value.accountId, acc.Value.userId, acc.Value.balance));
        })
    .WithName("BalanceByAccountId")
    .WithOpenApi();

app.MapGet("/accounts/balance", async (HttpContext ctx, AccountsService svc, CancellationToken ct) =>
    {
        Guid? userId = ResolveUserId(ctx, null);
        if (userId is null)
        {
            return Results.BadRequest("X-User-Id header is required");
        }

        (Guid accountId, Guid userId, decimal balance)? acc = await svc.GetByUserIdAsync(userId.Value, ct);
        return acc is null
            ? Results.NotFound("Account not found")
            : Results.Ok(new BalanceResponse(acc.Value.accountId, acc.Value.userId, acc.Value.balance));
    })
    .WithName("BalanceByUser")
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