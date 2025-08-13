---
id: "01000-001"
level: "lesson"
title: "RAG Architecture at 10,000 ft"
parent: "01000"
---

# RAG Architecture at 10,000 ft

A concise mental model of the end‑to‑end Retrieval‑Augmented Generation lifecycle.

## Details

### Purpose
Establish shared vocabulary and a system view before diving into extraction specifics. Learners should be able to sketch the pipeline and explain responsibilities of each stage.

### High-Level Stages
1. Authoring / Source Creation – Knowledge lives in documents, DB rows, APIs, applications.
2. Extraction – Acquire, parse, structure, enrich raw source material (focus of this course).
3. Indexing – Persist chunks + metadata into search substrates (vector, sparse, hybrid, graph).
4. Retrieval – Select candidate chunks for a user/query task (semantic, lexical, rerankers).
5. Augmented Generation – Compose prompt/context → model → draft answer.
6. Post‑Processing & Feedback – Validation, citation, evaluation, trace logging; feed signals back.

### Core Roles of Extraction in the Pipeline
- Coverage: ensure relevant source surface area is represented.
- Fidelity: preserve meaning, structure, provenance.
- Granularity: right chunk size & boundaries for retrieval precision/recall balance.
- Metadata Richness: contextual fields powering filters, boosting, reranking.

### Common Misconceptions
- "RAG = just add a vector DB" – ignores upstream quality levers.
- "Bigger context window obviates chunking" – still need selective retrieval & metadata.

### Minimal Diagram (verbal)
Sources → (Extract/Normalize/Enrich) → Chunk Store(s) → Retrieval (k, rerank) → LLM → Output + Feedback → Continuous Improvement.

### Failure Modes Traceable to Extraction
- Hallucinations from missing authoritative sources (coverage gap).
- Irrelevant passages retrieved due to noisy or oversized chunks (granularity issue).
- Lost citations because provenance fields dropped (metadata loss).
- Stale answers from unrefreshed incremental extraction (freshness lapse).

### Key Terms Introduced
RAG, Extraction, Chunk, Metadata, Provenance, Hybrid Retrieval, Reranker, Feedback Loop.

### Quick Self‑Check
Can you list 4 levers extraction controls that influence downstream answer quality?

### Next
We zoom into feedback loop placement and how extraction participates.
