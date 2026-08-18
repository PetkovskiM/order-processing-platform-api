using OrderProcessing.ReadModels.Orders;

namespace OrderProcessing.Api.Features.Orders.Queries.ReadModel;

public sealed record OrderReadModelPage(IReadOnlyList<OrderReadModel> Items, int TotalCount);