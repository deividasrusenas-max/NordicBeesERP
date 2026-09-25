<!-- Archived 2026-09-25 from .opencode/tasks/latest.md. Executed; report: .opencode/reports/page-authorization-20260916-2100.md -->

# TASK: Enable page authorisation (Option A)

## Task type

**BUILD.** Four commits. Follow `AGENTS.md`; where this task and `AGENTS.md` disagree,
`AGENTS.md` wins — stop and report.

### Authority rule

A bare "continue", whether typed by a human or injected by auto-resume, is **not**
approval. It does not authorise starting new work, moving past a STOP condition, or
acting on your own recommendation. If this session is resumed automatically with no
explicit human instruction naming what to do, stop and say so.

**Do not push.** Commit and bump, then stop and tell the owner. Pushing to `main`
recreates the staging container; the owner decides when that happens.

### Database rule

No schema changes in this task. Dev database reads only if needed. No production, no
staging.

## Preconditions

1. `git branch --show-current` is `main`. If not, STOP.
2. `git status --porcelain` is clean. If not, STOP and report what is dirty.
3. Read first — these are established findings, do not re-derive them:
   - `.opencode/reports/authorize-inventory-20260916-1259.md`
   - `.opencode/reports/authz-design-inputs-20260916-1352.md` (the D2 route table is the
     input for commit 2)
4. `mempalace` is off.

## Established facts — verified, build on them

- `Components/App.razor:15` is `<Routes @rendermode="InteractiveServer" />` (prerender on
  by default).
- `Components/Routes.razor` uses `RouteView`, not `AuthorizeRouteView`.
- `Components/Layout/MainLayout.razor:14` already wraps its content in
  `<CascadingAuthenticationState>`. **That is below `AuthorizeRouteView`** — it renders
  inside the layout, which `AuthorizeRouteView` itself renders. It cannot supply the
  cascade that `AuthorizeRouteView` needs.
- `Program.cs` has no `UseAuthentication()` / `UseAuthorization()`. **Do not add them.**
  Blazor interactive pages authorise through `AuthenticationStateProvider`, not
  middleware; adding middleware without `SignInAsync` creates the split-brain where
  `[Authorize]` endpoints reject everyone including the Admin.
- `Services/BlazorAuthStateProvider.cs:37-40` returns anonymous during prerender
  (`ProtectedLocalStorage` is unavailable server-side).
- Production roles (owner-run query): Admin 1, Manager 1, Warehouse 2, Designer 1 — all
  active, all correctly cased.

---

## Commit 1 — Turn authorisation on

1. `Program.cs` — add `builder.Services.AddCascadingAuthenticationState();` alongside the
   existing `AddAuthorizationCore()`.
2. `Components/Routes.razor` — replace `RouteView` with `AuthorizeRouteView`, keeping
   `DefaultLayout="@typeof(MainLayout)"`.
3. Supply both render fragments. Neither may be left to its default:

   **`<Authorizing>`** — a centred spinner. This covers the prerender pass, where the
   provider returns anonymous before `ProtectedLocalStorage` is readable. Without it, a
   logged-in user pressing F5 on a protected page sees a "no access" flash on every hard
   load.

   **`<NotAuthorized>`** — branch on `context.User.Identity?.IsAuthenticated`:
   - not authenticated → navigate to `/login` (include the attempted path as a return
     parameter if `Login.razor` already supports one; check before adding, do not invent
     a contract)
   - authenticated, wrong role → a short Lithuanian message naming that the user lacks
     permission, plus a link back to `/`. Do not redirect to `/login` — re-logging in
     will not help and is confusing.

4. Leave the `<CascadingAuthenticationState>` in `MainLayout.razor:14` in place. Nested
   cascade is redundant, not harmful; removing it is a separate cleanup. Note it in the
   report.

**Verify before committing**, and paste the evidence: log in as each of the three dev
users in turn and confirm `/admin/users` opens for Admin and shows the not-authorised
message for the others. If the dev database has no usable passwords, say so and state
what you verified instead — do not claim a check you did not run.

