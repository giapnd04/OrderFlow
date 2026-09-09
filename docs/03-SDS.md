# OrderFlow — System Design Specification

**Version:** 1.0
**Status:** Baseline
**Phase:** Phase 2 — System Design
**Architecture:** Clean Architecture
**Technology:** ASP.NET Core .NET 8, EF Core, SQL Server, Angular

---

# 1. Architecture Overview

OrderFlow sử dụng **Clean Architecture** nhằm tách business logic khỏi framework, persistence technology và external implementation details.

High-level architecture:

```text
                         Angular
                            │
                            ▼
                    ┌───────────────┐
                    │ OrderFlow.Api │
                    └───────┬───────┘
                            │
                            ▼
                 ┌──────────────────────┐
                 │ OrderFlow.Application│
                 └──────────┬───────────┘
                            │
                            ▼
                  ┌───────────────────┐
                  │  OrderFlow.Domain │
                  └───────────────────┘


          ┌──────────────────────────────┐
          │     OrderFlow.Infrastructure │
          └──────────────┬───────────────┘
                         │
                         ▼
                      EF Core
                         │
                         ▼
                    SQL Server
```

Infrastructure implements persistence and external concerns required by the Application/Core layers.

The architecture follows the principle that **business logic should not depend on infrastructure or framework implementation details**.

## Architectural principles

1. Domain remains independent from frameworks and infrastructure.
2. Application depends on Domain.
3. Infrastructure implements abstractions required by Application/Domain.
4. API exposes HTTP endpoints and invokes Application use cases.
5. Persistence implementation must not leak into Application abstractions.
6. Dependency direction must point toward the application/domain core.
7. Additional abstractions are introduced only when they solve a real coupling or design problem.

---

# 2. Project Responsibilities

| Project                    | Responsibility                                                                                      | Allowed Dependencies           | Forbidden Dependencies                                                  |
| -------------------------- | --------------------------------------------------------------------------------------------------- | ------------------------------ | ----------------------------------------------------------------------- |
| `OrderFlow.Api`            | HTTP/API boundary, controllers, authentication/authorization boundary, HTTP response handling       | Application                    | Direct dependency on Infrastructure implementation, EF Core, SQL Server |
| `OrderFlow.Application`    | Use cases, application orchestration, application-facing abstractions, DTO/contracts as appropriate | Domain                         | Infrastructure implementation, EF Core, SQL Server                      |
| `OrderFlow.Domain`         | Entities, value objects if needed, domain rules and invariants                                      | No external project dependency | Api, Application, Infrastructure, EF Core, ASP.NET Core, SQL Server     |
| `OrderFlow.Infrastructure` | Persistence implementation, EF Core, external services, repository implementations                  | Application and Domain         | Reverse dependency from Domain to Infrastructure                        |

The exact placement of individual abstractions may be refined during implementation if a concrete use case demonstrates a better boundary.

---

# 3. Dependency Rules

## 3.1 Dependency Direction

Confirmed dependency direction:

```text
Api
 ↓
Application
 ↓
Domain

Infrastructure
 ↓
Application
 ↓
Domain
```

The Domain must not depend on any outer layer.

The Application must not depend on Infrastructure.

The API should not directly depend on persistence implementations.

---

## 3.2 Dependency Inversion

Dependency Inversion is applied primarily around infrastructure concerns.

Example:

```text
Application
     │
     ▼
IOrderRepository
     ▲
     │ implements
     │
Infrastructure
     │
     ▼
OrderRepository
     │
     ▼
EF Core
     │
     ▼
SQL Server
```

The Application depends on an abstraction representing the persistence capability it requires.

Infrastructure provides the concrete implementation.

This allows persistence technology to change without forcing Application business/use-case code to depend directly on EF Core or SQL Server.

---

## 3.3 Composition Root

Dependency Injection wiring is handled at the application composition root.

The composition root may reference Infrastructure in order to register concrete implementations.

This does not mean Controllers or Application Services should directly depend on Infrastructure implementations.

Conceptually:

```text
Composition Root
      │
      ├── Application services
      │
      └── Infrastructure implementations
```

---

# 4. Component Responsibilities

## 4.1 Controllers

Controllers represent the HTTP boundary.

Expected responsibilities:

