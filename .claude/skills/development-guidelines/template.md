# Development Guidelines

## Coding Standards

### Naming Conventions

#### Variables and Functions

**TypeScript/JavaScript**:
```typescript
// ✅ Good example
const userProfileData = fetchUserProfile();
function calculateTotalPrice(items: CartItem[]): number { }

// ❌ Bad example
const data = fetch();
function calc(arr: any[]): number { }
```

**Principles**:
- Variables: camelCase, nouns or noun phrases
- Functions: camelCase, start with a verb
- Constants: UPPER_SNAKE_CASE
- Booleans: start with `is`, `has`, or `should`

#### Classes and Interfaces

```typescript
// Classes: PascalCase, nouns
class TaskManager { }
class UserAuthenticationService { }

// Interfaces: PascalCase, with or without an I prefix
interface ITaskRepository { }
interface Task { }

// Type aliases: PascalCase
type TaskStatus = 'todo' | 'in_progress' | 'completed';
```

### Code Formatting

**Indentation**: [2 spaces / 4 spaces / tabs]

**Line length**: maximum [80/100/120] characters

**Example**:
```typescript
// [Language] code formatting example
[Code example]
```

### Comment Conventions

**Function and class documentation**:
```typescript
/**
 * Calculates the total number of tasks
 *
 * @param tasks - Array of tasks to count
 * @param filter - Filter criteria (optional)
 * @returns Total number of tasks
 * @throws {ValidationError} If the task array is invalid
 */
function countTasks(
  tasks: Task[],
  filter?: TaskFilter
): number {
  // Implementation
}
```

**Inline comments**:
```typescript
// ✅ Good example: explains why
// Invalidate the cache to fetch the latest data
cache.clear();

// ❌ Bad example: states what is happening (obvious from the code)
// Clear the cache
cache.clear();
```

### Error Handling

**Principles**:
- Expected errors: define appropriate error classes
- Unexpected errors: propagate upward
- Never ignore errors

**Example**:
```typescript
// Error class definition
class ValidationError extends Error {
  constructor(
    message: string,
    public field: string,
    public value: unknown
  ) {
    super(message);
    this.name = 'ValidationError';
  }
}

// Error handling
try {
  const task = await taskService.create(data);
} catch (error) {
  if (error instanceof ValidationError) {
    console.error(`Validation error [${error.field}]: ${error.message}`);
    // Give feedback to the user
  } else {
    console.error('Unexpected error:', error);
    throw error; // Propagate upward
  }
}
```

## Git Workflow Rules

### Branching Strategy

**Branch types**:
- `main`: Always deployable to production
- `develop`: Latest state of development
- `feature/[feature-name]`: New feature development
- `fix/[fix-description]`: Bug fixes
- `refactor/[target]`: Refactoring

**Flow**:
```
main
  └─ develop
      ├─ feature/task-management
      ├─ feature/user-auth
      └─ fix/task-validation
```

### Commit Message Conventions

**Format**:
```
<type>(<scope>): <subject>

<body>

<footer>
```

**Type**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `style`: Code formatting
- `refactor`: Refactoring
- `test`: Adding or fixing tests
- `chore`: Build, auxiliary tools, etc.

**Example**:
```
feat(task): add task priority setting feature

Allow users to set a priority (high/medium/low) on tasks.
- Added a priority field to the Task model
- Added a --priority option to the CLI
- Implemented sorting by priority

Closes #123
```

### Pull Request Process

**Checks before creating**:
- [ ] All tests pass
- [ ] No lint errors
- [ ] Type checking passes
- [ ] Conflicts are resolved

**PR template**:
```markdown
## Summary
[Concise description of the changes]

## Reason for Change
[Why this change is needed]

## Changes
- [Change 1]
- [Change 2]

## Testing
- [ ] Unit tests added
- [ ] Manual testing performed

## Screenshots (if applicable)
[Images]

## Related Issues
Closes #[Issue number]
```

**Review process**:
1. Self-review
2. Run automated tests
3. Assign reviewers
4. Address review feedback
5. Merge after approval

## Test Strategy

### Test Types

#### Unit Tests

**Scope**: Individual functions and classes

**Coverage target**: [80/90/100]%

