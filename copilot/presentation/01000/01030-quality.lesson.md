---
id: "01000-004"
level: "lesson"
title: "Quality Dimensions & KPIs"
parent: "01000"
---

# Quality Dimensions & KPIs

Define measurable levers for extraction excellence.

## Details

### Core Dimensions
1. Coverage – % of prioritized source surface area represented as retrievable chunks.
2. Granularity – Chunk size distribution aligned with retrieval token budgets.
3. Fidelity – Preservation of semantic structure & meaning (headings, lists, tables).
4. Freshness – Age since last successful extraction vs. SLA.
5. Metadata Richness – Presence of required fields enabling filters / boosting.
6. Provenance Integrity – Traceability from chunk back to original source & version.
7. Noise Ratio – Irrelevant / boilerplate tokens vs. useful content tokens.
8. Cost Efficiency – Compute / storage per effective chunk.

### Example KPIs
| Dimension | KPI | Target (illustrative) |
| --------- | --- | --------------------- |
| Coverage | % critical docs ingested | > 98% |
| Granularity | Median tokens per chunk | 180–250 |
| Freshness | Avg age (critical sources) | < 24h |
| Metadata | % chunks with full schema | > 99% |
| Provenance | % chunks with resolvable lineage | 100% |
| Noise | Boilerplate token share | < 5% |
| Cost | $ / 1k retrievable tokens | Trend ↓ |

### Measurement Tooling
- Inventory registry (source list + priority + SLA).
- Extraction job logs + metrics pipeline (e.g. Prometheus / OpenTelemetry tags).
- Evaluation harness: sample queries → retrieval recall vs. gold set.
- Schema validator for chunk records.

### Trade‑offs
- Smaller chunks improve precision but may degrade context coherence.
- Rich metadata increases storage & embedding cost but boosts retrieval accuracy.

### Dashboards
Surface: coverage trend, freshness heatmap, chunk size histogram, metadata completeness gauge, cost per source.

### Improvement Loop
1. Instrument → 2. Benchmark → 3. Identify gap → 4. Prioritize fix → 5. Deploy → 6. Re‑measure.

### Exercise Prompt
Pick a dimension and propose a feasible automated metric collection approach.

### Completion Signal
Learner can articulate at least 5 dimensions & propose a KPI each.

### Next
Proceed to the Chunking Strategies module for implementation depth.
