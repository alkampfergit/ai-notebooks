---
id: "02000-05000"
level: "lesson"
title: "Hybrid & Adaptive Pipelines"
parent: "02000"
---

# Hybrid & Adaptive Pipelines

- Combine structural, heuristic, and semantic signals to adapt chunking to document type.

## Details

Cover patterns for mixing strategies:

- Rule-based pre-processing (preserve tables/headings), followed by semantic splitting for long paragraphs.
- Adaptive window sizes based on local token density or content type (code vs prose).
- Online chunking: indexing time vs. query-time re-chunking.
- Monitoring and feedback loops to tune chunking heuristics.

Exercise: design a hybrid pipeline for mixed HTML + transcripts corpus.