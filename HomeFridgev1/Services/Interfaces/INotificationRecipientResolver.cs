namespace HomeFridgev1.Services.Interfaces
{
    public interface INotificationRecipientResolver
    {
        Task<List<string>> GetRecipientsForExpiryAsync(int householdId, CancellationToken cancellationToken = default);

        Task<List<string>> GetRecipientsForOutOfStockAsync(int householdId, CancellationToken cancellationToken = default);
    }
}
