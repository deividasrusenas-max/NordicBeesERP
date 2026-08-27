# Telegram notifications

Generic Telegram Bot API notifications for NordicBeesERP. No `Telegram.Bot` NuGet package is used — notifications are sent via a raw `HttpClient` POST to `https://api.telegram.org/bot{ BotToken }/sendMessage` (HTML parse mode). Failures are swallowed (logged as warning only) and never block the underlying operation.

## Group keys

Configuration lives under the `Telegram` section of `appsettings.json` (overridable per environment — see below). The `Groups` dictionary maps a group key to a chat id:

| Group key | Purpose | Status |
|-----------|---------|--------|
| `sandelis` | Warehouse / sandėlis | Reserved — config placeholder only, no code calls yet |
| `uzsakymai` | Orders / užsakymai | Wired — 2 triggers (see below) |
| `gamyba` | Production / gamyba | Reserved — config placeholder only, no code calls yet |

Group keys are lowercase, no diacritics, to avoid `appsettings.json` key-casing issues.

## uzsakymai triggers (wired)

Implemented in `Services/OrderService.cs` via `TelegramNotificationService.SendToGroupAsync("uzsakymai", message)`:

1. **New order created** — `CreateOrderAsync`, after the order row is committed. Message: order number + customer id.
2. **Order packed** — `MarkReadyForPickupCheckAsync`, when all order lines are packed (status transition to `ready_for_pickup`). Message: order number.

> ⚠️ **Trigger 3 (order closed/delivered WITHOUT an invoice) is NOT yet wired.** The `shipped` status is set in two methods (`MarkShippedAsync` and `CreateShipmentAsync`), so the exact wiring point is ambiguous. This is pending a decision — see the task handoff. When wired, the invoice-presence check is `orders.invoice_id IS NULL` (same pattern as `Order.IsUninvoiced` / `GetUninvoicedShippedOrdersAsync`).

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
