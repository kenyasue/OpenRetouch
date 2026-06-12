# Process Guide

## Basic Principles

### 1. Include Plenty of Concrete Examples

Present concrete code examples, not just abstract rules.

**Bad example**:
```
Variable names should be easy to understand
```

**Good example**:
```typescript
// ✅ Good example: roles are clear
const userAuthentication = new UserAuthenticationService();
const taskRepository = new TaskRepository();

// ❌ Bad example: vague
const auth = new Service();
const repo = new Repository();
```

### 2. Explain the Reasons

Make "why we do it this way" explicit.

**Example**:
```
## Do not ignore errors

Reason: Ignoring errors makes it difficult to investigate the root cause of problems.
Handle expected errors appropriately, and propagate unexpected errors upward
so they can be recorded in logs.
```

### 3. Set Measurable Criteria

Avoid vague wording and provide concrete numbers.

**Bad example**:
```
Keep code coverage high
```

**Good example**:
```
Code coverage targets:
- Unit tests: 80% or higher
- Integration tests: 60% or higher
- E2E tests: 100% of critical flows
```

## Git Workflow Rules

### Branching Strategy (Git Flow)

**What is Git Flow**:
A branching model proposed by Vincent Driessen that systematically manages feature development, releases, and hotfixes. Clear role separation enables parallel work in team development and stable releases.

**Branch layout**:
```
main (production environment)
└── develop (development/integration environment)
    ├── feature/* (new feature development)
    ├── fix/* (bug fixes)
    └── release/* (release preparation) *as needed
```

**Operating rules**:
- **main**: Holds only stable code that has been released to production. Manage versions with tags
- **develop**: Integrates the latest development code for the next release. Run automated tests in CI
- **feature/\*, fix/\***: Branch off from develop; merge back into develop via PR when work is complete
- **No direct commits**: Require PR review on all branches to ensure code quality
- **Merge policy**: squash merge for feature→develop; merge commit recommended for develop→main

**Benefits of Git Flow**:
- Branch roles are clear, making parallel development by multiple people easier
- The production environment (main) is always kept clean
- Urgent issues can be handled quickly with a hotfix branch (introduce as needed)

### Commit Message Conventions

**Conventional Commits recommended**:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Type list**:
```
feat: new feature (minor version up)
fix: bug fix (patch version up)
docs: documentation
style: formatting (no effect on code behavior)
refactor: refactoring
perf: performance improvement
test: adding or fixing tests
build: build system
ci: CI/CD configuration
chore: other (dependency updates, etc.)

BREAKING CHANGE: breaking change (major version up)
```

**Example of a good commit message**:

```
feat(task): add priority setting feature

Users can now set a priority (high/medium/low) on tasks.

Implementation details:
- Added a priority field to the Task model
- Added a --priority option to the CLI
- Implemented sorting by priority

Breaking changes:
- The structure of the Task type has changed
- Existing task data requires migration

Closes #123
BREAKING CHANGE: Added required priority field to the Task type
```

### Pull Request Template

**An effective PR template**:

```markdown
## Type of Change
- [ ] New feature (feat)
- [ ] Bug fix (fix)
- [ ] Refactoring (refactor)
- [ ] Documentation (docs)
- [ ] Other (chore)

## Changes
### What was changed
[Concise description]

### Why it was changed
[Background / reason]

### How it was changed
- [Change 1]
- [Change 2]

## Testing
### Tests performed
- [ ] Unit tests added
- [ ] Integration tests added
- [ ] Manual testing performed

### Test results
[Description of test results]

## Related Issues
Closes #[number]
Refs #[number]

## Review Points
[What you especially want reviewers to look at]
```

## Test Strategy

### Test Pyramid

```
       /\
      /E2E\       Few (slow, high cost)
     /------\
    /Integra-\     Some
   /----tion--\
  /   Unit     \   Many (fast, low cost)
 /--------------\
```

**Target ratio**:
- Unit tests: 70%
- Integration tests: 20%
- E2E tests: 10%

### How to Write Tests

**Given-When-Then pattern**:

```typescript
describe('TaskService', () => {
  describe('task creation', () => {
    it('creates a task when the data is valid', async () => {
      // Given: setup
      const service = new TaskService(mockRepository);
      const validData = { title: 'Test' };

      // When: execute
      const result = await service.create(validData);

      // Then: verify
      expect(result.id).toBeDefined();
      expect(result.title).toBe('Test');
    });

    it('throws ValidationError when the title is empty', async () => {
      // Given: setup
      const service = new TaskService(mockRepository);
      const invalidData = { title: '' };

      // When/Then: execute and verify
      await expect(
        service.create(invalidData)
      ).rejects.toThrow(ValidationError);
    });
  });
});
```

### Coverage Targets

**Measurable targets**:

