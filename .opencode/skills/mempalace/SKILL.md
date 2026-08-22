---
name: mempalace
description: Long-term project memory search for NordicBeesERP, powered by MemPalace (local, no API key). Use this at the start of any investigation or fix to check whether something similar was already discussed, decided, or fixed before — before asking the user or re-deriving from scratch. Also use when the user asks "did we already look at this" or "what did we decide about X".
---

# MemPalace — NordicBeesERP Long-Term Memory

MemPalace indexes this project's code, docs, and past session history into
a searchable local memory ("palace"), organized as Wings → Rooms → Drawers.
This project's wing is `nordicbeeserp`, with rooms: `frontend`, `backend`,
`data`, `migrations`, `docs`, `helpers`, `general`.

**New code and fixes get added to the palace automatically** after every
`fixer` task completes (via an automated hook) — you don't need to
manually re-mine after implementing something. This skill is about
*searching* what's already there, not about adding to it yourself.

## When to search before starting work

- Before implementing a fix, search for whether this exact issue or a
  very similar one was already investigated/decided/fixed in a past
  session — re-doing already-settled work wastes time and can silently
  contradict an earlier decision.
- When the user references something vague ("that thing we discussed",
  "the bug from before") — search first rather than asking them to
  re-explain, if a quick search might resolve it.
- When you're about to make an architectural choice (e.g. which pattern
  to follow, which table a field should belong to) — check if this was
  already decided and documented in a past session.

## How to search

If the `mempalace_search` MCP tool is available, use it directly:
`mempalace_search(query, wing="nordicbeeserp", room=<optional>)`

Prefer scoping to a room when you have a good guess (e.g. `backend` for
Service/Model questions, `migrations` for schema history, `docs` for
spec/plan questions) — an unscoped search across the whole wing returns
noisier results.

If MCP isn't available, fall back to the CLI:
`mempalace search "query" --wing nordicbeeserp`

## Presenting results

- Always cite the room/drawer source for anything you found — don't
  present it as if you already knew it.
- If results are ambiguous or seem unrelated to the current task, say so
  rather than forcing a fit — a bad memory match is worse than no match.
- If nothing relevant comes back, just say so and proceed with the task
  normally — this is a supplement to investigation, not a requirement
  that blocks work if it comes up empty.

## Known caveat

This project's palace was mined in bulk once with fairly broad
auto-detected room definitions — some rooms (e.g. `migrations`) may
contain more files than you'd expect, because rooms are assigned by
content/keyword matching, not just folder location. Treat a single
match with some skepticism if it seems oddly categorized; corroborate
with an actual file read before treating it as settled fact.
