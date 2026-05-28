using System.Text;
using System.Xml.Linq;

namespace NordicBeesERP.Services;

public class ViesResult
{
    public bool IsValid { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? CountryCode { get; set; }
    public string? VatNumber { get; set; }
    public string? Error { get; set; }
    public bool ServiceAvailable { get; set; } = true;
}

public interface IViesService
{
    Task<ViesResult> LookupAsync(string vatCode);
}

public class ViesService(IHttpClientFactory httpClientFactory, ILogger<ViesService> logger) : IViesService
{
    private const string ViesUrl = "https://ec.europa.eu/taxation_customs/vies/services/checkVatService";

    public async Task<ViesResult> LookupAsync(string vatCode)
    {
        if (string.IsNullOrWhiteSpace(vatCode) || vatCode.Length < 4)
            return new ViesResult { Error = "Nėra VAT kodo" };

        // Extract country code and number
        var countryCode = vatCode[..2].ToUpper();
        var number = vatCode[2..].Trim();

        try
        {
            var soapBody = $"""
                <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" 
                                  xmlns:urn="urn:ec.europa.eu:taxud:vies:services:checkVat:types">
                    <soapenv:Body>
                        <urn:checkVat>
                            <urn:countryCode>{countryCode}</urn:countryCode>
                            <urn:vatNumber>{number}</urn:vatNumber>
                        </urn:checkVat>
                    </soapenv:Body>
                </soapenv:Envelope>
                """;

            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var request = new HttpRequestMessage(HttpMethod.Post, ViesUrl)
            {
                Content = new StringContent(soapBody, Encoding.UTF8, "text/xml")
            };
            request.Headers.Add("SOAPAction", "");

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("VIES HTTP {Status}", response.StatusCode);
                return new ViesResult { ServiceAvailable = false, Error = $"VIES HTTP {(int)response.StatusCode}" };
            }

            var xml = await response.Content.ReadAsStringAsync();
            logger.LogDebug("VIES response: {Xml}", xml[..Math.Min(300, xml.Length)]);

            var doc = XDocument.Parse(xml);
            XNamespace ns = "urn:ec.europa.eu:taxud:vies:services:checkVat:types";

            var checkVatResponse = doc.Descendants(ns + "checkVatResponse").FirstOrDefault();
            if (checkVatResponse == null)
            {
                // Check for fault
                var fault = doc.Descendants("faultstring").FirstOrDefault();
                if (fault != null)
                    return new ViesResult { IsValid = false, Error = fault.Value };
                return new ViesResult { ServiceAvailable = false, Error = "Nežinoma VIES klaida" };
            }

            var isValid = checkVatResponse.Element(ns + "valid")?.Value == "true";
            var name = checkVatResponse.Element(ns + "name")?.Value?.Trim();
            var address = checkVatResponse.Element(ns + "address")?.Value?.Trim();

            // VIES returns "---" when data not available
            if (name == "---") name = null;
            if (address == "---") address = null;

            return new ViesResult
            {
                IsValid = isValid,
                Name = name,
                Address = address,
                CountryCode = countryCode,
                VatNumber = vatCode,
                ServiceAvailable = true
            };
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("VIES timeout for {VatCode}", vatCode);
            return new ViesResult { ServiceAvailable = false, Error = "VIES timeout" };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "VIES error for {VatCode}", vatCode);
            return new ViesResult { ServiceAvailable = false, Error = ex.Message };
        }
    }
}