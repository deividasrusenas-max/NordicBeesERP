using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.Printing;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

/// <summary>
/// Orchestrates label printing: resolves station → printer, renders ZPL,
/// queues print job, records label event.
/// </summary>
public class LabelPrintService : ILabelPrintService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
    private readonly ILabelTemplateService _templateService;

    public LabelPrintService(
        IDbContextFactory<NordicBeesERPContext> contextFactory,
        ILabelTemplateService templateService)
    {
        _contextFactory = contextFactory;
        _templateService = templateService;
    }

    public async Task PrintReceiptLabelAsync(int containerId, int stationId, int? operatorId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var container = await context.Containers
            .FirstOrDefaultAsync(c => c.Id == containerId);
        if (container == null)
            throw new ArgumentException($"Container {containerId} not found");

        var station = await context.WeighingStations
            .FirstOrDefaultAsync(s => s.Id == stationId);
        if (station == null)
            throw new ArgumentException($"WeighingStation {stationId} not found");

        var printer = await context.Printers
            .FirstOrDefaultAsync(p => p.Id == station.PrinterId);
        if (printer == null)
            throw new ArgumentException($"Printer {station.PrinterId} not found");

        var (delivery, supplierName, materialName, warehouseName) = await ResolveDeliveryInfoAsync(context, container);

        var templateType = container.ContainerType == "BUCKET"
            ? LabelTemplateType.RECEIPT_BUCKET
            : LabelTemplateType.RECEIPT_BARREL;

        var labelData = BuildLabelData(container, delivery, supplierName, materialName, warehouseName);
        var zpl = _templateService.RenderZpl(templateType, labelData);

        // Queue print job
        var job = new PrintJob
        {
            ContainerId = container.Id,
            PrinterId = printer.Id,
            StationId = station.Id,
            JobType = "RECEIPT_LABEL",
            ZplContent = zpl,
            Status = "PENDING",
            RetryCount = 0,
            CreatedByUserId = operatorId,
            CreatedAt = DateTime.Now
        };
        context.PrintJobs.Add(job);
        await context.SaveChangesAsync();

        // Update container label tracking
        container.LastLabelPrintedAt = DateTime.Now;
        container.LabelPrintCount += 1;
        context.Containers.Update(container);
        await context.SaveChangesAsync();

        // Record label event
        var @event = new ContainerLabelEvent
        {
            ContainerId = container.Id,
            PrintJobId = job.Id,
            EventType = "PRINTED",
            OperatorId = operatorId,
            CreatedAt = DateTime.Now
        };
        context.ContainerLabelEvents.Add(@event);
        await context.SaveChangesAsync();
    }

    public async Task PrintQuarantineLabelAsync(int containerId, int stationId, int? operatorId, int? nonConformanceId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var container = await context.Containers
            .FirstOrDefaultAsync(c => c.Id == containerId);
        if (container == null)
            throw new ArgumentException($"Container {containerId} not found");

        var station = await context.WeighingStations
            .FirstOrDefaultAsync(s => s.Id == stationId);
        if (station == null)
            throw new ArgumentException($"WeighingStation {stationId} not found");

        var printer = await context.Printers
            .FirstOrDefaultAsync(p => p.Id == station.PrinterId);
        if (printer == null)
            throw new ArgumentException($"Printer {station.PrinterId} not found");

        var (delivery, supplierName, materialName, warehouseName) = await ResolveDeliveryInfoAsync(context, container);

        var templateType = container.ContainerType == "BUCKET"
            ? LabelTemplateType.QUARANTINE_BUCKET
            : LabelTemplateType.QUARANTINE_BARREL;

        var labelData = BuildLabelData(container, delivery, supplierName, materialName, warehouseName);
        labelData.NonConformanceId = nonConformanceId;
        var zpl = _templateService.RenderZpl(templateType, labelData);

        // Queue print job
        var job = new PrintJob
        {
            ContainerId = container.Id,
            PrinterId = printer.Id,
            StationId = station.Id,
            JobType = "QUARANTINE_LABEL",
            ZplContent = zpl,
            Status = "PENDING",
            RetryCount = 0,
            CreatedByUserId = operatorId,
            CreatedAt = DateTime.Now
        };
        context.PrintJobs.Add(job);
        await context.SaveChangesAsync();

        // Update container label tracking
        container.LastLabelPrintedAt = DateTime.Now;
        container.LabelPrintCount += 1;
        context.Containers.Update(container);
        await context.SaveChangesAsync();

        // Record label event
        var @event = new ContainerLabelEvent
        {
            ContainerId = container.Id,
            PrintJobId = job.Id,
            EventType = "QUARANTINE_PRINTED",
            OperatorId = operatorId,
            CreatedAt = DateTime.Now
        };
        context.ContainerLabelEvents.Add(@event);
        await context.SaveChangesAsync();
    }

    public async Task ReprintLabelAsync(int containerId, ReprintReasonCode reasonCode, string? reasonText, int? operatorId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var container = await context.Containers
            .FirstOrDefaultAsync(c => c.Id == containerId);
        if (container == null)
            throw new ArgumentException($"Container {containerId} not found");

        var (delivery, supplierName, materialName, warehouseName) = await ResolveDeliveryInfoAsync(context, container);

        // Find the default active printer for the warehouse
        var printer = await context.Printers
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync();
        if (printer == null)
            throw new InvalidOperationException("No active printer found");

        var templateType = container.ContainerType == "BUCKET"
            ? LabelTemplateType.RECEIPT_BUCKET
            : LabelTemplateType.RECEIPT_BARREL;

        var labelData = BuildLabelData(container, delivery, supplierName, materialName, warehouseName);
        var zpl = _templateService.RenderZpl(templateType, labelData);

        // Queue print job
        var job = new PrintJob
        {
            ContainerId = container.Id,
            PrinterId = printer.Id,
            JobType = "REPRINT",
            ZplContent = zpl,
            Status = "PENDING",
            RetryCount = 0,
            CreatedByUserId = operatorId,
            CreatedAt = DateTime.Now
        };
        context.PrintJobs.Add(job);
        await context.SaveChangesAsync();

        // Update container label tracking
        container.LastLabelPrintedAt = DateTime.Now;
        container.LabelPrintCount += 1;
        context.Containers.Update(container);
        await context.SaveChangesAsync();

        // Record label event
        var @event = new ContainerLabelEvent
        {
            ContainerId = container.Id,
            PrintJobId = job.Id,
            EventType = "REPRINTED",
            OperatorId = operatorId,
            ReasonCode = reasonCode.ToString(),
            ReasonText = reasonText,
            CreatedAt = DateTime.Now
        };
        context.ContainerLabelEvents.Add(@event);
        await context.SaveChangesAsync();
    }

    private static async Task<(Delivery Delivery, string SupplierName, string MaterialName, string WarehouseName)>
        ResolveDeliveryInfoAsync(NordicBeesERPContext context, Container container)
    {
        var deliveryLine = await context.DeliveryLines
            .FirstOrDefaultAsync(dl => dl.Id == container.DeliveryLineId);
        if (deliveryLine == null)
            throw new InvalidOperationException($"Container {container.Id} has no delivery line");

        var delivery = await context.Deliveries
            .FirstOrDefaultAsync(d => d.Id == deliveryLine.DeliveryId);
        if (delivery == null)
            throw new InvalidOperationException($"Container {container.Id} has no delivery");

        var supplier = await context.BusinessPartners
            .FirstOrDefaultAsync(bp => bp.Id == delivery.SupplierId);
        var material = await context.RawMaterialTypes
            .FirstOrDefaultAsync(rm => rm.Id == delivery.RawMaterialTypeId);
        var warehouse = await context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == delivery.WarehouseId);

        return (delivery, supplier?.Name ?? "Unknown", material?.Name ?? "Unknown", warehouse?.Name ?? "Unknown");
    }

    private static ContainerLabelData BuildLabelData(
        Container container, Delivery delivery, string supplierName, string materialName, string warehouseName)
    {
        return new ContainerLabelData
        {
            ContainerCode = container.ContainerCode ?? "",
            DeliveryNumber = delivery.DeliveryNumber ?? "",
            SupplierName = supplierName,
            RawMaterialName = materialName,
            OriginCountry = delivery.OriginCountry ?? "",
            NetWeightKg = container.NetWeight,
            TareWeightKg = container.TareWeight,
            GrossWeightKg = container.GrossWeight,
            DeliveryDate = delivery.DeliveryDate,
            WarehouseName = warehouseName,
            ContainerType = container.ContainerType ?? "BARREL",
            NonConformanceId = null
        };
    }
}
