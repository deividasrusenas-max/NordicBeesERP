using System.Collections.Generic;
using System.Threading.Tasks;
using NordicBeesERP.Models;

public interface IDebtReconciliationService
{
    Task<DebtReconciliationResult> GetReconciliationAsync(int partnerId, int year, int? endMonth);
    Task<Dictionary<int, decimal>> GetBalancesBulkAsync(IEnumerable<int> partnerIds, int? year = null);
}
