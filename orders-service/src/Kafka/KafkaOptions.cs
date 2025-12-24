namespace OrdersService.Kafka
{
    public sealed class KafkaOptions
    {
        public string Brokers { get; init; } = "";
        public string Topic { get; init; } = "orders";
        public string ConsumerGroup { get; init; } = "orders-service";
    }
}