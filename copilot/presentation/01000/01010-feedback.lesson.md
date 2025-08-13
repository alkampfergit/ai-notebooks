---
id: "01000-002"
level: "lesson"
title: "Where Extraction Fits & Feedback Loops"
parent: "01000"
---

# Where Extraction Fits & Feedback Loops

Understand bidirectional flows: extraction supplies retrieval; feedback refines extraction.

## Details

### Learning Goal
Reveal that extraction is not a one‑off ETL but an adaptive subsystem responsive to retrieval & generation telemetry.

### Feedback Loop Types
1. Retrieval Gaps: Queries with low answer confidence or fallback to model guessing → signal missing sources or chunk under-segmentation.
2. Hallucination Audits: Unsupported claims → highlight provenance gaps or metadata insufficiency.
3. Click / Usage Analytics: Low usage of certain source-derived chunks → potential redundancy or over-representation.
4. Freshness Signals: User complaints about outdated info → drive incremental or CDC jobs.
5. Evaluation Harness: Synthetic or curated Q/A benchmarks measuring recall & grounding.

### Telemetry Captured
- Query → retrieved chunk IDs + scores.
- Chunk provenance (source, version, timestamp, section path).
- Generation metrics: answer length, citation density, grounding score.
- Human review labels (correct / partially / hallucinated).

### Adaptive Responses
| Signal | Extraction Adjustment |
| ------ | --------------------- |
| Low recall | Expand sources, refine parsing, increase chunk overlap |
| High latency | Precompute embeddings, reduce chunk size variance |
| Hallucinations | Enrich metadata, improve provenance capture, add disambiguating context |
| Staleness | Trigger incremental extraction, tighten freshness SLA |

### Governance Cadence
- Daily: freshness & error rate dashboards.
- Weekly: recall / precision evaluation runs.
- Monthly: source inventory review & backlog grooming.

### Anti‑Pattern
Treating extraction code as "set & forget"; leads to silent model drift manifestations.

### Key Takeaway
Feedback loops transform extraction into a living product function rather than a pipeline chore.

### Exercise Prompt
List 3 telemetry fields you would log to enable adaptive extraction decisions.

### Transition
Next we catalog source types to anticipate tailoring needs.