```json
// jest.config.js
{
  "coverageThreshold": {
    "global": {
      "branches": 80,
      "functions": 80,
      "lines": 80,
      "statements": 80
    },
    "./src/services/": {
      "branches": 90,
      "functions": 90,
      "lines": 90,
      "statements": 90
    }
  }
}
```

**Reasons**:
- Require high coverage for important business logic (services/)
- Lower coverage is acceptable for the UI layer
- Do not aim for 100% (balance cost against benefit)

## Code Review Process

### Purpose of Reviews

1. **Quality assurance**: Early detection of bugs
2. **Knowledge sharing**: The whole team understands the codebase
3. **Learning opportunity**: Sharing best practices

### Keys to Effective Reviews

**For reviewers**:

1. **Constructive feedback**
```markdown
## ❌ Bad example
This code is bad.

## ✅ Good example
This implementation has O(n²) time complexity.
Using a Map can improve it to O(n):

```typescript
const taskMap = new Map(tasks.map(t => [t.id, t]));
const result = ids.map(id => taskMap.get(id));
```
```

2. **Make priorities explicit**
```markdown
[Required] Security: passwords are being written to logs
[Recommended] Performance: avoid DB calls inside loops
[Suggestion] Readability: could this function name be clearer?
[Question] Could you explain the intent of this logic?
```

3. **Give positive feedback too**
```markdown
✨ This implementation is easy to understand!
👍 Edge cases are thoroughly considered
💡 This pattern could be useful elsewhere
```

**For reviewees**:

1. **Do a self-review**
   - Review your own code before creating the PR
   - Add comments where explanation is needed

2. **Keep PRs small**
   - 1 PR = 1 feature
   - Changed files: 10 or fewer recommended
   - Changed lines: 300 or fewer recommended

3. **Explain thoroughly**
   - Why you chose this implementation
   - Alternatives you considered
   - Points you especially want reviewed

### Review Time Guidelines

- Small PR (100 lines or fewer): 15 minutes
- Medium PR (100-300 lines): 30 minutes
- Large PR (300+ lines): 1 hour or more

**Principle**: Avoid large PRs; split them up

## Promoting Automation (if applicable)

### Automating Quality Checks

**Automation items and adopted tools**:

1. **Lint checks**
   - **ESLint 9.x** + **@typescript-eslint**
     - Unify coding standards with a TypeScript-specific rule set
     - Automatically detect potential bugs and deprecated patterns
     - Configuration file: `eslint.config.js` (Flat Config format)

2. **Code formatting**
   - **Prettier 3.x**
     - Automatically format code style and reduce debate during reviews
     - Use alongside ESLint, avoiding conflicts with `eslint-config-prettier`
     - Configuration file: `.prettierrc`

3. **Type checking**
   - **TypeScript Compiler (tsc) 5.x**
     - Check only type errors with `tsc --noEmit`
     - Verify type safety independently of the build
     - Configuration file: `tsconfig.json`

4. **Test execution**
   - **Vitest 2.x**
     - Vite-based for fast startup and execution
     - Native TypeScript/ESM support, works with zero configuration
     - Coverage measurement (@vitest/coverage-v8) included out of the box
     - Modern development experience with HMR support

5. **Build verification**
   - **TypeScript Compiler (tsc)**
     - Guarantee a type-checked build with the standard compiler
     - Simple setup with no additional tools required
     - Manage output settings centrally in `tsconfig.json`

**Implementation**:

**1. CI/CD (GitHub Actions)**
```yaml
# .github/workflows/ci.yml
name: CI
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '24'
      - run: npm ci
      - run: npm run lint
      - run: npm run typecheck
      - run: npm run test
      - run: npm run build
```

**2. Pre-commit hooks (Husky 9.x + lint-staged)**
```json
// package.json
{
  "scripts": {
    "prepare": "husky",
    "lint": "eslint .",
    "format": "prettier --write .",
    "typecheck": "tsc --noEmit",
    "test": "vitest run",
    "build": "tsc"
  },
  "lint-staged": {
    "*.{ts,tsx}": [
      "eslint --fix",
      "prettier --write"
    ]
  }
}
```
```bash
# .husky/pre-commit
npm run lint-staged
npm run typecheck
```

**Adoption benefits**:
- Automatic checks run before commits, preventing defective code from being introduced
- CI runs automatically when a PR is created, ensuring quality before merge
- Early detection reduces fix costs by up to 80% (compared with bugs found after production release)

**Why this setup was chosen**:
- A standard, modern setup in the TypeScript ecosystem as of 2025
- High compatibility between tools with few configuration conflicts
- An excellent balance between developer experience and execution speed

## Checklist

- [ ] A branching strategy has been decided
- [ ] Commit message conventions are clear
- [ ] A PR template is provided
- [ ] Test types and coverage targets are set
- [ ] A code review process is defined
- [ ] A CI/CD pipeline is in place