* Receive HTTP requests.
* Bind/deserialize request data.
* Perform appropriate HTTP/input-level validation.
* Invoke the appropriate Application use case.
* Convert application result into an HTTP response.
* Return appropriate HTTP status codes.

Controllers should not:

* Contain core business rules.
* Directly access `DbContext`.
* Directly execute database queries.
* Directly manipulate repositories for business workflows.
* Contain complex order-processing logic.

Working principle:

```text
HTTP Request
     ↓
Controller
     ↓
Application Use Case
```

---

# 5. Application Layer

The Application layer represents system use cases.

Examples may include:

```text
CreateOrder
GetOrder
CancelOrder
UpdateOrder
ProcessPayment
```

depending on the final SRS/business scope.

Application responsibilities:

* Coordinate a business use case.
* Coordinate multiple repositories/services when required.
* Manage application workflow.
* Invoke domain behavior.
* Define application-facing abstractions where appropriate.
* Transform input/output between API/application boundaries where appropriate.

Application should not:

* Depend directly on EF Core.
* Depend directly on SQL Server.
* Contain persistence implementation.
* Become a container for every business rule.

For example, `CreateOrder` may require:

```text
ICustomerRepository
IProductRepository
IOrderRepository
```

because one use case may need to coordinate several data sources.

However, excessive dependencies in one use case may indicate a poor boundary or an overly complex use case and should be reviewed during implementation.

---

# 6. Domain Layer

The Domain layer represents the core business model.

Potential domain concepts include:

```text
Order
OrderItem
Product
Customer
```

and other domain concepts defined by the SRS.

Domain responsibilities:

* Represent business entities.
* Maintain domain invariants.
* Encapsulate business behavior where appropriate.
* Enforce rules that must always remain true regardless of how the system is called.

The Domain must remain independent of:

* ASP.NET Core
* EF Core
* SQL Server
* Infrastructure
* API concerns

The Domain should not be responsible for:

* HTTP requests/responses.
* Database access.
* Authentication framework implementation.
* API serialization.
* Persistence implementation.

---

# 7. Infrastructure Layer

Infrastructure contains implementation details.

Primary responsibilities:

* EF Core configuration.
* `DbContext`.
* Repository implementations.
* Database access.
* SQL Server integration.
* External service implementations if required.

Example:

```text
OrderFlow.Infrastructure
│
├── Persistence
│   ├── OrderFlowDbContext
│   ├── Configurations
│   └── Migrations
│
├── Repositories
│   └── OrderRepository
│
└── ExternalServices
    └── ...
```

The exact structure may evolve during implementation.

Infrastructure must implement Application-facing abstractions without forcing those abstractions to expose EF Core-specific concepts.

---

# 8. Repository Strategy

OrderFlow uses repositories where they provide a meaningful abstraction around persistence.

Example:

```text
Application
    │
    ▼
IOrderRepository
```

Implementation:

```text
Infrastructure
    │
    ▼
OrderRepository
    │
    ▼
EF Core
```

## Repository abstraction rules

Repository interfaces should express capabilities required by the use case.

Examples:

```text
GetById
Exists
Add
Update
```

Exact methods will be determined during implementation based on actual use cases.

Repository interfaces should **not expose persistence implementation details**, such as:

```text
DbSet<T>
IQueryable<T>
EF Core-specific APIs
```

unless a concrete architectural decision later demonstrates a strong reason to do so.

The goal is to prevent:

```text
Application
    ↓
EF Core-specific abstraction
```

---

# 9. EF Core and DbContext

EF Core is an Infrastructure concern.

`DbContext` belongs to:

```text
OrderFlow.Infrastructure
```

and is responsible for:

* Database connection.
* Entity configuration.
* Change tracking.
* Persistence operations.
* Transaction participation.
* EF Core-specific behavior.

Application and Domain should not directly depend on `DbContext`.

---

# 10. Request Flow

Typical OrderFlow request:

```text
Angular
   ↓
HTTP Request
   ↓
Controller
   ↓
Application Use Case
   ↓
Domain Entity / Domain Behavior
   ↓
Repository Abstraction
   ↓
Infrastructure Repository
   ↓
EF Core
   ↓
SQL Server
```

Example `CreateOrder`:

```text
POST /api/orders
        ↓
OrderController
        ↓
CreateOrder Use Case
        ↓
Customer/Product validation and retrieval
        ↓
Create Order Domain Entity
        ↓
Apply Domain Rules
        ↓
IOrderRepository.Add(...)
        ↓
OrderRepository
        ↓
EF Core
        ↓
SQL Server
```

