using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using PayoutData = BTCPayServer.Data.PayoutData;

public interface IPayoutHandler
{
    public bool CanHandle(PaymentMethodId paymentMethod);
    public Task TrackClaim(PaymentMethodId paymentMethodId, IClaimDestination claimDestination);
    public Task<IClaimDestination> ParseClaimDestination(PaymentMethodId paymentMethodId, string destination);
    public IPayoutProof ParseProof(PayoutData payout);
    void StartBackgroundCheck(Action<Type[]> subscribe);
    Task BackgroundCheck(object o);
    Task<decimal> GetMinimumPayoutAmount(PaymentMethodId paymentMethod, IClaimDestination claimDestination);
    Dictionary<PayoutState, List<(string Action, string Text)>> GetPayoutSpecificActions();
    Task<StatusMessageModel> DoSpecificAction(string action, string[] payoutIds, string storeId);
}
