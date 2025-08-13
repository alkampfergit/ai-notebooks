---
id: "01000-001-030"
level: "slide"
title: "Extraction = Quality Lever"
parent: "01000-001"
background-image: 'img/search.png'
background-image-transparency: 50
---

# Extraction Responsibilities

- Coverage
- Fidelity
- Granularity
- Metadata Richness

## Details
Define each lever with a short diagnostic question:
- Coverage: Are all authoritative sources represented? (Gap → hallucination risk)
- Fidelity: Did we preserve meaning, structure, provenance? (Loss → citation failure)
- Granularity: Are chunks sized + bounded to balance recall vs precision? (Too big → noise; too small → fragmentation)
- Metadata Richness: Do we power filters, boosts, reranking, freshness tracking? (Sparse metadata → weak retrieval)
Explain these as design tradeoffs the team can tune intentionally instead of leaving accidental.
