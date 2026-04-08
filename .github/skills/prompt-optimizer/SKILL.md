# Skill: Prompt Optimizer

Use this skill only when the incoming prompt contains `optimize prompt first` (case-insensitive).

## Goal
Convert raw user input into an execution-ready internal brief that improves precision and delivery speed while preserving intent.

## Workflow
1. Normalize the request.
- Remove filler.
- Resolve obvious shorthand.
- Extract explicit deliverables.

2. Lock constraints.
- Technical stack and architecture limits.
- Safety limits (non-destructive operations, validation requirements).
- Output expectations (code changes, tests, docs, summary).

3. Infer minimal assumptions.
- Fill small gaps using repository context.
- Keep assumptions narrow and reversible.

4. Produce internal execution brief.
- Objective (one sentence)
- Scope in / out
- Concrete tasks
- Acceptance checks
- Risks and mitigations

5. Execute from brief.
- Start implementation immediately unless high-risk ambiguity exists.

6. Show optimized prompt.
- Include a visible `Optimized Prompt` section in output.
- Keep it concise and faithful to original intent and requested scope.

## Guardrails
- Never change user intent.
- Never add major requirements not requested.
- Never block on questions when a safe assumption is possible.
- Ask for clarification only when choices have non-obvious consequences.
