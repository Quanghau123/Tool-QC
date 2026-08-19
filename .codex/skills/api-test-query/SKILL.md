---
name: api-test-query
description: Design list, search, filter, sort, pagination, aggregate, empty-data, and cross-scope query API tests. Use for Tool-QC endpoints returning collections or paged response envelopes.
---

# API Query Testing

## Required coverage

- Empty result, one item, multiple items, and data from another scope.
- Search every supported field, case/diacritic behavior where defined, no match, and
  special characters.
- Every filter member plus invalid enum/value and combined filters.
- Ascending and descending sort; verify newest/oldest ordering and deterministic ties
  when material.
- First, middle, last, and out-of-range pages; page size one and normal page size.
- Assert `totalCount`, `pageSize`, `current`, `totalPages`, `hasNext`, `hasPrevious`.
- Assert `moreInfo`/aggregates over the entire filtered dataset, not only current page.
- Confirm search/filter/sort/page composition and no cross-event/tenant/resource leakage.

Assert the important fields of every representative row, not only HTTP status or count.
