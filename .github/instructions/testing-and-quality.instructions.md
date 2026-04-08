---
description: Test and quality expectations for this repository
applyTo: "tests/WindowsUtilityPack.Tests/**/*.cs,src/WindowsUtilityPack/**/*.cs"
---

# Testing and Quality Instructions

- Add tests for all new service logic and validation rules.
- Test invalid inputs, cancellation, and error paths.
- Use clear test names and keep Arrange/Act/Assert structure readable.
- Keep tests independent from local machine state where possible.
- If a change is hard to test, create a seam instead of skipping tests.
- Avoid reducing current test coverage in touched areas.
