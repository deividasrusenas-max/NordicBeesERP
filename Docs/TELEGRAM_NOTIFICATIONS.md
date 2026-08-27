# Telegram notifications

Generic Telegram Bot API notifications for NordicBeesERP. No `Telegram.Bot` NuGet package is used — notifications are sent via a raw `HttpClient` POST to `https://api.telegram.org/bot{ BotToken }/sendMessage` (HTML parse mode). Failures are swallowed (logged as warning only) and never block the underlying operation.

## Group keys

Configuration lives under the `Telegram` section of `appsettings.json` (overridable per environment — see below). The `Groups` dictionary maps a group key to a chat id:

| Group key | Purpose | Status |
|-----------|---------|--------|
| `sandelis` | Warehouse / sandėlis | Reserved — config placeholder only, no code calls yet |
| `uzsakymai` | Orders / užsakymai | Wired — 3 triggers (see below) |
| `gamyba` | Production / gamyba | Reserved — config placeholder only, no code calls yet |

Group keys are lowercase, no diacritics, to avoid `appsettings.json` key-casing issues.

## uzsakymai triggers (wired)

Implemented in `Services/OrderService.cs` via `TelegramNotificationService.SendToGroupAsync("uzsakymai", message)`:

1. **New order created** — `CreateOrderAsync`, after the order row is committed. Message: order number + customer id.
2. **Order packed** — `MarkReadyForPickupCheckAsync`, when all order lines are packed (status transition to `ready_for_pickup`). Message: order number.
3. **Order closed/delivered WITHOUT an invoice** — after the status transitions to `shipped` in BOTH `MarkShippedAsync` and `CreateShipmentAsync`. Sends only when `orders.invoice_id IS NULL` (checked via `SqlQueryRaw<int?>` immediately after the status UPDATE). Message: order number, noting no invoice was issued.

> **Trigger 3 is wired** in both `shipped`-transition methods. Each reads `invoice_id` and fires to `uzsakymai` only when it is null. This is a best-effort, fire-and-forget notification (`_ = _telegram.SendToGroupAsync(...)`) and never blocks or rolls back the shipment.

## Configuring real values

Do NOT put real tokens/chat ids in `appsettings.json` (committed). Populate per environment:

**Dev (user-secrets):**
```
dotnet user-secrets set "Telegram:BotToken" "<token>"
dotnet user-secrets set "Telegram:Groups:uzsakymai" "<chatId>"
dotnet user-secrets set "Telegram:Groups:sandelis" "<chatId>"
dotnet user-secrets set "Telegram:Groups:gamyba" "<chatId>"
```

**Staging / Prod (environment variables — ASP.NET Core `:` → `__` binding):**
```
Telegram__BotToken=<token>
Telegram__Groups__uzsakymai=<chatId>
Telegram__Groups__sandelis=<chatId>
Telegram__Groups__gamyba=<chatId>
```

`TelegramNotificationService` silently skips sending when `BotToken` is empty, the group key is missing, or the chat id is `0`.
