# Architecture Design Guide

## Basic Principles

### 1. State the Rationale for Every Technology Choice

**Bad example**:
```
- Node.js
- TypeScript
```

**Good example**:
```
- Node.js v24.11.0 (LTS)
  - Long-term support guaranteed until April 2026, so stable operation in production can be expected
  - Excellent asynchronous I/O handling, delivering high performance as an API server
  - Rich npm ecosystem makes it easy to obtain the libraries we need

- TypeScript 5.x
  - Static typing catches bugs at compile time, improving maintainability
  - Powerful IDE completion features increase development efficiency
  - Sharing type definitions in team development ensures code readability and quality

- npm 11.x
  - Bundled with Node.js v24.11.0, so no separate installation is required
  - The workspaces feature supports monorepo layouts
  - package-lock.json enables strict dependency management
```

### 2. Layer Separation Principle

Make each layer's responsibilities explicit and keep dependencies one-directional:

```
UI → Service → Data (OK)
UI ← Service (NG)
UI → Data (NG)
```

### 3. Measurable Requirements

Write all performance requirements in a measurable form.

## Designing a Layered Architecture

### Responsibilities of Each Layer

**UI layer**:
```typescript
// Responsibility: accept and validate user input
class CLI {
  // OK: call the service layer
  async addTask(title: string) {
    const task = await this.taskService.create({ title });
    console.log(`Created: ${task.id}`);
  }

  // NG: call the data layer directly
  async addTask(title: string) {
    const task = await this.repository.save({ title }); // ❌
  }
}
```

**Service layer**:
```typescript
// Responsibility: implement business logic
class TaskService {
  // Business logic: automatic priority estimation
  async create(data: CreateTaskData): Promise<Task> {
    const task = {
      ...data,
      estimatedPriority: this.estimatePriority(data),
    };
    return this.repository.save(task);
  }
}
```

**Data layer**:
```typescript
// Responsibility: data persistence
class TaskRepository {
  async save(task: Task): Promise<void> {
    await this.storage.write(task);
  }
}
```

## Setting Performance Requirements

### Concrete Numeric Targets

```
Command execution time: within 100ms (on an average PC environment)
└─ Measurement method: measure from CLI startup to result display with console.time
└─ Measurement environment: CPU equivalent to Core i5, 8GB RAM, SSD

Task list display: within 1 second for up to 1,000 items
└─ Measurement method: measure with 1,000 dummy data records
└─ Acceptable range: 100ms for 100 items, 1 second for 1,000 items, 10 seconds for 10,000 items
```

## Security Design

### Three Principles of Data Protection

1. **Principle of least privilege**
```bash
# File permissions
chmod 600 ~/.devtask/tasks.json  # Read/write for the owner only
```

2. **Input validation**
```typescript
function validateTitle(title: string): void {
  if (!title || title.length === 0) {
    throw new ValidationError('Title is required');
  }
  if (title.length > 200) {
    throw new ValidationError('Title must be 200 characters or fewer');
  }
}
```

3. **Managing secrets**
```bash
# Manage via environment variables
export DEVTASK_API_KEY="xxxxx"  # Never hardcode in source code
```

## Scalability Design

### Handling Data Growth

**Expected data volume**: [e.g., 10,000 tasks]

**Countermeasures**:
- Paginate data
- Archive old data
- Optimize indexes

```typescript
// Example archive feature: move old tasks to a separate file
class ArchiveService {
  async archiveCompletedTasks(olderThan: Date): Promise<void> {
    const oldTasks = await this.repository.findCompleted(olderThan);
    await this.archiveStorage.save(oldTasks);
    await this.repository.deleteMany(oldTasks.map(t => t.id));
  }
}
```

## Dependency Management

### Version Management Policy

```json
{
  "dependencies": {
    "commander": "^11.0.0",  // Minor version updates are automatic
    "chalk": "5.3.0"         // Pin when there is risk of breaking changes
  },
  "devDependencies": {
    "typescript": "~5.3.0",  // Only patch versions update automatically
    "eslint": "^9.0.0"
  }
}
```

**Policy**:
- Pin stable versions (allow up to minor versions with ^)
- Pin exactly when there is risk of breaking changes
- For devDependencies, allow only automatic patch updates (~)

## Checklist

- [ ] Every technology choice includes a stated rationale
- [ ] The layered architecture is clearly defined
- [ ] Performance requirements are measurable
- [ ] Security considerations are documented
- [ ] Scalability has been considered
- [ ] A backup strategy is defined
- [ ] The dependency management policy is clear
- [ ] A test strategy is defined
