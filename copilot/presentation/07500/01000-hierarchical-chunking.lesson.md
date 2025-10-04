---
id: "07500-01000"
level: "lesson"
title: "Hierarchical & Parent-Child Chunking"
parent: "07500"
---

# Hierarchical & Parent-Child Chunking

- Create small retrieval-friendly child chunks while linking them to larger parent contexts for richer LLM prompting.

## Details

Cover design patterns and trade-offs:
- When to split into child chunks and when to retain a parent document.
- Linking strategies: parent_id fields, rank-aggregation, and context assembly at query time.
- Use cases: long technical manuals, legal documents, multi-section reports.
- Retrieval-time assembly strategies: fetch children + parent, or fetch parent only when necessary.

Exercise: design a parent-child scheme for a large handbook and sketch retrieval-time assembly logic.