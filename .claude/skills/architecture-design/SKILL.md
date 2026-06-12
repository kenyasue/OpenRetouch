---
name: architecture-design
description: Detailed guide and template for creating an architecture design document. Use only when designing architecture.
allowed-tools: Read, Write
---

# Architecture Design Skill

This skill is a detailed guide for creating a high-quality architecture design document.

## Prerequisites

Before starting architecture design, confirm the following:

### Required Documents

1. `docs/product-requirements.md` (PRD)
2. `docs/functional-design.md` (Functional Design Document)

Architecture design defines the system structure and technology stack
required to technically realize the PRD requirements and the functional design.

## Priority of Existing Documents

**Important**: If an existing architecture design document exists at `docs/architecture.md`,
follow this order of priority:

1. **Existing architecture design document (`docs/architecture.md`)** - Highest priority
   - Contains project-specific technology choices and design
   - Takes precedence over this skill's guide

2. **This skill's guide** - Reference material
   - Generic templates and examples
   - Use when no existing design document exists, or as a supplement

**When creating a new document**: Refer to this skill's template and guide
**When updating**: Update while preserving the structure and content of the existing design document

## Output Location

Save the completed architecture design document to:

```
docs/architecture.md
```

## Template Reference

When creating the architecture design document, use the template while referring to the following guide:
- Guide: ./guide.md
- Template: ./template.md
