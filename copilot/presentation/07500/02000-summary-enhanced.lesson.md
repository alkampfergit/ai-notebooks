---
id: "07500-02000"
level: "lesson"
title: "Summary-Enhanced Chunking"
parent: "07500"
---

# Summary-Enhanced Chunking

- Use concise summaries to produce denser embedding signals or to index alongside full text.

## Details

Topics:
- Methods for generating chunk summaries (abstractive vs extractive).
- Indexing strategies: summary-only, summary+text, or summary as an additional vector.
- Advantages: lower-cost embeddings, noise reduction, better match for intent queries.
- Pitfalls: summary drift, loss of fine-grained facts—when to fall back to full text.

Lab: create summaries for sample chunks, embed summaries and measure retrieval hit-rate vs full-text embeddings.