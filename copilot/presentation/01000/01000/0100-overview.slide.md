---
id: "01000-001-010"
level: "slide"
title: "RAG at 10,000 ft"
parent: "01000-001"
background-color: 'orange'
---

# RAG Architecture: Mental Model

Why RAG? Structured path from raw knowledge → trustworthy answers.

- Sources → Extraction → Index → Retrieval → Generation → Feedback
- Extraction quality amplifies (or bottlenecks) every downstream stage
- Shared vocabulary before we go deep

## Details
Set the framing: learners should internalize the pipeline as a living system, not a black box around a vector store. Stress that today we focus on Extraction because it is the controllable leverage point for coverage, fidelity, granularity, and metadata richness. This slide anchors everything else.

Key message: RAG is a loop, not a line — feedback hardens earlier stages over time.
