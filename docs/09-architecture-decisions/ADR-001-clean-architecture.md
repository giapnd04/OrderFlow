# ADR-001: Use Clean Architecture

## Status

Accepted

## Context

OrderFlow is a backend ordering system that needs to be maintainable, testable, and easy to extend.

The system contains multiple responsibilities such as:

- Business rules
- Application use cases
- Database access
- Authentication and authorization
- HTTP API
- External services

If these responsibilities are tightly coupled, changes to one part of the system may affect other parts and make the system difficult to maintain and test.

Therefore, a clear separation of responsibilities is required.

## Decision

We will use Clean Architecture for the OrderFlow backend.

The system will be separated into the following layers:

- Domain
- Application
- Infrastructure
- API

The dependency direction will point inward toward the Domain layer.

```text
API
 ↓
Application
 ↓
Domain

Infrastructure → Application / Domain
```

The Domain layer must not depend on infrastructure technologies such as Entity Framework Core or ASP.NET Core.

## Alternatives Considered

### Option 1: Traditional Layered Architecture

```text
Controller
    ↓
Service
    ↓
Repository
    ↓
Database
```

Advantages:

- Simple to understand
- Fast to implement
- Suitable for small applications

Disadvantages:

- Business logic can become tightly coupled to infrastructure
- Testing may become harder
- Dependency boundaries are less strict

### Option 2: Clean Architecture

Advantages:

- Clear separation of responsibilities
- Better testability
- Business logic is independent from infrastructure
- Easier to replace infrastructure implementations
- Suitable for demonstrating professional backend architecture

Disadvantages:

- More projects and abstractions
- Higher initial development complexity
- Can be excessive for very small applications

## Consequences

### Positive

- Business logic is separated from infrastructure concerns.
- Unit testing becomes easier.
- Infrastructure technologies can be replaced with less impact on business logic.
- The codebase has clear architectural boundaries.
- The architecture provides a good foundation for future features.

### Negative

- More files and projects are required.
- Developers need to understand dependency inversion and architectural boundaries.
- Some simple features may require more code than a traditional layered architecture.

## Related Documents

- `docs/02-SRS.md`
- `docs/03-SDS.md`
- `docs/04-database-design.md`
- `docs/05-api-design.md`