The exact sequence will be refined when the detailed Order business flow is implemented.

---

# 11. DTO / Entity Boundary

API models should not automatically expose Domain Entities directly.

Initial strategy:

```text
API Request DTO
       ↓
Application
       ↓
Domain Entity
       ↓
Application Result / DTO
       ↓
API Response DTO
```

Domain Entities represent business concepts.

DTOs represent communication contracts.

This prevents API contracts from becoming tightly coupled to the internal Domain model.

## Mapping

Mapping responsibility is currently treated as an Application/API boundary concern rather than a Domain responsibility.

The project will avoid introducing a mapping framework purely for the sake of abstraction.

Manual mapping may be preferred where the number of DTOs remains small.

The final mapping strategy is **working decision** and can be refined during implementation.

---

# 12. Validation Strategy

Validation is divided into three categories.

## 12.1 Input Validation

Examples:

```text
quantity > 0
required fields
string length
format
```

These concern whether an incoming request has a valid structure.

They belong at the application/API boundary.

---

## 12.2 Business Validation

Examples may include:

```text
Customer must exist
Product must be available
Order cannot be cancelled after a certain state
```

These are determined by OrderFlow business rules.

Application coordinates validations requiring external state.

Domain enforces rules that are intrinsic to the domain model.

---

## 12.3 Domain Invariants

A domain invariant is a condition that must always remain true for a valid Domain object.

Examples:

```text
Order cannot contain an invalid quantity.
Order cannot transition to an invalid status.
```

These should be protected by the Domain model rather than relying exclusively on Controllers.

---

# 13. Transaction Strategy

Initial strategy:

> Transaction boundary should align with the Application use case when a use case requires multiple persistence operations to succeed or fail atomically.

Example:

```text
CreateOrder
    │
    ├── Read Customer
    ├── Read Products
    ├── Create Order
    └── Persist Order
```

If the use case requires atomic persistence, the transaction must ensure that relevant changes are committed consistently.

`DbContext` can provide Unit-of-Work-like behavior through EF Core's change tracking and `SaveChanges`.

A separate `IUnitOfWork` abstraction will **not be introduced automatically**.

It will only be considered if actual use cases demonstrate a need for an explicit abstraction.

Detailed transaction boundary is a **working/deferred decision** to be finalized after concrete use cases are designed.

---

# 14. Error Handling

OrderFlow distinguishes between different error categories.

## Validation Error

Example:

```text
Invalid quantity
Missing required field
Invalid request
```

Expected HTTP response:

```text
400 Bad Request
```

---

## Not Found

Example:

```text
Customer does not exist
Product does not exist
Order does not exist
```

Expected response:

```text
404 Not Found
```

---

## Conflict

Example:

```text
Invalid order state transition
Business operation conflicts with current state
```

Expected response:

```text
409 Conflict
```

---

## Unauthorized / Forbidden

Authentication/authorization failures should be represented using appropriate HTTP status codes.

---

## Unexpected Exception

Unexpected technical errors should not leak internal implementation details to clients.

They should be handled centrally at the API boundary through exception-handling middleware or an equivalent mechanism.

The exact error-response format is a **working decision** to be finalized during API design.

---

# 15. Authentication & Authorization Boundary

Authentication is an API/security infrastructure concern.

The API is responsible for integrating with the authentication mechanism.

Authorization determines whether an authenticated actor may perform an operation.

Application use cases may enforce business authorization rules where necessary, but should not become tightly coupled to ASP.NET Core authentication implementation.

Domain should not depend directly on:

```text
HttpContext
ASP.NET Core Identity implementation
JWT framework APIs
```

Security implementation details remain outside the Domain.

Detailed authentication mechanism is deferred to the Security/API Design stage.

---

# 16. Cross-cutting Concerns

## Logging

Logging is treated as a cross-cutting concern.

Application and infrastructure may produce meaningful logs through abstractions/framework-supported logging mechanisms without embedding logging logic into Domain entities.

---

## Exception Handling

Centralized exception handling should exist at the API boundary.

Controllers should not contain repetitive:

```text
try/catch
```

logic for every endpoint.

---

## Validation

Validation is separated between:

