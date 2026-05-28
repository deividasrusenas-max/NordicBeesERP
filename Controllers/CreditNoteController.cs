// =====================================================
// NORDIC BEES ERP - CREDIT NOTE API CONTROLLER
// Framework: .NET 10
// =====================================================

using Microsoft.AspNetCore.Mvc;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Dtos;

namespace NordicBeesERP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CreditNoteController : ControllerBase
    {
        private readonly ICreditNoteService _creditNoteService;

        public CreditNoteController(ICreditNoteService creditNoteService)
        {
            _creditNoteService = creditNoteService;
        }

        // =====================================================
        // SEARCH INVOICES FOR CREDIT NOTE
        // =====================================================
        
        [HttpGet("search-invoices")]
        public async Task<ActionResult<List<InvoiceModel>>> SearchInvoices(
            [FromQuery] string? searchTerm,
            [FromQuery] int limit = 20,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var invoices = await _creditNoteService.SearchInvoicesAsync(
                    searchTerm ?? string.Empty, 
                    limit, 
                    cancellationToken);
                
                return Ok(invoices);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        
        // =====================================================
        // PDF GENERATION
        // =====================================================
        
        [HttpGet("pdf/{id:int}")]
        public async Task<IActionResult> GetPdf(int id)
        {
            try
            {
                var pdfBytes = await _creditNoteService.GeneratePdfAsync(id);
                return File(pdfBytes, "application/pdf", "Kreditine_saskaita.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}