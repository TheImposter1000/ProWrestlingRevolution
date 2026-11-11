#!/usr/bin/env python3
"""Generate PRW editor label metadata from the native schema, not TEW data."""
from pathlib import Path
import csv
import re

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
SCHEMA = (ROOT / "PrwSchema.cs").read_text(encoding="utf-8")
OUT = HERE / "manifest"
OUT.mkdir(exist_ok=True)

pattern = re.compile(
    r'new\("(?P<table>tbl[A-Za-z0-9_]+)",\s*'
    r'\[(?P<keys>[^\]]*)\],\s*\[(?P<cols>[^\]]*)\]\s*\)',
    re.DOTALL,
)
tables = []
for match in pattern.finditer(SCHEMA):
    table = match.group("table")
    keys = set(re.findall(r'"([^"]+)"', match.group("keys")))
    fields = re.findall(r'"([^"]+)"', match.group("cols"))
    tables.append((table, keys, fields))

labels = {
    "tblWorker": ("Workers", "Worker identity and general information"),
    "tblWorkerBio": ("Worker Biographies", "Long-form worker biographies"),
    "tblWorkerBusiness": ("Worker Business", "Owner and booker information"),
    "tblWorkerOver": ("Worker Popularity", "Popularity by region"),
    "tblWorkerPhysical": ("Worker Physical", "Physical condition details"),
    "tblWorkerSkills": ("Worker Skills", "Skills and potential"),
    "tblContract": ("Contracts", "Employment and contract terms"),
    "tblContractBio": ("Contract Biographies", "Contract biography text"),
    "tblWorkerAttributes": ("Worker Attributes", "Worker attributes and flags"),
    "tblAlternative": ("Alternative Identities", "Alternative worker identities"),
    "tblFed": ("Companies", "Wrestling companies and federation data"),
    "tblMatch": ("Match Types", "Match definitions and rules"),
    "tblMatchRingSetup": ("Match Ring Setups", "Match ring setup options"),
    "tblMatchFinishes": ("Match Finishes", "Allowed match finishes"),
}
actual = {table for table, _, _ in tables}
if len(tables) != 14 or set(labels) != actual:
    raise RuntimeError(f"Expected 14 native tables, found: {sorted(actual)}")
if len({(table, col) for table, _, fields in tables for col in fields}) != sum(
    len(fields) for _, _, fields in tables
):
    raise RuntimeError("Duplicate editor field keys")

def nice(field):
    value = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", field.replace("_", " "))
    return value.replace("UID", "ID").replace("Vs", "vs")

def write(name, header, rows):
    with (OUT / name).open("w", encoding="utf-8", newline="") as out:
        writer = csv.writer(out, delimiter="\t", lineterminator="\n")
        writer.writerow(header)
        writer.writerows(rows)

write("categories.tsv",
      ["TableName", "DisplayName", "DisplayOrder", "IsVisible", "Description"],
      [(table, labels[table][0], index + 1, "true", labels[table][1])
       for index, (table, _, _) in enumerate(tables)])
write("groups.tsv",
      ["TableName", "GroupName", "DisplayName", "DisplayOrder", "IsVisible"],
      [(table, "General", labels[table][0], 1, "true") for table, _, _ in tables])
write("fields.tsv",
      ["TableName", "FieldName", "DisplayLabel", "DisplayOrder",
       "IsVisible", "IsReadOnly", "GroupName"],
      [(table, field, nice(field), index + 1, "true",
        str(field in keys).lower(), "General")
       for table, keys, fields in tables for index, field in enumerate(fields)])
write("settings.tsv", ["SettingKey", "SettingValue"], [
    ("SchemaVersion", "1"),
    ("Purpose", "Pro Wrestling Revolution database editor metadata"),
    ("Source", "PRW native schema; TEW9Access structural reference only"),
])
print("Editor metadata:", len(tables), "categories;",
      sum(len(fields) for _, _, fields in tables), "fields")