```text
Input validation
Business validation
Domain invariants
```

as described in Section 12.

---

## Authentication / Authorization

Handled at the API/security boundary with business-level authorization rules applied where necessary.

---

## Configuration

Configuration of:

* database connection
* external services
* authentication
* environment-specific settings

belongs outside the Domain.

Infrastructure owns infrastructure-specific configuration.

---

# 17. Non-functional Considerations

## Performance

Initial focus:

* Appropriate database indexes.
* Efficient EF Core queries.
* Avoid unnecessary database round trips.
* Avoid loading unnecessary data.
* Pagination for potentially large collections.

No premature optimization will be introduced before identifying actual bottlenecks.

---

## Scalability

OrderFlow is initially designed as a modular monolithic backend.

The architecture should allow individual infrastructure concerns to evolve without forcing a rewrite of Domain business logic.

Microservices are not part of the current scope.

---

## Maintainability

Maintainability is supported through:

* Separation of concerns.
* Explicit dependency direction.
* Use-case-oriented Application layer.
* Technology-independent Domain.
* Isolated Infrastructure implementation.

---

## Testability

The architecture should allow:

```text
Domain
    ↓
Unit Tests

Application
    ↓
Use-case tests with mocked/fake abstractions

Infrastructure
    ↓
Integration tests
```

The goal is not to mock every dependency automatically.

Tests should be selected based on the responsibility being verified.

---

## Security

Security considerations include:

* Authentication.
* Authorization.
* Input validation.
* Safe error handling.
* Protection of secrets/configuration.
* Database access through controlled infrastructure.

Detailed security requirements are deferred to the security phase.

---

# 18. Architectural Trade-offs

## Decision 1 — Use Clean Architecture

### Decision

**Use Clean Architecture.**

### Reason

OrderFlow is a portfolio Backend .NET project intended not only to produce a working system but also to demonstrate understanding of:

* dependency management
* separation of concerns
* domain modeling
* application use cases
* persistence abstraction
* testability
* maintainable backend architecture

### Trade-off

Advantages:

* Clear separation of responsibilities.
* Strong dependency boundaries.
* Better testability.
* Domain is independent of persistence technology.
* Demonstrates useful backend architectural knowledge.

Disadvantages:

* More projects and abstractions than a simple CRUD application.
* More design decisions are required.
* Can become over-engineered if abstractions are added without a real need.

### Rejected Alternative

A simple layered architecture could reduce initial complexity, but Clean Architecture is retained because the project is explicitly intended as a learning and portfolio project and has enough business workflow to justify meaningful separation.

---

# 19. Explicitly Rejected Patterns

The following patterns are intentionally avoided.

## Controller → DbContext

```text
Controller
    ↓
DbContext
```

Reason:

Controllers should not contain persistence logic.

---

## Controller → Repository

```text
Controller
    ↓
Repository
```

Reason:

This bypasses the Application use-case boundary.

---

## Application → EF Core

```text
Application
    ↓
EF Core
```

Reason:

This couples application logic to persistence technology.

---

## Domain → EF Core

```text
Domain
    ↓
EF Core
```

Reason:

This couples business logic to infrastructure.

---

## Repository abstraction exposing EF Core

Avoid:

```text
IOrderRepository
{
    IQueryable<Order>
    DbSet<Order>
}
```

Reason:

The abstraction leaks persistence technology and weakens Dependency Inversion.

---

# 20. Architectural Decisions Intentionally NOT Made Yet

The following decisions are deliberately deferred:

* Exact Application Service vs Use Case class structure.
* Exact Domain Service requirements.
* Exact DTO organization.
* Mapping library vs manual mapping.
* Detailed repository method signatures.
* Query-specific read model strategy.
* Detailed transaction implementation.
* Whether an explicit Unit of Work abstraction is necessary.
* Detailed authentication implementation.
* Detailed authorization policy design.
* Caching strategy.
* Advanced performance optimizations.

These decisions should be made when concrete requirements/use cases demonstrate that they are necessary.

---

# 21. Known Limitations

Current SDS v1 has several areas that are not fully validated yet.

### 1. Layer responsibilities require deeper review

The dependency direction has been reviewed, but Controller/Application/Domain responsibilities still require detailed challenge.

### 2. Business logic placement requires validation

The architecture permits Domain logic, but actual OrderFlow business rules still need to be mapped explicitly to Domain vs Application.

