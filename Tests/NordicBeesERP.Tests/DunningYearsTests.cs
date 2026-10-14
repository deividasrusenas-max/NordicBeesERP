using NordicBeesERP.Services.Dunning;
using Xunit;

namespace NordicBeesERP.Tests;

public class DunningYearsTests
{
    [Fact]
    public void Options_start_at_current_year_and_go_back_four_years() =>
        Assert.Equal(new[] { 2026, 2025, 2024, 2023, 2022 }, DunningYears.Options(2026));
}
