using System;
using System.Data;

namespace NordicBeesERP.Services;

public class TestBadPatterns
{
    private readonly string _adminPassword = "Admin123!";

    public void BadInsert(IDbCommand cmd, string lotNumber)
    {
        cmd.CommandText = $"INSERT INTO order_lines (lot_number) VALUES ('{lotNumber}')";
    }

    public async System.Threading.Tasks.Task BadSave(dynamic context)
    {
        await context.SaveChangesAsync();
    }
}