**Example**:
```typescript
describe('TaskService', () => {
  describe('create', () => {
    it('creates a task with valid data', async () => {
      const service = new TaskService(mockRepository);
      const task = await service.create({
        title: 'Test task',
        description: 'Description',
      });

      expect(task.id).toBeDefined();
      expect(task.title).toBe('Test task');
    });

    it('throws ValidationError when the title is empty', async () => {
      const service = new TaskService(mockRepository);

      await expect(
        service.create({ title: '' })
      ).rejects.toThrow(ValidationError);
    });
  });
});
```

#### Integration Tests

**Scope**: Interaction between multiple components

**Example**:
```typescript
describe('Task CRUD', () => {
  it('can create, read, update, and delete a task', async () => {
    // Create
    const created = await taskService.create({ title: 'Test' });

    // Read
    const found = await taskService.findById(created.id);
    expect(found?.title).toBe('Test');

    // Update
    await taskService.update(created.id, { title: 'Updated' });
    const updated = await taskService.findById(created.id);
    expect(updated?.title).toBe('Updated');

    // Delete
    await taskService.delete(created.id);
    const deleted = await taskService.findById(created.id);
    expect(deleted).toBeNull();
  });
});
```

#### E2E Tests

**Scope**: Entire user scenarios

**Example**:
```typescript
describe('Task management flow', () => {
  it('allows a user to add and complete a task', async () => {
    // Add a task
    await cli.run(['add', 'New task']);
    expect(output).toContain('Task added');

    // Show the task list
    await cli.run(['list']);
    expect(output).toContain('New task');

    // Complete the task
    await cli.run(['complete', '1']);
    expect(output).toContain('Task completed');
  });
});
```

### Test Naming Conventions

**Pattern**: `[target]_[condition]_[expected result]`

**Example**:
```typescript
// ✅ Good examples
it('create_emptyTitle_throwsValidationError', () => { });
it('findById_existingId_returnsTask', () => { });
it('delete_nonExistentId_throwsNotFoundError', () => { });

// ❌ Bad examples
it('test1', () => { });
it('works', () => { });
it('should work correctly', () => { });
```

### Using Mocks and Stubs

**Principles**:
- Mock external dependencies (APIs, DB, file system)
- Use the real implementation for business logic

**Example**:
```typescript
// Mock the repository
const mockRepository: ITaskRepository = {
  save: jest.fn(),
  findById: jest.fn(),
  findAll: jest.fn(),
  delete: jest.fn(),
};

// Use the actual implementation for the service
const service = new TaskService(mockRepository);
```

## Code Review Standards

### Review Points

**Functionality**:
- [ ] Are the requirements met?
- [ ] Are edge cases considered?
- [ ] Is error handling appropriate?

**Readability**:
- [ ] Is the naming clear?
- [ ] Are comments appropriate?
- [ ] Is complex logic explained?

**Maintainability**:
- [ ] Is there no duplicated code?
- [ ] Are responsibilities clearly separated?
- [ ] Is the impact scope of changes limited?

**Performance**:
- [ ] Are there no unnecessary computations?
- [ ] Is there no risk of memory leaks?
- [ ] Are database queries optimized?

**Security**:
- [ ] Is input validation appropriate?
- [ ] Are no secrets hardcoded?
- [ ] Are permission checks implemented?

### How to Write Review Comments

**Constructive feedback**:
```markdown
## ✅ Good example
With this implementation, performance may degrade as the number of tasks grows.
How about considering an index-based lookup instead?

## ❌ Bad example
This code is not good.
```

**Make priorities explicit**:
- `[Required]`: Must fix
- `[Recommended]`: Should fix
- `[Suggestion]`: Please consider
- `[Question]`: Question for understanding

## Development Environment Setup

### Required Tools

| Tool | Version | Installation Method |
|--------|-----------|-----------------|
| [Tool 1] | [Version] | [Command] |
| [Tool 2] | [Version] | [Command] |

### Setup Steps

```bash
# 1. Clone the repository
git clone [URL]
cd [project-name]

# 2. Install dependencies
[Install command]

# 3. Configure environment variables
cp .env.example .env
# Edit the .env file

# 4. Start the development server
[Start command]
```

### Recommended Development Tools (if applicable)

- [Tool 1]: [Description]
- [Tool 2]: [Description]
