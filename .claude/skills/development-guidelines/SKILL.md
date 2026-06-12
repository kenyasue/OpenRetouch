---
name: development-guidelines
description: Comprehensive guide and template for establishing a unified development process and coding standards across the whole team. Use when creating development guidelines and when implementing code.
allowed-tools: Read, Write, Edit
---

# Development Guidelines Skill

Covers the two elements required for team development:
1. Coding standards for implementation (implementation-guide.md)
2. Standardization of the development process (process-guide.md)

## Prerequisites

Before starting to create development guidelines, confirm the following:

### Recommended Documents

1. `docs/architecture.md` (Architecture Design Document) - to confirm the technology stack
2. `docs/repository-structure.md` (Repository Structure) - to confirm the directory structure

Development guidelines define concrete coding standards and a development process
based on the project's technology stack and directory structure.

## Priority of Existing Documents

**Important**: If existing development guidelines exist at `docs/development-guidelines.md`,
follow this order of priority:

1. **Existing development guidelines (`docs/development-guidelines.md`)** - Highest priority
   - Contain project-specific standards and processes
   - Take precedence over this skill's guides

2. **This skill's guides** - Reference material
   - ./guides/implementation.md: Generic coding standards
   - ./guides/process.md: Generic development process
   - Use when no existing guidelines exist, or as a supplement

**When creating a new document**: Refer to this skill's guides and template
**When updating**: Update while preserving the structure and content of the existing guidelines

## Output Location

Save the completed development guidelines to:

```
docs/development-guidelines.md
```

## Quick Reference

### When Implementing Code
Rules and standards for code implementation: ./guides/implementation.md

Contents include:
- TypeScript/JavaScript standards
- Type definitions and naming conventions
- Function design and error handling
- Comment conventions
- Security and performance
- Implementing test code
- Refactoring techniques

### When Referencing or Defining the Development Process
Git workflow, test strategy, code review: ./guides/process.md

Contents include:
- Basic principles (importance of concrete examples, explaining reasons)
- Git workflow rules (Git Flow branching strategy)
- Commit messages and the PR process
- Test strategy (pyramid and coverage)
- Code review process
- Quality automation

### Template
When creating development guidelines: ./template.md


## Usage Scenarios

### New Development
1. Check naming conventions and coding standards in ./guides/implementation.md
2. Check the branching strategy and PR handling in ./guides/process.md
3. Write tests first (TDD)

### Code Review
- Refer to "Code Review Process" in ./guides/process.md
- Check for standards violations against ./guides/implementation.md

### Test Design
- "Test Strategy" in ./guides/process.md (pyramid, coverage)
- "Test Code" in ./guides/implementation.md (implementation patterns)

### Release Preparation
- "Git Workflow Rules" in ./guides/process.md (policy for merging to main)
- Confirm commit messages follow Conventional Commits

## Checklist

- [ ] Coding standards are defined with concrete examples
- [ ] Naming conventions are clear (per language and project-specific)
- [ ] An error handling policy is defined
- [ ] A branching strategy has been decided (Git Flow recommended)
- [ ] Commit message conventions are clear
- [ ] A PR template is provided
- [ ] Test types and coverage targets are set
- [ ] A code review process is defined
- [ ] A CI/CD pipeline is in place
