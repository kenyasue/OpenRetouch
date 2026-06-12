# Repository Structure Document

## Project Structure

```
project-root/
├── src/                   # Source code
│   ├── [layer1]/          # [Description]
│   ├── [layer2]/          # [Description]
│   └── [layer3]/          # [Description]
├── tests/                 # Test code
│   ├── unit/              # Unit tests
│   ├── integration/       # Integration tests
│   └── e2e/               # E2E tests
├── docs/                  # Project documentation
├── config/                # Configuration files
└── scripts/               # Build and deployment scripts
```

## Directory Details

### src/ (Source Code Directory)

#### [Directory 1]

**Role**: [Description]

**Files placed here**:
- [File pattern 1]: [Description]
- [File pattern 2]: [Description]

**Naming conventions**:
- [Rule 1]
- [Rule 2]

**Dependencies**:
- May depend on: [Directory names]
- Must not depend on: [Directory names]

**Example**:
```
[directory-name]/
├── [example-file1].ts
└── [example-file2].ts
```

#### [Directory 2]

**Role**: [Description]

**Files placed here**:
- [File pattern 1]: [Description]

**Naming conventions**:
- [Rule 1]

**Dependencies**:
- May depend on: [Directory names]
- Must not depend on: [Directory names]

### tests/ (Test Directory)

#### unit/

**Role**: Location for unit tests

**Structure**:
```
tests/unit/
└── src/                    # Same structure as the src directory
    └── [layer]/
        └── [filename].test.ts
```

**Naming conventions**:
- Pattern: `[name of file under test].test.ts`
- Example: `TaskService.ts` → `TaskService.test.ts`

#### integration/

**Role**: Location for integration tests

**Structure**:
```
tests/integration/
└── [feature]/              # Directories split by feature
    └── [scenario].test.ts
```

#### e2e/

**Role**: Location for E2E tests

**Structure**:
```
tests/e2e/
└── [user-scenario]/        # Per user scenario
    └── [flow].test.ts
```

### docs/ (Documentation Directory)

**Documents placed here**:
- `product-requirements.md`: Product Requirements Document
- `functional-design.md`: Functional Design Document
- `architecture.md`: Architecture Design Document
- `repository-structure.md`: Repository Structure Document (this document)
- `development-guidelines.md`: Development Guidelines
- `glossary.md`: Glossary

### config/ (Configuration Directory - if applicable)

**Files placed here**:
- Configuration files
- Constant definition files

**Example**:
```
config/
├── default.ts
└── constants.ts
```

### scripts/ (Scripts Directory - if applicable)

**Files placed here**:
- Build scripts
- Development helper scripts

## File Placement Rules

### Source Files

| File Type | Location | Naming Convention | Example |
|------------|--------|---------|-----|
| [Type 1] | [Directory] | [Rule] | [Example] |
| [Type 2] | [Directory] | [Rule] | [Example] |

### Test Files

| Test Type | Location | Naming Convention | Example |
|-----------|--------|---------|-----|
| Unit tests | tests/unit/ | [target].test.ts | TaskService.test.ts |
| Integration tests | tests/integration/ | [feature].test.ts | task-crud.test.ts |
| E2E tests | tests/e2e/ | [scenario].test.ts | user-workflow.test.ts |

### Configuration Files

| File Type | Location | Naming Convention |
|------------|--------|---------|
| Environment configuration | config/environments/ | [environment-name].ts |
| Tool configuration | Project root | [tool-name].config.js |
| Type definitions | src/types/ | [target].d.ts |

## Naming Conventions

### Directory Names

- **Layer directories**: plural, kebab-case
  - Examples: `services/`, `repositories/`, `controllers/`
- **Feature directories**: singular, kebab-case
  - Examples: `task-management/`, `user-authentication/`

### File Names

- **Class files**: PascalCase
  - Examples: `TaskService.ts`, `UserRepository.ts`
- **Function files**: camelCase
  - Examples: `formatDate.ts`, `validateEmail.ts`
- **Constant files**: UPPER_SNAKE_CASE
  - Examples: `API_ENDPOINTS.ts`, `ERROR_MESSAGES.ts`

### Test File Names

- Pattern: `[target].test.ts` or `[target].spec.ts`
- Examples: `TaskService.test.ts`, `formatDate.spec.ts`

## Dependency Rules

### Dependencies Between Layers

```
UI layer
    ↓ (OK)
Service layer
    ↓ (OK)
Data layer
```

**Forbidden dependencies**:
- Data layer → Service layer (❌)
- Data layer → UI layer (❌)
- Service layer → UI layer (❌)

### Dependencies Between Modules

**Circular dependencies are forbidden**:
```typescript
// ❌ Bad example: circular dependency
// fileA.ts
import { funcB } from './fileB';

// fileB.ts
import { funcA } from './fileA';  // Circular dependency
```

**Solution**:
```typescript
// ✅ Good example: extract a shared module
// shared.ts
export interface SharedType { /* ... */ }

// fileA.ts
import { SharedType } from './shared';

// fileB.ts
import { SharedType } from './shared';
```

## Scaling Strategy

### Adding Features

Placement policy when adding new features:

1. **Small feature**: place in an existing directory
2. **Medium feature**: create a subdirectory within the layer
3. **Large feature**: separate into an independent module

**Example**:
```
src/
├── services/
│   ├── TaskService.ts           # Existing feature
│   └── task-management/         # Medium feature split out
│       ├── TaskService.ts
│       ├── SubtaskService.ts
│       └── TaskCategoryService.ts
```

### Managing File Size

**File splitting guidelines**:
- Per file: 300 lines or fewer recommended
- 300-500 lines: consider refactoring
- 500+ lines: splitting strongly recommended

**How to split**:
```typescript
// Bad example: all functionality in one file
// TaskService.ts (800 lines)

// Good example: split by responsibility
// TaskService.ts (200 lines) - CRUD operations
// TaskValidationService.ts (150 lines) - Validation
// TaskNotificationService.ts (100 lines) - Notification handling
```

## Special Directories

### .steering/ (Steering Files)

**Role**: Defines "what to do this time" for a specific piece of development work

**Structure**:
```
.steering/
└── [YYYYMMDD]-[task-name]/
    ├── requirements.md      # Requirements for this piece of work
    ├── design.md            # Design of the changes
    └── tasklist.md          # Task list
```

**Naming convention**: `20250115-add-user-profile` format

### .claude/ (Claude Code Configuration)

**Role**: Claude Code configuration and customization

**Structure**:
```
.claude/
├── commands/                # Slash commands
├── skills/                  # Skills per task mode
└── agents/                  # Subagent definitions
```

## Exclusion Settings

### .gitignore

Files the project should exclude:
- `node_modules/`
- `dist/`
- `.env`
- `.steering/` (temporary files for task management)
- `*.log`
- `.DS_Store`

### .prettierignore, .eslintignore

Files tools should exclude:
- `dist/`
- `node_modules/`
- `.steering/`
- `coverage/`
