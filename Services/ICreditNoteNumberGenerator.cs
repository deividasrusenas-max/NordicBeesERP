using Microsoft.EntityFrameworkCore.Storage;
using System.Threading.Tasks;

namespace NordicBeesERP.Services
{
    public interface ICreditNoteNumberGenerator
    {
        /// <summary>
        /// Generates the next credit note number for the given date.
        /// Format: KLAK + YY (last 2 digits of year) + sequence (e.g., KLAK26001, KLAK26002...)
        /// </summary>
        /// <param name="creditDate">The credit note date (used to determine the year)</param>
        /// <param name="transaction">Optional database transaction for thread-safe operations</param>
        /// <returns>The generated credit note number</returns>
        Task<string> GenerateNextNumberAsync(System.DateTime creditDate, IDbContextTransaction? transaction = null);
    }
}