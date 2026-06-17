# Get Started: Share This Repo To Organization

Use this guide to help a colleague move this repository into an organization repository without transferring ownership.

## What You Need To Share

Send your colleague these details:

1. Source repository URL:
   - https://github.com/karsamrat03/ASPX_LOCDS_LegacyApp.git
2. Source branch:
   - main
3. Target organization repository URL:
   - https://github.com/<org>/<repo>.git

## Steps For Your Colleague (Preserves Full History)

### 1. Clone your source repo

```bash
git clone https://github.com/karsamrat03/ASPX_LOCDS_LegacyApp.git
cd ASPX_LOCDS_LegacyApp
```

### 2. Add org repo as another remote

```bash
git remote add org https://github.com/<org>/<repo>.git
git remote -v
```

Expected: both origin and org remotes are listed.

### 3. Push main branch to org repo

```bash
git push -u org main
```

### 4. Push tags (if any)

```bash
git push org --tags
```

### 5. Verify in GitHub

1. Open the org repository in browser.
2. Confirm the main branch exists.
3. Confirm commit history is visible.

## If Push Is Rejected (non-fast-forward)

This means the org repo already has commits.

Option A (recommended): merge/rebase with org history, then push.

```bash
git fetch org
git pull --rebase org main
git push -u org main
```

Option B (only with org admin approval): overwrite org main.

```bash
git push -u org main --force
```

## Alternative: GitHub Import (No Local Git Needed)

1. In GitHub org, create/import repository.
2. Choose Import Repository.
3. Source URL: https://github.com/karsamrat03/ASPX_LOCDS_LegacyApp.git
4. Start import.

## Permissions Needed

Your colleague needs permission in the organization to:

1. Create or write to the target repository.
2. Push to main (or create PR if branch protection is enabled).

## Quick Message You Can Forward

Hi, please migrate this repo into org:

1. git clone https://github.com/karsamrat03/ASPX_LOCDS_LegacyApp.git
2. cd ASPX_LOCDS_LegacyApp
3. git remote add org https://github.com/<org>/<repo>.git
4. git push -u org main
5. git push org --tags

If push is rejected due to existing commits in org repo, coordinate before using force push.
