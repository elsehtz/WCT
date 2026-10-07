#!/usr/bin/env python3
"""Builds Data/Reference/wc2026-reference.json from Wikipedia (CC BY-SA 4.0).

Supplies what ESPN's feed lacks or gets wrong for the 2026 World Cup: pre-tournament FIFA
ranking (April 2026 release), confederation, head coach, and each nation's registered
26-man squad (shirt number, position, date of birth, club at the time of the tournament).

    python3 tools/build_reference.py            # fetches the two articles
    python3 tools/build_reference.py squads.wiki wc.wiki   # or use saved wikitext
"""
import json, re, sys, urllib.request
from pathlib import Path

SQUADS = "2026_FIFA_World_Cup_squads"
MAIN = "2026_FIFA_World_Cup"

# Wikipedia section title -> FIFA/ESPN code.
CODES = {
    "Algeria": "ALG", "Argentina": "ARG", "Australia": "AUS", "Austria": "AUT", "Belgium": "BEL",
    "Bosnia and Herzegovina": "BIH", "Brazil": "BRA", "Canada": "CAN", "Cape Verde": "CPV",
    "Colombia": "COL", "Croatia": "CRO", "Curaçao": "CUW", "Czech Republic": "CZE", "DR Congo": "COD",
    "Ecuador": "ECU", "Egypt": "EGY", "England": "ENG", "France": "FRA", "Germany": "GER", "Ghana": "GHA",
    "Haiti": "HAI", "Iran": "IRN", "Iraq": "IRQ", "Ivory Coast": "CIV", "Japan": "JPN", "Jordan": "JOR",
    "Mexico": "MEX", "Morocco": "MAR", "Netherlands": "NED", "New Zealand": "NZL", "Norway": "NOR",
    "Panama": "PAN", "Paraguay": "PAR", "Portugal": "POR", "Qatar": "QAT", "Saudi Arabia": "KSA",
    "Scotland": "SCO", "Senegal": "SEN", "South Africa": "RSA", "South Korea": "KOR", "Spain": "ESP",
    "Sweden": "SWE", "Switzerland": "SUI", "Tunisia": "TUN", "Turkey": "TUR", "United States": "USA",
    "Uruguay": "URU", "Uzbekistan": "UZB",
}
# Flag codes used for foreign coaches -> nationality (a coach without a flag is a native).
NATIONS = {
    "ARG": "Argentina", "AUS": "Australia", "BEL": "Belgium", "BIH": "Bosnia-Herzegovina",
    "ENG": "England", "ESP": "Spain", "FRA": "France", "GER": "Germany", "GRE": "Greece",
    "ITA": "Italy", "MAR": "Morocco", "NED": "Netherlands", "POR": "Portugal", "USA": "United States",
}


def fetch(title):
    url = f"https://en.wikipedia.org/w/index.php?title={title}&action=raw"
    req = urllib.request.Request(url, headers={"User-Agent": "WorldCupTerminal-reference/1.0"})
    return urllib.request.urlopen(req, timeout=60).read().decode("utf-8")


def unlink(s):
    s = re.sub(r"\[\[(?:[^\]|]*\|)?([^\]]*)\]\]", r"\1", s)
    return re.sub(r"<!--.*?-->", "", s).strip()


def parse_ranks(main):
    """'* {{#invoke:flag|fb|ARG}} (1)' lines grouped under '''[[...|CONMEBOL]]''' headers."""
    out, conf = {}, None
    start = main.index("final positions in the FIFA Men's World Ranking before the tournament")
    for line in main[start:].splitlines():
        if line.startswith("{{col-end}}"):
            break
        h = re.match(r"'''\[\[[^|\]]*\|([A-Z]+)\]\]'''", line)
        if h:
            conf = h.group(1)
            continue
        m = re.match(r"\* \{\{#invoke:flag\|fb\|([A-Z]{3})\}\}.*?\((\d+)", line)
        if m:
            out[m.group(1)] = {"fifaRank": int(m.group(2)), "confederation": conf}
    return out


def parse_coach(line):
    """Last-listed coach wins (e.g. Tunisia changed coach after match one)."""
    line = re.sub(r"<!--.*?-->", "", line)
    picks = re.findall(r"(?:\{\{#invoke:flag\|icon\|([A-Z]{3})\}\}\s*)?\[\[(?:[^\]|]*\|)?([^\]]*)\]\]", line)
    nat, name = picks[-1]
    note = None
    if len(picks) > 1:
        note = f"replaced {picks[0][1]} after the opening match"
    return name, (NATIONS[nat] if nat else None), note


def parse_player(line):
    dob = re.search(r"birth date and age2\|(?:df=y\|)?\d+\|\d+\|\d+\|(\d+)\|(\d+)\|(\d+)", line)
    body = re.sub(r"\{\{birth date and age2[^}]*\}\}", "", line)
    body = unlink(body)
    params = dict(p.split("=", 1) for p in body.strip("{}").split("|") if "=" in p)
    return {
        "number": int(params["no"]) if params.get("no", "").isdigit() else 0,
        "pos": params.get("pos", ""),
        "name": params.get("name", ""),
        "dob": f"{int(dob[1]):04d}-{int(dob[2]):02d}-{int(dob[3]):02d}" if dob else None,
        "club": params.get("club", ""),
        "caps": int(params["caps"]) if params.get("caps", "").isdigit() else None,
        "captain": "captain" in params.get("other", "").lower(),
    }


def parse_squads(squads):
    teams, cur = {}, None
    for line in squads.splitlines():
        h = re.match(r"^===([^=].*?)===\s*$", line)
        if h:
            cur = CODES.get(h.group(1).strip())
            if cur:
                teams[cur] = {"wikiName": h.group(1).strip(), "players": []}
            continue
        if cur is None:
            continue
        if line.startswith("Coach:"):
            name, nat, note = parse_coach(line)
            teams[cur].update(coach=name, coachNationality=nat, coachNote=note)
        elif line.startswith("{{nat fs g player"):
            teams[cur]["players"].append(parse_player(line))
        elif line.startswith("==") and not line.startswith("==="):
            cur = None
    return teams


def main():
    squads = Path(sys.argv[1]).read_text() if len(sys.argv) > 2 else fetch(SQUADS)
    main_txt = Path(sys.argv[2]).read_text() if len(sys.argv) > 2 else fetch(MAIN)
    ranks, teams = parse_ranks(main_txt), parse_squads(squads)
    for code, t in teams.items():
        t.update(ranks.get(code, {}))
    missing = sorted(set(CODES.values()) - teams.keys())
    assert not missing, f"teams missing: {missing}"
    for code, t in teams.items():
        assert len(t["players"]) == 26 and "coach" in t and "fifaRank" in t, code
    out = {
        "source": "Wikipedia: '2026 FIFA World Cup squads' and '2026 FIFA World Cup' (CC BY-SA 4.0)",
        "rankingRelease": "FIFA/Coca-Cola Men's World Ranking, April 2026 (last before the tournament)",
        "teams": dict(sorted(teams.items())),
    }
    dest = Path(__file__).resolve().parent.parent / "Data" / "Reference" / "wc2026-reference.json"
    dest.write_text(json.dumps(out, ensure_ascii=False, indent=1) + "\n")
    print(f"wrote {dest} ({len(teams)} teams)")


if __name__ == "__main__":
    main()
