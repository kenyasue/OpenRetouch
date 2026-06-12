---
name: repository-structure
description: Detailed guide and template for creating a repository structure document. Use only when defining the repository structure.
allowed-tools: Read, Write
---

# Repository Structure Definition Skill

This skill is a detailed guide for defining a clear, maintainable repository structure.

## Prerequisites

Before starting to define the repository structure, confirm the following:

### Required Documents

1. `docs/product-requirements.md` (PRD)
2. `docs/functional-design.md` (Functional Design Document)
3. `docs/architecture.md` (Architecture Design Document)

The repository structure defines a concrete directory structure that reflects the technology stack and system composition decided in the architecture design.

## Priority of Existing Documents

**Important**: If an existing repository structure document exists at `docs/repository-structure.md`, follow this order of priority:

1. **Existing repository structure document (`docs/repository-structure.md`)** - Highest priority
   - Contains the project-specific directory structure
   - Takes precedence over this skill's guide

2. **This skill's guide** - Reference material
   - Generic templates and examples
   - Use when no existing document exists, or as a supplement

**When creating a new document**: Refer to this skill's template and guide
**When updating**: Update while preserving the structure and content of the existing document

## Output Location

Save the completed repository structure document to:

```
docs/repository-structure.md
```

## Template Reference

When creating the repository structure document, use the following template: ./template.md

## Detailed Guide

For a more detailed creation guide, refer to: ./guide.md
