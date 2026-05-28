BEFORE EVERY TASK:
1. Read .roo/rules-code/01-nordicbees.md using filesystem MCP
2. Read the FULL file before editing using filesystem MCP
3. Verify DB schema with MySQL MCP if touching models or services

CRITICAL C# RULES:
- ALWAYS use IDbContextFactory<NordicBeesERPContext>
- ALWAYS: using var context = await _dbContextFactory.CreateDbContextAsync();
- ALWAYS mark modified properties before SaveChangesAsync:
  context.Entry(entity).Property(p => p.Field).IsModified = true;
- ALWAYS add [Column("snake_case")] to every model property
- NEVER use .Include()
- NEVER use EF navigation properties
- Decimal culture: always InvariantCulture, NEVER lt-LT
- List<T> not ICollection<T> in models

CRITICAL BLAZOR RULES:
- @inject via INTERFACE only: @inject IInvoiceService InvoiceService
- NEVER: @inject NordicBeesERP.Services.InvoiceService
- @using directives only in _Imports.razor
- Every page needs @page directive

CRITICAL MUDBLAZOR v8.15.0 RULES:
- MudText NOT MudTypography
- Variant.Filled NOT Variant.Contained
- MudTable MUST have T attribute
- MudTable: HeaderContent / RowTemplate Context="item" / FooterContent
- NEVER use Header / Rows / Footer inside MudTable
- Lines="3" NOT Rows="3" on MudTextField
- All generic components need T attribute

AFTER EVERY FILE CHANGE:
1. dotnet build - fix ALL errors before moving on
2. git add . && git commit -m "[Module] description"

FORBIDDEN:
- Partial code or snippets
- Code with build errors
- Touching files outside task scope
- Hardcoding passwords
- EF migrations
- .Include() calls

BEFORE WRITING ANY SERVICE:
- Read the model file first: filesystem MCP read Models/Expenses/XxxModel.cs
- Read IDbContextFactory usage example: filesystem MCP read Services/DeliveryService.cs

BEFORE WRITING ANY RAZOR PAGE:
- Read _Imports.razor first
- Read NavMenu.razor if adding navigation
- Read an existing similar page for patterns: filesystem MCP read Components/Pages/Deliveries.razor

BUILD COMMAND (always use full path):
cd "/Users/deividasru/Projects/ERP DEV/NordicBeesERP" && dotnet build

AFTER CREATING ANY SERVICE:
- Read Program.cs with filesystem MCP
- Add registration: builder.Services.AddScoped<IXxxService, XxxService>();
- For IHostedService: builder.Services.AddHostedService<XxxWorker>();

DBCONTEXT FILE:
- Path: Data/NordicBeesERPContext.cs
- Always read full file before adding DbSet
- Add DbSet<ModelName> ModelNames { get; set; }

DB TABLE NAMES - CRITICAL:
- Suppliers table = business_partners (NOT suppliers)
- Settings table = app_settings (key/value: setting_key, setting_value)
- supplier_id in expense_invoices → references business_partners.id
- Expense supplier filter: WHERE partner_type = 'expense_supplier'
- DbContext file: Data/NordicBeesERPContext.cs
- Build command: cd "/Users/deividasru/Projects/ERP DEV/NordicBeesERP" && dotnet build

BEFORE WRITING ANY SERVICE:
- Read existing service for patterns: filesystem MCP read Services/DeliveryService.cs
- Read model file before writing service

BEFORE WRITING ANY RAZOR PAGE:
- Read _Imports.razor first
- Read NavMenu.razor if adding navigation
- Read existing page for patterns: filesystem MCP read Components/Pages/Deliveries.razor

AFTER CREATING ANY SERVICE:
- Read Program.cs and add registration
- builder.Services.AddScoped<IXxxService, XxxService>();
- For IHostedService: builder.Services.AddHostedService<XxxWorker>();

CONTEXT LIMIT RULES:
- NEVER read more than 2 files per task
- NEVER read files larger than 200 lines at once
- Read only the specific file you are editing
- Do NOT scan project structure unless explicitly asked
- Do NOT read _Imports.razor, Program.cs unless task requires it

LOOP PREVENTION:
- If a command returns empty output, it means SUCCESS - stop and report done
- Empty grep output = 0 errors found = build passed
- Never repeat the same command more than once
- If unsure about result, use attempt_completion immediately
