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
            var payment = await context.SupplierPayments.FindAsync(id);
            if (payment != null)
            {
                var deliveryId = payment.DeliveryId;
                context.SupplierPayments.Remove(payment);
                await context.SaveChangesAsync();
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
            var existing = await context.SupplierPayments.FindAsync(payment.Id);
            if (existing == null) return;
            
            existing.Amount = payment.Amount;
            existing.PaymentDate = payment.PaymentDate;
            existing.PaymentMethod = payment.PaymentMethod;
            existing.Notes = payment.Notes;
            
            context.Entry(existing).Property(x => x.Amount).IsModified = true;
            context.Entry(existing).Property(x => x.PaymentDate).IsModified = true;
            context.Entry(existing).Property(x => x.PaymentMethod).IsModified = true;
            context.Entry(existing).Property(x => x.Notes).IsModified = true;
            
            await context.SaveChangesAsync();
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
