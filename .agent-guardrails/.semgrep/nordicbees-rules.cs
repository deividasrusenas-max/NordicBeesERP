// Semgrep rule-test fixture for .agent-guardrails/nordicbees-rules.yaml (Etapas 0c C8).
// Run: semgrep --test --config .agent-guardrails/nordicbees-rules.yaml .agent-guardrails/.semgrep/nordicbees-rules.cs
// Excluded from repo-wide scans (CI / pre-commit) by the explicit entry in /.semgrepignore, so the
// intentional ruleid: positives never show up there; explicitly passed targets bypass .semgrepignore.
// Not compiled: the .NET SDK default item excludes skip folders starting with '.' (**/.*/**).
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace RuleTests
{
    // =====================================================================
    // nordicbees-notracking-savechanges
    // =====================================================================
    public class SaveChangesCases
    {
        public async Task InsertOnly_Add(AppDb context)
        {
            context.Things.Add(new Thing());
            // ok: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task InsertOnly_AddAsync(AppDb context)
        {
            await context.Things.AddAsync(new Thing());
            // ok: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task InsertOnly_AddRange(AppDb ctx)
        {
            ctx.Things.AddRange(new Thing(), new Thing());
            // ok: nordicbees-notracking-savechanges
            await ctx.SaveChangesAsync();
        }

        public async Task InsertOnly_AfterUntrackedRead(AppDb context, int id)
        {
            var existing = await context.Things.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            context.Audits.Add(new Audit { ThingId = existing!.Id });
            // ok: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task FindAsync_Mutate(AppDb context, int id)
        {
            var thing = await context.Things.FindAsync(id);
            thing!.Name = "changed";
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task TrackedRead_Mutate(AppDb context, int id)
        {
            var thing = await context.Things.FirstOrDefaultAsync(t => t.Id == id);
            thing!.Name = "changed";
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task Update_Then_Save(AppDb context, Thing thing)
        {
            context.Things.Update(thing);
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task ContextUpdate_Then_Save(AppDb context, Thing thing)
        {
            context.Update(thing);
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task Remove_Then_Save(AppDb context, Thing thing)
        {
            context.Things.Remove(thing);
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task EntryIsModified_Then_Save(AppDb context, Thing thing)
        {
            context.Entry(thing).Property(t => t.Name).IsModified = true;
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }
    }

    // C8 fix-up (reviewer on 07e7be5): nested blocks, list+foreach, compound / nested mutation
    public class SaveChangesNestedCases
    {
        public async Task UpdateInLoop(AppDb context, Thing[] things)
        {
            foreach (var t in things) { context.Things.Update(t); }
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task RemoveInIf(AppDb context, Thing thing, bool remove)
        {
            if (remove) { context.Things.Remove(thing); }
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task FindMutateInTry(AppDb context, int id)
        {
            try
            {
                var thing = await context.Things.FindAsync(id);
                thing!.Name = "changed";
            }
            catch (Exception) { throw; }
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task ListForeachMutate(AppDb context)
        {
            var things = await context.Things.Where(t => t.Id > 0).ToListAsync();
            foreach (var t in things) { t.Name = "bulk"; }
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task CompoundAssign(AppDb context, int id)
        {
            var thing = await context.Things.FindAsync(id);
            thing!.Qty += 1;
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task NestedMember(AppDb context, int id)
        {
            var thing = await context.Things.SingleAsync(t => t.Id == id);
            thing.Child.Name = "nested";
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task TypedDeclaration(AppDb context, int id)
        {
            Thing? thing = await context.Things.FirstOrDefaultAsync(t => t.Id == id);
            if (thing == null) return;
            thing.Name = "typed";
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task EntryInForeach(AppDb context, Thing[] things)
        {
            foreach (var t in things) { context.Entry(t).Property(x => x.Name).IsModified = true; }
            // ruleid: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task InsertInLoop(AppDb context, Thing[] things)
        {
            foreach (var t in things) { context.Things.Add(t); }
            // ok: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }

        public async Task NonEfAwaitThenAdd(AppDb context, IThingSource source)
        {
            var dto = await source.GetAsync();
            dto.Name = "from service";
            context.Things.Add(dto);
            // ok: nordicbees-notracking-savechanges
            await context.SaveChangesAsync();
        }
    }

    // =====================================================================
    // nordicbees-stringcomparison-in-linq
    // =====================================================================
    public class StringComparisonCases
    {
        public async Task EfQuery_Terminal(AppDb context, string term)
        {
            // ruleid: nordicbees-stringcomparison-in-linq
            var list = await context.Things.Where(t => t.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToListAsync();
        }

        public async Task EfQuery_FirstOrDefault(AppDb db, string term)
        {
            // ruleid: nordicbees-stringcomparison-in-linq
            var one = await db.Things.FirstOrDefaultAsync(t => t.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase));
        }

        public void EfQuery_Built(AppDb context, string term)
        {
            // ruleid: nordicbees-stringcomparison-in-linq
            var query = context.Things.Where(t => t.Name.Equals(term, StringComparison.OrdinalIgnoreCase));
        }

        public bool InMemory_List(System.Collections.Generic.List<Thing> things, string term)
        {
            // ok: nordicbees-stringcomparison-in-linq
            return things.Any(t => t.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        public bool InMemory_Plain(string a, string b)
        {
            // ok: nordicbees-stringcomparison-in-linq
            return a.Equals(b, StringComparison.OrdinalIgnoreCase);
        }

        public async Task InMemory_AfterMaterialise(AppDb context, string term)
        {
            // ok: nordicbees-stringcomparison-in-linq
            var list = (await context.Things.ToListAsync()).Where(t => t.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    public class StringComparisonBuiltQueryCases
    {
        public async Task VarQueryBuiltStepByStep(AppDb context, string term, bool filter)
        {
            var q = context.Things.AsQueryable();
            if (filter)
            {
                // ruleid: nordicbees-stringcomparison-in-linq
                q = q.Where(t => t.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            var list = await q.ToListAsync();
        }

        public void TypedQueryBuilt(AppDb _context, string term)
        {
            IQueryable<Thing> query = _context.Things;
            // ruleid: nordicbees-stringcomparison-in-linq
            query = query.Where(t => t.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase));
        }

        public System.Collections.Generic.List<Thing> InMemoryListReassigned(System.Collections.Generic.List<Thing> things, string term)
        {
            var list = things;
            // ok: nordicbees-stringcomparison-in-linq
            list = list.Where(t => t.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
            return list;
        }
    }

    // =====================================================================
    // nordicbees-ef-decimal-precision-annotation-missing
    // =====================================================================
    [Table("things")]
    public class Thing
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Qty { get; set; }
        public Thing Child { get; set; } = null!;

        // ruleid: nordicbees-ef-decimal-precision-annotation-missing
        public decimal Price { get; set; }

        // ruleid: nordicbees-ef-decimal-precision-annotation-missing
        public decimal? Discount { get; set; } = 0m;

        [Precision(5, 2)]
        // ok: nordicbees-ef-decimal-precision-annotation-missing
        public decimal VatRate { get; set; }

        [Column("amount", TypeName = "decimal(12,2)")]
        // ok: nordicbees-ef-decimal-precision-annotation-missing
        public decimal Amount { get; set; }
    }

    public class ThingViewModel
    {
        // ok: nordicbees-ef-decimal-precision-annotation-missing
        public decimal Total { get; set; }
    }

    [Table("audits")]
    public class Audit
    {
        public int Id { get; set; }
        public int ThingId { get; set; }
    }

    public interface IThingSource
    {
        Task<Thing> GetAsync();
    }

    public class AppDb : DbContext
    {
        public DbSet<Thing> Things { get; set; } = null!;
        public DbSet<Audit> Audits { get; set; } = null!;
    }
}
