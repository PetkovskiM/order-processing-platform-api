using OrderProcessing.Api.Services.Messaging;
using OrderProcessing.Contracts.Orders;

namespace OrderProcessing.Api.Tests.Unit;

public sealed class RabbitMqTopologyTests
{
    [Fact]
    public void GetRoutingKey_ForOrderCreatedEvent_ReturnsCreatedKey()
    {
        var routingKey = RabbitMqTopology.GetRoutingKey(
            typeof(OrderCreatedIntegrationEvent).FullName!);

        Assert.Equal(
            RabbitMqTopology.OrderCreatedRoutingKey,
            routingKey);
    }

    [Fact]
    public void GetRoutingKey_ForOrderCompletedEvent_ReturnsCompletedKey()
    {
        var routingKey = RabbitMqTopology.GetRoutingKey(
            typeof(OrderCompletedIntegrationEvent).FullName!);

        Assert.Equal(
            RabbitMqTopology.OrderCompletedRoutingKey,
            routingKey);
    }

    [Fact]
    public void GetRoutingKey_ForOrderCancelledEvent_ReturnsCancelledKey()
    {
        var routingKey = RabbitMqTopology.GetRoutingKey(
            typeof(OrderCancelledIntegrationEvent).FullName!);

        Assert.Equal(
            RabbitMqTopology.OrderCancelledRoutingKey,
            routingKey);
    }

    [Fact]
    public void GetRoutingKey_ForUnknownEvent_ThrowsException()
    {
        var action = () =>
            RabbitMqTopology.GetRoutingKey(
                "UnknownIntegrationEvent");

        var exception =
            Assert.Throws<InvalidOperationException>(
                action);

        Assert.Contains(
            "UnknownIntegrationEvent",
            exception.Message);
    }
}