### 3. Repository design is not finalized

The architectural principle is established:

```text
Application abstraction
        ↓
Infrastructure implementation
```

but exact repository boundaries and query methods remain to be designed.

### 4. Transaction strategy is preliminary

The project currently assumes use-case-oriented transaction boundaries, but concrete multi-step workflows must be analyzed before finalizing implementation.

---

# 22. Deferred Decisions

The following items can safely be resolved later without blocking the overall architecture:

```text
Detailed DTO design
Detailed mapper strategy
Repository method details
Unit of Work necessity
Exact validation framework
Exact exception response format
Authentication implementation
Authorization policies
Caching
Advanced performance optimization
```

The guiding principle is:

> Do not introduce an abstraction or infrastructure component until a concrete requirement justifies it.

---

# 23. Architecture Quality Check

## 1. Consistent with SRS?

**Yes, at the architectural level.**

The architecture supports Customer, Product, Order, Inventory, Payment and related capabilities identified as core/supporting capabilities.

Detailed mapping between every SRS requirement and component will be validated during detailed use-case design.

---

## 2. Business flow can map to architecture?

**Yes.**

Typical flow:

```text
API
 ↓
Application Use Case
 ↓
Domain Behavior
 ↓
Repository Abstraction
 ↓
Infrastructure
 ↓
Database
```

---

## 3. Dependency direction consistent?

**Yes.**

```text
Api → Application → Domain

Infrastructure → Application → Domain
```

No inward dependency from Domain to Infrastructure is permitted.

---

## 4. Responsibility overlap?

Potential overlap remains to be reviewed.

Particularly:

```text
Controller
Application Service
Domain Entity
```

This is a **Phase 2 follow-up item**, not an architectural blocker.

---

## 5. Unjustified abstractions?

No major unnecessary abstraction has been identified yet.

Repository abstractions are retained because they represent application-required persistence capabilities and isolate Infrastructure.

A separate Unit of Work abstraction is intentionally not introduced yet.

---

## 6. Over-engineering?

Current architecture has moderate complexity but is considered acceptable for OrderFlow.

The main over-engineering risk is not the four-project structure itself, but adding:

```text
interfaces
factories
mediators
generic repositories
unit of work
mapping frameworks
domain services
```

without a concrete requirement.

---

## 7. Decisions without rationale?

The major architecture decision has rationale.

Several detailed implementation decisions remain deferred intentionally.

---

## 8. SDS describes unsupported architecture?

No major contradiction identified.

Some sections are intentionally marked as working/deferred decisions.

---

## 9. Architecture not documented?

Dependency direction, responsibilities, repository boundary, transaction approach, DTO boundary and cross-cutting concerns have been documented at the current level.

---

## 10. Ready for implementation?

**Not fully locked yet.**

The architecture is sufficiently defined to establish the codebase structure, but the complete SDS should be considered **v1 baseline rather than final architecture specification**.

Before substantial implementation, the following should be reviewed:

```text
B. Layer Responsibility
C. Business Logic
D. Repository
E. Transaction
F. DTO / Entity Boundary
G. Cross-cutting Concerns
```

---

# 24. Current Architecture Decision

**Decision: KEEP WITH CONTROLLED ADJUSTMENTS**

The four-project Clean Architecture structure remains:

```text
OrderFlow
│
├── OrderFlow.Api
├── OrderFlow.Application
├── OrderFlow.Domain
└── OrderFlow.Infrastructure
```

with:

```text
Api
 ↓
Application
 ↓
Domain

Infrastructure
 ↓
Application
 ↓
Domain
```

The architecture is intentionally kept simple enough for the actual scale of OrderFlow.

No additional architectural layer or abstraction is introduced unless a concrete business/use-case requirement justifies it.

---

# 25. Phase 2 Principle

The architecture is not considered successful merely because the project contains four projects.

The actual success criteria are:

```text
Business Requirement
        ↓
Use Case
        ↓
Application Responsibility
        ↓
Domain Responsibility
        ↓
Infrastructure Responsibility
        ↓
Persistence
```

Every important architectural decision should be explainable through:

```text
Problem
   ↓
Options
   ↓
Trade-off
   ↓
Decision
   ↓
Rationale
```

This SDS should therefore evolve when new design decisions are validated rather than being treated as a document that must be perfect before implementation.
