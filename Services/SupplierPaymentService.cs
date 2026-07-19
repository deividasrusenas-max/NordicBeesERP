using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

public class SupplierPaymentService : ISupplierPaymentService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
    private readonly IDeliveryService _deliveryService;

    public SupplierPaymentService(IDbContextFactory<NordicBeesERPContext> contextFactory, IDeliveryService deliveryService)
    {
        _contextFactory = contextFactory;
        _deliveryService = deliveryService;
    }

    public async Task<List<SupplierPayment>> GetByDeliveryAsync(int deliveryId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.SupplierPayments
            .Where(p => p.DeliveryId == deliveryId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<List<SupplierPayment>> GetBySupplierAsync(int supplierId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.SupplierPayments
            .Where(p => p.SupplierId == supplierId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalPaidForDeliveryAsync(int deliveryId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.SupplierPayments
            .Where(p => p.DeliveryId == deliveryId)
            .SumAsync(p => p.Amount);
    }

    public async Task<int> CreatePaymentAsync(SupplierPayment payment)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            payment.CreatedAt = DateTime.Now;
            context.SupplierPayments.Add(payment);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            // Update delivery status
            await _deliveryService.UpdateDeliveryStatusAsync(payment.DeliveryId);
            
            return payment.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeletePaymentAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var payment = await context.SupplierPayments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (payment != null)
            {
                var deliveryId = payment.DeliveryId;
                await context.Database.ExecuteSqlRawAsync("DELETE FROM supplier_payments WHERE id = {0}", id);
                await transaction.CommitAsync();

                // Update delivery status
                await _deliveryService.UpdateDeliveryStatusAsync(deliveryId);
            }
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateAsync(SupplierPayment payment)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var existing = await context.SupplierPayments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == payment.Id);
            if (existing == null) return;

            // Save via raw SQL (NoTracking — Update + SaveChanges would silently do nothing)
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE supplier_payments SET amount = {0}, payment_date = {1}, payment_method = {2}, notes = {3} WHERE id = {4}",
                payment.Amount,
                payment.PaymentDate,
                payment.PaymentMethod,
                payment.Notes,
                payment.Id);

            await transaction.CommitAsync();

            // Update delivery status
            await _deliveryService.UpdateDeliveryStatusAsync(payment.DeliveryId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
