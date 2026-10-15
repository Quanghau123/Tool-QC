# BA Source Register

## Confirmed sources

| Path | Format | Authority | Confirmation date | SHA-256 | Last verified |
|---|---|---|---|---|---|
| `C:\Users\quanghau\Downloads\Campaign (1).xlsx` | Excel workbook (`.xlsx`) | Authoritative comparison source for the generated Campaign `VERIFY` testcases in this task; not a broader project BA pack | 2026-10-13 | `608D78161D0199BB1C187A805C0795368821E83399446C71195EEDAA0561ED07` | 2026-10-13 |

The workbook remains at its original location and is not copied or modified.
Verify its SHA-256 before each task that uses it as a source. Stop and request
reconfirmation if the hash differs from the registered value.

## Decisions

| Date | Source / section | Conflict | User decision | Affected behavior |
|---|---|---|---|---|
| 2026-10-13 | `Campaign (1).xlsx`, Campaign `VERIFY` workbook | The project has no separate BA document set, while this workbook is the source used to generate the requested testcase suite. | User confirmed this workbook is authoritative for this comparison. | Use this workbook as the comparison baseline for `projects/be-booking/testcases/workbook-generated/`; do not generalize its authority to unrelated project behavior. |