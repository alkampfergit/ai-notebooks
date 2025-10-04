---
id: "02000-01000"
level: "lesson"
title: "Chunking Goals & Trade-offs"
parent: "02000"
---

# Chunking Goals & Trade-offs

- Define what makes a good chunk for retrieval and downstream generation.
- Trade-offs between chunk size, overlap, and semantic coherence.

## Details

This lesson frames the problem: what are chunking objectives (recall, precision, latency, cost, coherence) and how they conflict. We'll cover:

- Retrieval signal vs. context length: when larger chunks help or hurt.
- Overlap heuristics: why overlap improves recall and how much is too much.
- Domain considerations: technical vs. conversational corpora, code, tables.
- Cost and latency implications for indexing and query-time retrieval.

Practice: analyze a short document and propose 3 chunking configurations with expected pros/cons.