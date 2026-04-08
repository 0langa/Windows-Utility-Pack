---
description: Global prompt normalization and enhancement rules for coding agents
applyTo: "**/*"
---

# Prompt Optimization Instructions

- Treat prompt optimization as optional and trigger-based.
- Only run optimization when the original user prompt contains the phrase `optimize prompt first` (case-insensitive).
- Build an internal execution brief with:
  - objective
  - constraints
  - inferred assumptions
  - acceptance checks
- Preserve intent and scope; do not expand scope unless user asks.
- If the request is underspecified, make minimal safe assumptions and proceed.
- Escalate only when a wrong assumption could cause destructive or high-risk outcomes.
- Apply `.github/skills/prompt-optimizer/SKILL.md` workflow.
- When optimization is triggered, include an `Optimized Prompt` section in output so the user can see the transformed prompt.
