using System.Net.Http.Headers;
using NordicBeesERP.Models.Printing;

namespace NordicBeesERP.Services;

/// <summary>
/// ZPL label template renderer. Produces ZPL for receipt and quarantine labels.
/// P0: hardcoded ZPL. P1 will use Scriban templates.
/// </summary>
public class ZplLabelTemplateService : ILabelTemplateService
{
    private readonly HttpClient _httpClient;

    public ZplLabelTemplateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string RenderZpl(LabelTemplateType type, ContainerLabelData data)
    {
        return type switch
        {
            LabelTemplateType.RECEIPT_BARREL => RenderReceiptZpl(data, "BARREL"),
            LabelTemplateType.RECEIPT_BUCKET => RenderReceiptZpl(data, "BUCKET"),
            LabelTemplateType.QUARANTINE_BARREL => RenderQuarantineZpl(data, "BARREL"),
            LabelTemplateType.QUARANTINE_BUCKET => RenderQuarantineZpl(data, "BUCKET"),
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unknown template type: {type}")
        };
    }

    public async Task<byte[]> PreviewPngAsync(string zpl, int labelWidthMm = 108, int labelHeightMm = 75)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(zpl));
        var response = await _httpClient.PostAsync(
            $"https://api.labelary.com/v1/printers/{labelWidthMm}x{labelHeightMm}/labels/0.2/0/1/png/",
            new StringContent(encoded, System.Text.Encoding.UTF8, "text/plain"));

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static string RenderReceiptZpl(ContainerLabelData data, string containerType)
    {
        var qrData = $"{data.ContainerCode}|{data.DeliveryNumber}|{data.SupplierName}|{data.NetWeightKg}|{data.DeliveryDate:yyyy-MM-dd}";

        return $@"^XA
^PON
^LL400
^LH0,0
^FO50,50^A0N,45,40^FD{data.ContainerCode}^FS
^FO50,110^A0N,20,18^FD{TranslateContainerType(containerType)}^FS
^FO50,150^A0N,20,18^FD{data.SupplierName}^FS
^FO50,190^A0N,20,18^FD{data.RawMaterialName}^FS
^FO50,230^A0N,20,18^FD{data.OriginCountry}^FS
^FO50,270^A0N,25,22^FD{data.NetWeightKg:F3} kg^FS
^FO50,310^A0N,20,18^FD{data.DeliveryDate:yyyy-MM-dd}^FS
^FO50,350^A0N,20,18^FD{data.WarehouseName}^FS
^FO50,390^BQN,2,8^FD{qrData}^FS
^XZ";
    }

    private static string RenderQuarantineZpl(ContainerLabelData data, string containerType)
    {
        var qrData = $"{data.ContainerCode}|QUARANTINE|{data.DeliveryNumber}|{data.DeliveryDate:yyyy-MM-dd}";

        return $@"^XA
^PON
^LL450
^LH0,0
^FO50,50^A0N,45,40^FD{data.ContainerCode}^FS
^FO200,50^A0N,50,45^FS
^FO200,50^XGQUARANTINE,1,1^FS
^FO50,120^A0N,20,18^FD{TranslateContainerType(containerType)}^FS
^FO50,160^A0N,20,18^FD{data.SupplierName}^FS
^FO50,200^A0N,20,18^FD{data.RawMaterialName}^FS
^FO50,240^A0N,20,18^FD{data.NetWeightKg:F3} kg^FS
^FO50,280^A0N,20,18^FD{data.DeliveryDate:yyyy-MM-dd}^FS
^FO50,320^A0N,20,18^FD{data.WarehouseName}^FS
^FO50,360^A0N,20,18^FDNC-{data.NonConformanceId}^FS
^FO50,400^BQN,2,8^FD{qrData}^FS
^XZ";
    }

    private static string TranslateContainerType(string type) => type switch
    {
        "BARREL" => "Bakas",
        "BUCKET" => "Dėžės",
        _ => type
    };
}
