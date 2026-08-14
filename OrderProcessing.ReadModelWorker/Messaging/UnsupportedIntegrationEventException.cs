namespace OrderProcessing.ReadModelWorker.Messaging
{
    public sealed class UnsupportedIntegrationEventException : Exception
    {
        public UnsupportedIntegrationEventException(string eventType) :
            base($"Integration event type '{eventType}'  is not supported by the email worker.")
        {
        }
    }
}
