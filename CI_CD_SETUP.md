# CI/CD Pipeline Setup for IPTVGuideDog

## Overview

This repository uses GitHub Actions to automatically run tests on every code change. The CI/CD pipeline ensures code quality by:

1. **Building the solution** on every push and pull request
2. **Running all unit tests** including the GroupsCommandTests
3. **Publishing test results** in a readable format
4. **Tracking code coverage** (optional)

## Workflows

### 1. .NET Tests (`.github/workflows/dotnet.yml`)

**When it runs:**
- On every push to `main` branch
- On every pull request to `main` branch

**What it does:**
- Sets up .NET 10 environment
- Restores dependencies
- Builds the solution in Release mode
- Runs all tests
- Publishes test results as artifacts
- Displays test results in the PR check

### 2. Code Coverage (`.github/workflows/coverage.yml`)

**When it runs:**
- On every push to `main` branch
- On every pull request to `main` branch

**What it does:**
- Runs tests with code coverage collection
- Uploads coverage reports to Codecov (optional service)
- Tracks coverage trends over time

## Branch Protection Rules (Recommended)

To enforce these checks before merging, configure branch protection on `main`:

### Steps:

1. Go to your GitHub repository
2. Click **Settings** ? **Branches**
3. Click **Add rule** under "Branch protection rules"
4. Enter `main` as the branch name pattern
5. Enable:
   - ? **Require a pull request before merging**
   - ? **Require status checks to pass before merging**
   - ? **Require branches to be up to date before merging**

### Select required status checks:

- ? `build-and-test` - The main test job
- ? `coverage` - Code coverage analysis

### Additional protections (optional):

- ? **Dismiss stale pull request approvals when new commits are pushed**
- ? **Require code reviews before merging**
- ? **Restrict who can push to matching branches** (optional, for admins only)

## Best Practices

### 1. **Always Use Pull Requests**
Never push directly to `main`. Always create a PR and let the CI/CD pipeline validate your changes first.

### 2. **Watch the Test Results**
Each PR will show test results inline. Click on "Details" to see full logs if a check fails.

### 3. **Fix Failing Tests Immediately**
If CI/CD catches a test failure:
- Don't merge the PR
- Fix the issue locally
- Push the fix to your branch
- The CI/CD will automatically re-run

### 4. **Keep Tests Fast**
The unit tests included (GroupsCommandTests, GroupsFileValidatorTests) are designed to be fast. Keep test suites lean.

### 5. **Use Meaningful Commit Messages**
This helps track which changes introduced test failures:
```
Good: "Fix GroupsCommand backup logic when no new groups are added"
Poor: "fix stuff"
```

## Monitoring & Troubleshooting

### View Workflow Runs

1. Go to your repository
2. Click **Actions** tab
3. Select a workflow to see runs
4. Click on a run to see detailed logs

### Common Issues

**Tests fail locally but pass in CI:**
- Ensure you're using the same .NET version (10.x)
- Check for environment-specific issues
- Verify all dependencies are installed: `dotnet restore`

**Tests pass locally but fail in CI:**
- Run tests with: `dotnet test --configuration Release`
- Check for timing-sensitive tests
- Verify file system operations work on Linux (CI uses ubuntu-latest)

**Workflow doesn't trigger:**
- Ensure you pushed to `main` or created a PR to `main`
- Check the workflow YAML syntax (GitHub will show errors)
- Verify the workflow file is in `.github/workflows/` directory

## Example: Working with PRs

```bash
# 1. Create a feature branch
git checkout -b feature/new-feature

# 2. Make changes and commit
git add .
git commit -m "Add new feature"

# 3. Push to GitHub
git push origin feature/new-feature

# 4. Create a Pull Request on GitHub
# - Go to https://github.com/jake1164/IPTVGuideDog
# - Click "Compare & pull request"
# - Add description
# - Wait for CI/CD checks to complete

# 5. If tests fail:
# - Fix the issue locally
# - Commit and push
# - CI/CD automatically re-runs

# 6. Once all checks pass:
# - Request review
# - After approval, merge the PR
```

## Advanced: Customizing Workflows

To modify the workflows (e.g., change .NET version, add more test projects):

1. Edit `.github/workflows/dotnet.yml` or `.github/workflows/coverage.yml`
2. Make changes
3. Commit and push to any branch
4. GitHub will validate the workflow syntax
5. Once merged to `main`, the updated workflow applies

### Common customizations:

**Run on more branches:**
```yaml
on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]
```

**Add specific test project:**
```yaml
- name: Run specific tests
  run: dotnet test tests/IPTVGuideDog.Cli.Tests/IPTVGuideDog.Cli.Tests.csproj
```

**Only run on certain paths:**
```yaml
on:
  push:
    paths:
      - 'src/**'
      - 'tests/**'
      - '.github/workflows/**'
```

## References

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [.NET GitHub Actions](https://github.com/actions/setup-dotnet)
- [EnricoMi Test Results Action](https://github.com/EnricoMi/publish-unit-test-result-action)
- [Codecov Integration](https://codecov.io/)
