---
id: "07500-04000"
level: "lesson"
title: "Multi-Representation Pipelines"
parent: "07500"
---

# Multi-Representation Pipelines

- Index and serve multiple representations of content (full text vectors, summary vectors, sparse lexical signals, metadata) and aggregate results.

## Details

Cover architectures and orchestration:
- How to store and query dense + sparse vectors together (hybrid search).
- Ranking fusion strategies: score normalization, cascade ranking, rerankers.
- Cost-performance trade-offs: storage, query latency, and update complexity.
- Monitoring and feedback loops: how to track which representation contributed to successful answers.

Practical: design an index schema that stores 3 representations per chunk and a simple cascade retrieval flow that uses cheap signals first.