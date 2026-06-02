using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NordicBeesERP.Data;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NordicBeesERPContext))]
    [Migration("20260602150000_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
#pragma warning restore 612, 618
        }
    }
}