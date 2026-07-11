#!/usr/bin/env python3
import shutil, datetime, os

path = os.path.expanduser("~/.config/kilo/kilo.jsonc")
ts = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
backup = f"{path}.bak_{ts}"
shutil.copy2(path, backup)
print(f"Backup: {backup}")

with open(path, "r", encoding="utf-8") as f:
    content = f.read()

old_block = '''    "webfetch": "allow",
    "doom_loop": "allow",
    "notebook_read": {
      "Migrations/20260602150000_InitialCreate.cs": "allow",
      "/Users/deividasru/Projects/NordicBeesERP/Models/Printing": "deny"
    },
    "notebook_edit": {
      "Models/Printing/PrintingEnums.cs": "allow",
      "Models/WarehouseModule/WeighingStation.cs": "allow",
      "/Users/deividasru/Projects/NordicBeesERP/Models/Printing/PrintJobEnums.cs": "allow",
      "Models/Printing/ContainerWeightCorrection.cs": "deny"
    }
  },'''

new_block = '''    "webfetch": "allow",
    "doom_loop": "allow"
  },'''

if old_block not in content:
    print("NEPAVYKO: tikslus blokas nerastas — failas jau pasikeitęs. NIEKO NEKEIČIU. Patikrink rankiniu būdu.")
else:
    content = content.replace(old_block, new_block)

    old_exp = '''  "experimental": {
    "codebase_search": true,
    "native_notebook_tools": true
  },'''
    new_exp = '''  "experimental": {
    "codebase_search": true,
    "native_notebook_tools": false
  },'''
    content = content.replace(old_exp, new_exp)

    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
    print("Pataisyta: notebook_read/notebook_edit blokai pašalinti, native_notebook_tools -> false")
