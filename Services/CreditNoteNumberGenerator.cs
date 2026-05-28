using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services
{
    public class CreditNoteNumberGenerator : ICreditNoteNumberGenerator
    {
        private readonly NordicBeesERPContext _context;

        public CreditNoteNumberGenerator(NordicBeesERPContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Generates the next credit note number for the given date.
        /// Format: KLAK + YY (last 2 digits of year) + sequence (e.g., KLAK26001, KLAK26002...)
        /// Thread-safe using database transactions.
        /// </summary>
        /// <param name="creditDate">The credit note date (used to determine the year)</param>
        /// <param name="transaction">Optional database transaction for thread-safe operations</param>
        /// <returns>The generated credit note number</returns>
        public async Task<string> GenerateNextNumberAsync(DateTime creditDate, IDbContextTransaction? transaction = null)
        {
            var yearYY = creditDate.ToString("yy");
            var prefix = "KLAK";

            // Get the maximum existing sequence for the current year
            var maxSequence = await GetMaxSequenceAsync(yearYY, transaction);

            // Increment sequence by 1
            var newSequence = maxSequence + 1;

            // Format: AKLAK + YY + sequence (zero-padded to 4 digits)
            return $"{prefix}{yearYY}{newSequence:D4}";
        }

        /// <summary>
        /// Gets the maximum sequence number for the given year from the database.
        /// Uses raw SQL for thread-safe operations within a transaction.
        /// </summary>
        private async Task<int> GetMaxSequenceAsync(string yearYY, IDbContextTransaction? transaction)
        {
            var sql = $@"
                SELECT COALESCE(MAX(CAST(SUBSTRING(credit_note_number, 8, 4) AS UNSIGNED)), 0)
                FROM credit_notes
                WHERE credit_note_number LIKE 'KLAK{yearYY}%'
                AND credit_note_number REGEXP '^KLAK{yearYY}[0-9]{{4}}$'
            ";

            var connection = _context.Database.GetDbConnection();
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = transaction?.GetDbTransaction();

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            var result = await command.ExecuteScalarAsync();
            return result != null && int.TryParse(result.ToString(), out var seq) ? seq : 0;
        }
    }
}