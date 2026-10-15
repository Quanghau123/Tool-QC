# Agent instructions

Additional repository guidance lives in [`CLAUDE.md`](CLAUDE.md). Read that file
completely before inspecting, changing, generating, or running tests in this
repository.


## BA status

- 2026-10-11: Người dùng xác nhận dự án không có bộ tài liệu BA riêng.
- 2026-10-13: Người dùng xác nhận `C:\Users\quanghau\Downloads\Campaign (1).xlsx` là nguồn chuẩn để đối chiếu bộ testcase Campaign workbook-generated; xem [docs/BA/README.md](docs/BA/README.md).

## Testcase naming and ordering

- Name every newly authored testcase in `cases[].name` as `Testcase X: N`, where `N` is a clear Vietnamese testcase title. Calculate `X` separately for each testcase directory as one greater than the larger of the number of existing case objects in that directory and the highest `X` already used there. Count case objects across JSON files directly in that directory. Do not renumber existing cases when adding a new one.
- Keep testcase files alphabetized by title `N`: name each new JSON file from `N` without a numeric prefix, so sorting filenames A–Z follows the testcase title. If one JSON file contains multiple case objects, keep those entries alphabetized by `N` as well. Do not rename existing files solely to apply this rule.

