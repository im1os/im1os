using iM1os.Application.Payments;

namespace iM1os.Application.FinancialServices.Payments;

public interface IPaymentService
{
    Task<PaymentsWorkspace> GetWorkspaceAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<PaymentTransactionResult> CreateSaleAsync(Guid organizationId, Guid actorUserId, PaymentSaleRequest request, CancellationToken cancellationToken);
}
