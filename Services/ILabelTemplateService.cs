using NordicBeesERP.Models.Printing;

namespace NordicBeesERP.Services;

public interface ILabelTemplateService
{
    string RenderZpl(LabelTemplateType type, ContainerLabelData data);
    Task<byte[]> PreviewPngAsync(string zpl, int labelWidthMm = 108, int labelHeightMm = 75);
}
