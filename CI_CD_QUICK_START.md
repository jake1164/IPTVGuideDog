# Quick Setup Checklist

## ? Step 1: Push the Workflow Files

The workflow files have been created in:
- `.github/workflows/dotnet.yml` - Main build and test workflow
- `.github/workflows/coverage.yml` - Code coverage workflow

Push these to your repository:
```bash
git add .github/
git add CI_CD_SETUP.md
git commit -m "Add GitHub Actions CI/CD workflows"
git push origin main
```

## ? Step 2: Configure Branch Protection (Recommended)

This prevents merging PRs if tests fail:

1. Go to: https://github.com/jake1164/IPTVGuideDog/settings/branches
2. Click "Add rule"
3. Enter `main` as pattern
4. Enable:
   - ? Require a pull request before merging
   - ? Require status checks to pass before merging
     - Select: `build-and-test`
     - Select: `coverage`

## ? Step 3: Test It Out

Create a test PR:

```bash
# Create a test branch
git checkout -b test/ci-workflow

# Make a small change (or just touch a file)
echo "# CI/CD Test" >> TEST_CI.md

# Commit and push
git add TEST_CI.md
git commit -m "Test CI/CD workflow"
git push origin test/ci-workflow
```

Then:
1. Go to GitHub
2. Click "Compare & pull request"
3. Watch the Actions tab as it runs tests
4. You should see a green checkmark when tests pass
5. Close the PR without merging

## ?? Monitoring Your Tests

### Where to find test results:

1. **In Pull Requests:**
   - Test results appear directly in the PR
   - Click "Details" to see full logs

2. **In Actions Tab:**
   - https://github.com/jake1164/IPTVGuideDog/actions
   - Shows all workflow runs
   - Green ? = passed
   - Red ? = failed
   - Yellow ? = in progress

3. **In Commits:**
   - Green checkmark next to commit hash = all tests passed
   - Red X = tests failed

## ?? Best Practices Going Forward

### ? DO:
- Always create a pull request (never push directly to main)
- Review the CI/CD results before requesting approval
- Fix failing tests immediately
- Write descriptive commit messages
- Keep tests focused and fast

### ? DON'T:
- Push directly to main
- Force push to main
- Ignore failing tests in CI/CD
- Merge PRs with failing checks
- Skip the PR review process

## ?? Troubleshooting

**Q: Workflow not triggering?**
A: 
- Verify `.github/workflows/dotnet.yml` exists
- Check file syntax (GitHub will show errors)
- Wait a moment; sometimes GitHub takes time to register new workflows

**Q: Tests pass locally but fail in CI?**
A:
- Run: `dotnet build --configuration Release`
- Run: `dotnet test --configuration Release`
- Check for environment-specific code (Windows vs Linux)

**Q: Tests fail in CI but pass locally?**
A:
- CI runs on Linux (ubuntu-latest)
- Your machine might be Windows
- Check file path handling (use `Path.Combine`)
- Ensure no Windows-specific dependencies

**Q: How do I see detailed test output?**
A:
- Go to Actions tab
- Click the failed workflow run
- Click the "build-and-test" job
- Scroll to see test output
- Look for the "Run tests" step for details

## ?? Getting Help

For GitHub Actions documentation:
- https://docs.github.com/en/actions
- https://docs.github.com/en/actions/quickstart

For .NET testing:
- https://learn.microsoft.com/en-us/dotnet/core/testing/
- https://learn.microsoft.com/en-us/visualstudio/test/unit-test-basics

## Summary

You now have:
? Automated testing on every code change
? Test results visible in PRs
? Protection against broken code merging to main
? Coverage tracking
? A CI/CD foundation for future enhancements

Your tests (GroupsCommandTests, GroupsFileValidatorTests) will now run automatically on every PR and push to main!
