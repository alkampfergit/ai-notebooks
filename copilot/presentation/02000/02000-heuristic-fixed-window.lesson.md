---
id: "02000-02000"
level: "lesson"
title: "Heuristic & Fixed Window Methods"
parent: "02000"
---

# Heuristic & Fixed Window Methods

- Simple, predictable chunking strategies: fixed window, sliding window, token-based windows.

## Details

Covers mechanical chunkers that don't rely on semantics. Topics:

- Fixed-size windows (bytes/tokens/characters) and sliding windows with overlap.
- Heuristics to align chunks to sentence or paragraph boundaries.
- Tokenization considerations across languages and encoders.
- Pros/cons: speed, reproducibility, and failure modes (cuts in the middle of semantic units).

Exercise: implement a sliding-window chunker and measure average chunk coherence on a sample corpus.