using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Part D (deadlock flake fix): every test class in this project that touches the real, shared
/// `nordic_bees_erp_test` database (i.e. uses <see cref="DbTestFixture"/>) is tagged
/// <c>[Collection("RealDatabase")]</c>. xUnit runs test CLASSES in different collections in
/// parallel by default; classes sharing one collection name run sequentially relative to each
/// other. Three different classes (CreditNoteServiceTests before A3's fix,
/// ExpenseSupplierChangeAtomicityTests, ExpenseSupplierIdentityKeptTests) hit real MySQL
/// deadlocks/phantom-row races from concurrent writes to overlapping shared tables
/// (business_partners, currencies, invoice_lines, …) under the previous default (52 classes, 52
/// separate implicit collections, all running in parallel against one physical database with no
/// per-test transaction isolation). Grouping them into one collection is the standard, documented
/// xUnit pattern for "tests that cannot run in parallel because they share an external resource" —
/// not a retry, a skip, or a loosened assertion. Pure-unit-test classes (no DbTestFixture) are
/// unaffected and keep running in parallel with this collection and each other.
/// </summary>
[CollectionDefinition("RealDatabase")]
public class RealDatabaseCollection
{
}
