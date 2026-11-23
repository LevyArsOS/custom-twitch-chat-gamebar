# Contributing

1. Open or pick an issue; every change belongs to one.
2. Agree on the design in the issue before coding (maintainers keep the detailed specification
   outside this repository).
3. Branch from `develop` as `feature/<TASK>/<slug>` (`bugfix/` for fixes; `hotfix/` from `main`).
4. Commits: [Conventional Commits](https://www.conventionalcommits.org/), English, signed,
   at most 200 changed lines each.
5. Run `scripts/check` (tests and coverage) before pushing; CI runs it again.
6. Open the pull request against `develop` using the template.
