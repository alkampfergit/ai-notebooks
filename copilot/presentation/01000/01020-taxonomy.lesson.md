---
id: "01000-003"
level: "lesson"
title: "Taxonomy of Source Types"
parent: "01000"
---

# Taxonomy of Source Types

Survey heterogeneous knowledge reservoirs requiring distinct extraction tactics.

## Details

### Dimensions for Classification
- Structure: Unstructured (PDF scan) → Semi (HTML, Markdown) → Structured (DB tables, APIs schemas).
- Volatility: Static manuals vs. fast‑changing tickets / product catalogs.
- Access Pattern: Batch filesystem, streaming events, paginated API, CDC feed.
- Latency Sensitivity: Near‑real‑time (incident data) vs. archival (policies).
- Compliance Sensitivity: PII / regulated vs. public.

### Major Classes
1. Documents (PDF, DOCX, HTML, Markdown)
2. Repositories / Code (optional extension)
3. Databases (SQL, NoSQL, time‑series)
4. APIs & SaaS (REST, GraphQL, webhooks)
5. Application UIs / RPA (last resort scraping)
6. Logs / Events (append‑only streams)

### Selection Criteria for MVP
- Business value density.
- Extraction feasibility (tools maturity, authentication simplicity).
- Update cadence vs. freshness SLA.
- Risk (PII exposure, licensing constraints).

### Tailoring Considerations
| Source | Challenges | Key Metadata |
| ------ | ---------- | ------------ |
| PDF | Layout / OCR | page, bbox, section path |
| HTML | Boilerplate noise | DOM path, role, link href |
| SQL DB | Normalization fragmentation | table, pk, snapshot ts |
| API | Rate limits, pagination | endpoint, query params, version |
| SaaS | Proprietary schemas | object type, tenant, last_modified |

### Provenance Strategy
Always capture: source_id, source_type, version/hash, extraction_timestamp, lineage_path.

### Exercise Prompt
Rank 5 potential sources for a hypothetical support assistant and justify order.

### Transition
We now define quality dimensions guiding decisions across these sources.
