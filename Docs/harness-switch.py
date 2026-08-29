#!/usr/bin/env python3
"""
harness-switch.py — paprastas meniu OpenCode harness profiliui perjungti.

Ką daro:
  1. Parodo sąrašą projektų (NordicBeesERP / Deltamark eshop).
  2. Parodo sąrašą profilių (nuskaito ~/.opencode-profiles/ aplanko turinį).
  3. Nustato OPENCODE_CONFIG_DIR TIK šiam paleidimui (nepalieka pėdsako
     jūsų terminale po to, kai OpenCode užsidaro).
  4. Paleidžia `opencode` pasirinktame projekto kataloge.
  5. Papildoma parinktis — patikrinti dabartinę modelių/GPU būseną
     neperjungiant nieko.

Naudojimas:
  python3 harness-switch.py

Prieš pirmą kartą naudojant, pakeiskite žemiau esančias konstantas
(PROJECTS, SSH_HOST) pagal savo realią aplinką.
"""

import os
import subprocess
import sys
from pathlib import Path

# ---------------------------------------------------------------------------
# NUSTATYMAI — pritaikykite savo aplinkai
# ---------------------------------------------------------------------------

PROFILES_DIR = Path.home() / ".opencode-profiles"

PROJECTS = {
    "1": ("NordicBeesERP", Path.home() / "Projects" / "NordicBeesERP"),
    "2": ("Deltamark eshop", Path.home() / "Projects" / "Deltamark eshop"),
}

# local-llm mašina (per Tailscale) — patikslinkite tikrą SSH vartotojo vardą,
# jei "asus" neteisingas. Jei nenorite SSH patikrinimo, palikite tuščią "".
SSH_HOST = "asus@100.110.26.80"

# Portai, kuriuos tikrina "Tikrinti būseną" — pakoreguokite pagal savo
# realų llama-swap-config.yaml išdėstymą.
PORTS = {
    8086: "coder / merged",
    8087: "fixer",
    8088: "reviewer",
    8089: "vl-ocr",
}
LLM_HOST = "100.110.26.80"

# ---------------------------------------------------------------------------


def list_profiles() -> list[str]:
    if not PROFILES_DIR.exists():
        return []
    return sorted(p.name for p in PROFILES_DIR.iterdir() if p.is_dir())


def check_status() -> None:
    print("\n--- Modelių būsena (llama-swap) ---")
    for port, label in PORTS.items():
        try:
            result = subprocess.run(
                ["curl", "-s", "-m", "3", f"http://{LLM_HOST}:{port}/v1/models"],
                capture_output=True,
                text=True,
            )
            status = "atsako" if '"id"' in result.stdout else "neatsako / tuščia"
        except Exception as exc:  # noqa: BLE001
            status = f"klaida ({exc})"
        print(f"  :{port} ({label}) — {status}")

    if SSH_HOST:
        print("\n--- GPU būsena (nvidia-smi per SSH) ---")
        try:
            result = subprocess.run(
                [
                    "ssh",
                    "-o", "ConnectTimeout=5",
                    SSH_HOST,
                    "nvidia-smi --query-gpu=index,memory.used,memory.total,utilization.gpu --format=csv",
                ],
                capture_output=True,
                text=True,
                timeout=10,
            )
            output = result.stdout.strip() or result.stderr.strip()
            print(output if output else "  (tuščias atsakymas)")
        except Exception as exc:  # noqa: BLE001
            print(f"  Nepavyko prisijungti per SSH ({SSH_HOST}): {exc}")
    else:
        print("\n(SSH_HOST nenustatytas — GPU būsena netikrinama.)")


def choose_project() -> tuple[str, Path] | None:
    print("\nProjektas:")
    for key, (name, _path) in PROJECTS.items():
        print(f"  {key}) {name}")
    print("  q) atšaukti")
    choice = input("> ").strip().lower()
    if choice == "q":
        return None
    if choice not in PROJECTS:
        print("Neteisingas pasirinkimas.")
        return choose_project()
    return PROJECTS[choice]


def choose_profile(profiles: list[str]) -> str | None:
    print("\nProfilis:")
    for i, name in enumerate(profiles, start=1):
        print(f"  {i}) {name}")
    print("  s) tik patikrinti dabartinę būseną (nieko neperjungti)")
    print("  q) atšaukti")
    choice = input("> ").strip().lower()
    if choice == "q":
        return None
    if choice == "s":
        return "__status__"
    try:
        idx = int(choice)
        if 1 <= idx <= len(profiles):
            return profiles[idx - 1]
    except ValueError:
        pass
    print("Neteisingas pasirinkimas.")
    return choose_profile(profiles)


def launch(profile_name: str, project_name: str, project_path: Path) -> None:
    profile_path = PROFILES_DIR / profile_name

    if not project_path.exists():
        print(f"\nKLAIDA: projekto katalogas nerastas: {project_path}")
        return

    print(f"\nProfilis:  {profile_name}")
    print(f"Projektas: {project_name} ({project_path})")
    print(f"OPENCODE_CONFIG_DIR = {profile_path}")
    print(
        "\nPaleidžiama 'opencode'. Pirmas atsakymas gali užtrukti ilgiau "
        "įprasto — modeliai persikrauna GPU serverio pusėje. Tai normalu.\n"
    )

    env = os.environ.copy()
    env["OPENCODE_CONFIG_DIR"] = str(profile_path)

    try:
        subprocess.run(["opencode"], cwd=str(project_path), env=env)
    except FileNotFoundError:
        print("\nKLAIDA: komanda 'opencode' nerasta PATH. Patikrinkite diegimą.")
    except KeyboardInterrupt:
        pass

    print("\nOpenCode sesija baigta.")


def main() -> None:
    profiles = list_profiles()
    if not profiles:
        print(f"Nerasta jokių profilių aplanke: {PROFILES_DIR}")
        print("Sukurkite bent vieną poaplankį su opencode.json prieš naudojant šį skriptą.")
        sys.exit(1)

    while True:
        print("\n========================================")
        print(" NordicBeesERP / Deltamark harness meniu")
        print("========================================")

        project = choose_project()
        if project is None:
            print("Baigiama.")
            return
        project_name, project_path = project

        profile = choose_profile(profiles)
        if profile is None:
            continue
        if profile == "__status__":
            check_status()
            input("\nPaspauskite Enter, kad grįžtumėte į meniu...")
            continue

        launch(profile, project_name, project_path)

        again = input("\nGrįžti į meniu? (t/n): ").strip().lower()
        if again != "t":
            print("Baigiama.")
            return


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nBaigiama.")
