namespace OrderProcessing.ReadModelWorker.Configuration;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string DatabaseName { get; set; } = "OrderProcessingReadDb";

    public string OrdersCollectionName { get; set; } = "orders";
}