# Contributing

This repo publishes NuGet packages from Git tags via GitHub Actions. To keep GitHub Releases readable, prefer working through PRs (even when working solo).

## Workflow (recommended)

1. Create a branch:

```powershell
git checkout master
git pull
git checkout -b feat/short-topic
```

2. Commit changes (small and focused).

3. Push the branch and open a PR into `master`:

```powershell
git push -u origin feat/short-topic
```

4. Add labels to the PR (used for release notes):
   - area: `area:client` / `area:server` / `area:exporter`
   - type: `enhancement` / `bug` / `breaking` / `documentation` / `chore` / `dependencies`
   - optional: `skip-changelog`

5. Merge when CI is green.

## Releases

- Create a tag `vX.Y.Z` (for example `v1.0.0`). The `Release NuGet` workflow will build, test, pack, create a GitHub Release, and push packages.
- Release notes are auto-generated and categorized using `.github/release.yml`, based on PR labels.
