namespace NordicBeesERP.Services;

public interface IGooglePlacesService
{
    Task<bool> IsHealthyAsync();
}