## Commit 2 — Attributes on the unattributed routes

Apply the D2 table from `authz-design-inputs-20260916-1352.md` §D2, with these
owner decisions overriding it:

- **These four move from the Warehouse group to `[Authorize(Roles = "Admin,Manager")]`** —
  Warehouse must not see anything money-related:
  `/warehouse/delivery-pricing` · `/warehouse/deliveries/{Id:int}/pricing` ·
  `/warehouse/supplier-debts` · `/warehouse/supplier-debts/{SupplierId:int}`
- `/login`, `/Error`, `/not-found` stay public — no attribute.
- Everything else exactly as the table proposes. `Manager` stays in the role lists.

Add the attribute to the page component, next to the existing `@page` directive. Change
nothing else in these files — no formatting, no refactoring, no touching component logic.

Report any route in the table whose file no longer matches the line reference, and stop
rather than guessing.

## Commit 3 — Delete the template leftovers

Delete `Components/Pages/Counter.razor` and `Components/Pages/Weather.razor`. Grep for
any link or reference to `/counter` and `/weather` first; if anything references them,
stop and report instead of deleting.

## Commit 4 — Lockout guards in `ErpUserService`

With authorisation live, the D6 trap becomes real: deactivating or demoting the last
active Admin locks everyone out of `/admin/users` permanently, and re-seeding resets the
password without re-activating the account.

Add server-side guards — in the service, not the UI:

- refuse to deactivate the last active Admin
- refuse to change the role of the last active Admin away from Admin
- refuse to let a user deactivate or demote their own account

Each refusal returns a domain error with a Lithuanian message naming the reason, never a
silent no-op and never an unhandled exception. The UI surfaces whatever the service
returns.

Unit tests for all three, plus the passing case (deactivating a non-last Admin when a
second active Admin exists).

---

## Rules for all commits

- One concern per commit, four commits, in this order. Do not squash.
- Every commit builds clean and its tests pass before the next.
- New user-facing strings in Lithuanian.
- No new NuGet packages. No schema changes. No migrations.
- Do not refactor adjacent code because it looks wrong — note it in the report.
- `Docs/FROZEN.md` — check before touching any file it names; §2 claims a script block in
  `App.razor` that no longer exists (drift), report it, do not act on it.
- Full `dotnet test` must pass before the bump. `DbTestFixture` has a working default
  connection string; if `bump-version.sh` skips its own test gate, run `dotnet test`
  yourself and report both.
- `bump-version.sh patch` once, after commit 4. **Then stop — do not push.**

## Verification required in the report

`git status` is not accepted as proof of a clean tree (D-012). Provide:

1. `git log --oneline -6` — four commits plus the bump.
2. `git show --stat` for each commit.
3. File inventory with modification times, including git-ignored directories:
   `find . -newermt "<session start>" -type f -not -path "./.git/*" -not -path "*/bin/*" -not -path "*/obj/*" -printf "%T+ %p\n" | sort`
4. Full `dotnet test` output.
5. The per-role access check from commit 1, or an explicit statement of what could not be
   verified and why.
6. Confirmation that `git log --oneline origin/main..HEAD` is NOT empty — i.e. nothing was
   pushed.

## Report

`.opencode/reports/authz-enable-<YYYYMMDD-HHMM>.md`:

1. What each commit changed, with `file:line`.
2. The `<NotAuthorized>` / `<Authorizing>` behaviour actually implemented, and how each
   was verified.
3. Any route from the D2 table that did not match its file reference.
4. The redundant `MainLayout` cascade — confirm it was left in place.
5. The six verification outputs above.
6. **What a human must check in a browser before this is deployed** — a short list.

## STOP conditions

- Branch is not `main`, or the tree is dirty at start.
- A route file does not match the D2 table reference.
- `/counter` or `/weather` is referenced anywhere.
- `Login.razor` has no return-path parameter and adding one would widen the commit.
- You are about to add `UseAuthentication()` or `UseAuthorization()`.
- You are about to push.
- Any instruction here conflicts with `AGENTS.md`.
